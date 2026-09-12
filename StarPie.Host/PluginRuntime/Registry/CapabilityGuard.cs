using System;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.PluginRuntime.Lifecycle;

namespace StarPie.PluginRuntime.Registry
{
    /// <summary>
    /// 单插件的全部能力调用守卫：状态检查、在途计数、超时、异常捕获、连续失败熔断与隔离。
    /// </summary>
    /// <remarks>
    /// 只有 <see cref="PluginLifecycleState.Active"/> 允许执行插件代码：其余状态（含 Stopping）
    /// 立即拒绝新调用；卸载管线据 <see cref="InFlightCount"/> 与 <see cref="WaitForInFlightAsync"/>
    /// 排空在途调用。异常一律"记录并重抛"：宿主不吞插件异常，但异常在写入日志前已转成 DTO
    /// （见 <see cref="PluginLogEntry.FromException"/>），不把插件对象带进长生命周期结构。
    ///
    /// 在途计数按"插件代码真实结束"归零：调用超时或被调用方取消，只要插件实现还在跑就仍计在途，
    /// 卸载排空因此不会被谎报的归零骗过。同步能力调用在宿主线程池执行并由守卫限时等待
    /// （能力契约必须线程无关，界面线程亲和的工作不得写进能力实现）；异步能力调用把超时令牌
    /// 交给实现，实现须尊重该令牌才能被超时打断。两者超时后都不再等待残留执行，但残留执行
    /// 仍计入在途直到真正结束。
    ///
    /// 连续失败达到阈值即开断并调用 <see cref="PluginLifecycleStateMachine.Quarantine"/>（隔离是
    /// 终态，重试 = 重新装载）。调用方主动取消（非超时）不计插件失败。
    /// </remarks>
    public sealed class CapabilityGuard
    {
        private const string FailureLogMessage = "插件调用失败";
        private const string CircuitBreakLogMessage = "插件调用连续失败熔断，进入隔离";

        private readonly string _pluginId;
        private readonly PluginLifecycleStateMachine _lifecycle;
        private readonly IPluginLogSink _logSink;
        private readonly TimeSpan _callTimeout;
        private readonly int _failureThreshold;
        private readonly object _sync = new();

        private int _inFlight;
        private int _consecutiveFailures;
        private bool _circuitOpen;
        private TaskCompletionSource<bool>? _drained;

        /// <summary>构造单插件守卫。</summary>
        /// <param name="pluginId">插件 id（日志与异常用）。</param>
        /// <param name="lifecycle">该插件的生命周期状态机（守卫读状态、熔断时置隔离）。</param>
        /// <param name="logSink">日志落点；缺省写 Debug。</param>
        /// <param name="options">超时与熔断阈值；缺省 5 秒 / 3 次。</param>
        public CapabilityGuard(
            string pluginId,
            PluginLifecycleStateMachine lifecycle,
            IPluginLogSink? logSink = null,
            CapabilityGuardOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            ArgumentNullException.ThrowIfNull(lifecycle);

            _pluginId = pluginId;
            _lifecycle = lifecycle;
            _logSink = logSink ?? DebugPluginLogSink.Instance;
            CapabilityGuardOptions effective = options ?? CapabilityGuardOptions.Default;
            if (effective.FailureThreshold < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "熔断阈值至少为 1");
            }

            _callTimeout = effective.CallTimeout;
            _failureThreshold = effective.FailureThreshold;
        }

        /// <summary>当前在途调用数（插件代码仍在执行的调用；卸载安全点的排空判据）。</summary>
        public int InFlightCount
        {
            get
            {
                lock (_sync)
                {
                    return _inFlight;
                }
            }
        }

        /// <summary>连续失败计数（成功一次即复位）。</summary>
        public int ConsecutiveFailures
        {
            get
            {
                lock (_sync)
                {
                    return _consecutiveFailures;
                }
            }
        }

        /// <summary>熔断阈值（达到即隔离）。</summary>
        public int FailureThreshold => _failureThreshold;

        /// <summary>熔断是否已断开（断开后新调用一律快速失败）。</summary>
        public bool IsCircuitOpen
        {
            get
            {
                lock (_sync)
                {
                    return _circuitOpen;
                }
            }
        }

        /// <summary>守卫下的同步调用：在宿主线程池执行，超时由守卫判定。</summary>
        /// <param name="callTarget">调用面标识（能力 id；日志与异常用）。</param>
        /// <param name="call">调用体。</param>
        /// <param name="timeout">本次调用的超时覆盖；缺省用守卫配置。</param>
        /// <typeparam name="TResult">返回值类型。</typeparam>
        public TResult Invoke<TResult>(
            string callTarget,
            Func<TResult> call,
            TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(call);
            Enter(callTarget);

            TimeSpan effectiveTimeout = timeout ?? _callTimeout;
            Task<TResult>? execution = null;
            try
            {
                execution = Task.Run(call);
                TResult result = execution
                    .WaitAsync(effectiveTimeout)
                    .GetAwaiter()
                    .GetResult();
                RecordSuccess();
                return result;
            }
            catch (TimeoutException) when (execution is { IsCompleted: false })
            {
                var timeoutException = new CapabilityCallTimeoutException(
                    _pluginId,
                    callTarget,
                    effectiveTimeout);
                RecordFailure(callTarget, timeoutException);
                throw timeoutException;
            }
            catch (Exception exception)
            {
                RecordFailure(callTarget, exception);
                throw;
            }
            finally
            {
                ReleaseInFlightWhenCompleted(execution);
            }
        }

        /// <summary>守卫下的同步调用（无返回值）：在宿主线程池执行，超时由守卫判定。</summary>
        /// <param name="callTarget">调用面标识。</param>
        /// <param name="call">调用体。</param>
        /// <param name="timeout">本次调用的超时覆盖；缺省用守卫配置。</param>
        public void Invoke(string callTarget, Action call, TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(call);
            Invoke<object?>(callTarget, () =>
            {
                call();
                return null;
            }, timeout);
        }

        /// <summary>守卫下的异步调用：超时令牌交给实现，超时计一次失败。</summary>
        /// <param name="callTarget">调用面标识（能力 id）。</param>
        /// <param name="call">调用体（须尊重取消令牌才能被超时打断）。</param>
        /// <param name="timeout">本次调用的超时覆盖；缺省用守卫配置。</param>
        /// <param name="cancellationToken">调用方取消令牌（主动取消不计插件失败）。</param>
        /// <typeparam name="TResult">返回值类型。</typeparam>
        public async Task<TResult> InvokeAsync<TResult>(
            string callTarget,
            Func<CancellationToken, Task<TResult>> call,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(call);
            Enter(callTarget);

            TimeSpan effectiveTimeout = timeout ?? _callTimeout;

            // 插件只看到超时源（单源）：调用方取消与超时不再经链接源传播——链接源可能在自身
            // 取消回调执行前就被守卫释放，导致插件永远收不到取消。超时源由完成后的续接释放。
            var timeoutSource = new CancellationTokenSource(effectiveTimeout);
            Task<TResult>? execution = null;
            try
            {
                execution = call(timeoutSource.Token);
                Task first = await Task.WhenAny(
                        execution,
                        Task.Delay(effectiveTimeout),
                        WaitForCallerCancellationAsync(cancellationToken))
                    .ConfigureAwait(false);

                if (first == execution || execution.IsCompleted)
                {
                    TResult result = await execution.ConfigureAwait(false);
                    RecordSuccess();
                    return result;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    // 调用方取消：守卫不再等待；插件实现仍可能继续跑（在途计数以真实结束为准），
                    // 不计插件失败，但超时源仍会在到期时取消协作式实现。
                    throw new OperationCanceledException(cancellationToken);
                }

                var timeoutException = new CapabilityCallTimeoutException(
                    _pluginId,
                    callTarget,
                    effectiveTimeout);
                RecordFailure(callTarget, timeoutException);
                throw timeoutException;
            }
            catch (CapabilityCallTimeoutException)
            {
                // 已在超时分支记过一次失败：直接向上抛，不再进入通用记账。
                throw;
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    // 调用方主动取消：调用未完成但不是插件失败，不计入熔断。
                    throw;
                }

                if (exception is OperationCanceledException && timeoutSource.IsCancellationRequested)
                {
                    // 实现尊重超时令牌：按超时记账，异常语义统一。
                    var timeoutException = new CapabilityCallTimeoutException(
                        _pluginId,
                        callTarget,
                        effectiveTimeout);
                    RecordFailure(callTarget, timeoutException, exception);
                    throw timeoutException;
                }

                RecordFailure(callTarget, exception);
                throw;
            }
            finally
            {
                ReleaseInFlightWhenCompleted(execution, timeoutSource);
            }
        }

        /// <summary>守卫下的异步调用（无返回值）。</summary>
        /// <param name="callTarget">调用面标识。</param>
        /// <param name="call">调用体。</param>
        /// <param name="timeout">本次调用的超时覆盖；缺省用守卫配置。</param>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        public Task InvokeAsync(
            string callTarget,
            Func<CancellationToken, Task> call,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(call);
            return InvokeAsync<object?>(
                callTarget,
                async token =>
                {
                    await call(token).ConfigureAwait(false);
                    return null;
                },
                timeout,
                cancellationToken);
        }

        /// <summary>等待在途调用归零（卸载安全点用）：插件代码真正结束才算归零。</summary>
        /// <param name="timeout">最长等待时间。</param>
        /// <param name="cancellationToken">等待取消令牌。</param>
        /// <returns>true = 已归零；false = 超时仍有在途调用。</returns>
        public async Task<bool> WaitForInFlightAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> drained;
            lock (_sync)
            {
                if (_inFlight == 0)
                {
                    return true;
                }

                drained = _drained ??= new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            try
            {
                await drained.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        /// <summary>调用入口：状态检查 + 熔断检查 + 在途计数。</summary>
        private void Enter(string callTarget)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(callTarget);
            lock (_sync)
            {
                if (_circuitOpen)
                {
                    throw new CapabilityCircuitOpenException(_pluginId, callTarget, _consecutiveFailures);
                }

                PluginLifecycleState state = _lifecycle.Current;
                if (state != PluginLifecycleState.Active)
                {
                    throw new CapabilityUnavailableException(_pluginId, callTarget, state);
                }

                _inFlight++;
            }
        }

        /// <summary>调用出口：在途归零时唤醒排空等待者。</summary>
        private void Exit()
        {
            TaskCompletionSource<bool>? drained = null;
            lock (_sync)
            {
                _inFlight--;
                if (_inFlight == 0)
                {
                    drained = _drained;
                    _drained = null;
                }
            }

            drained?.TrySetResult(true);
        }

        /// <summary>
        /// 在途出账跟随"插件代码真实结束"：无执行任务（进入即失败）立刻出账；
        /// 有执行任务（含超时/取消后仍在跑的实现）等它真正完成才出账。
        /// </summary>
        private void ReleaseInFlightWhenCompleted(Task? execution, IDisposable? cleanUp = null)
        {
            if (execution is null)
            {
                cleanUp?.Dispose();
                Exit();
                return;
            }

            _ = execution.ContinueWith(
                task =>
                {
                    try
                    {
                        // 观察残留异常（超时/取消后被遗弃的执行），避免未观测异常噪声。
                        _ = task.Exception;
                    }
                    finally
                    {
                        cleanUp?.Dispose();
                        Exit();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>调用方取消信号：不可取消的令牌返回一个永不完结的任务（不建定时器）。</summary>
        private static Task WaitForCallerCancellationAsync(CancellationToken cancellationToken)
            => cancellationToken.CanBeCanceled
                ? Task.Delay(Timeout.Infinite, cancellationToken)
                : NeverCompletes;

        /// <summary>不可取消令牌的占位任务（静态共享，永不完结）。</summary>
        private static readonly Task NeverCompletes =
            new TaskCompletionSource<bool>().Task;

        private void RecordSuccess()
        {
            lock (_sync)
            {
                _consecutiveFailures = 0;
            }
        }

        private void RecordFailure(string callTarget, Exception exception, Exception? detail = null)
        {
            bool tripped = false;
            string reason = string.Empty;
            lock (_sync)
            {
                _consecutiveFailures++;
                if (!_circuitOpen && _consecutiveFailures >= _failureThreshold)
                {
                    _circuitOpen = true;
                    tripped = true;
                    reason = $"插件 {_pluginId} 的调用面 {callTarget} 连续失败 {_consecutiveFailures} 次："
                        + $"{exception.GetType().Name}：{exception.Message}";
                }
            }

            _logSink.Write(PluginLogEntry.FromException(
                _pluginId,
                tripped ? PluginLogLevel.Critical : PluginLogLevel.Error,
                tripped ? CircuitBreakLogMessage : FailureLogMessage,
                detail ?? exception));

            if (tripped)
            {
                Quarantine(reason);
            }
        }

        /// <summary>熔断即隔离：已终态（例如卸载已收口）时幂等忽略，不打断调用方。</summary>
        private void Quarantine(string reason)
        {
            try
            {
                _lifecycle.Quarantine(reason);
            }
            catch (InvalidOperationException)
            {
                // 状态机已处于终态：隔离已达成的语义相同，保持幂等。
            }
        }
    }
}
