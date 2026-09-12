using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Loading;

namespace StarPie.PluginRuntime.Unloading
{
    /// <summary>
    /// 安全点卸载管线：配置落盘 → 拒绝新调用 → 能力摘除 → 在途归零 → StopAsync →
    /// 服务作用域释放 → ALC.Unload → 回收判定。
    /// </summary>
    /// <remarks>
    /// 任何一步失败都进隔离（<see cref="PluginUnloadStatus.Quarantined"/>）并附诊断，不谎报成功；
    /// 在途调用未归零时中止于危险区之前（不释放作用域、不卸载 ALC），要收口只能等重启或显式重载时
    /// 再走一次安全点。已隔离的插件可经同一管线回收资源，结果仍是隔离、不改写既有隔离原因。
    /// 回收判定对 headless 插件是硬判据：入口实例、ALC 与入口程序集的
    /// <see cref="WeakReference"/> 必须全部死亡；判定要求调用方经 <see cref="PluginUnloadRequest"/>
    /// 交出插件对象（交接即清空装载结果的强引用）。本管线只服务 headless 插件；UI 插件的卸载编排
    /// 另有一套（不判 ALC 与程序集回收，存活只记诊断），不在本管线内。
    /// </remarks>
    public sealed class PluginUnloadPipeline
    {
        private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);

        private readonly Action _flushPendingSaves;
        private readonly TimeSpan _drainTimeout;
        private readonly IPluginLogSink _logSink;

        /// <summary>构造卸载管线。</summary>
        /// <param name="flushPendingSaves">
        /// 配置落盘冲刷接缝（组合根接安全点的配置落盘编排）；必填——安全点的第一步不设"跳过"路径。
        /// </param>
        /// <param name="drainTimeout">在途调用归零的最长等待；缺省 5 秒。</param>
        /// <param name="logSink">诊断日志落点；缺省写 Debug。</param>
        public PluginUnloadPipeline(
            Action flushPendingSaves,
            TimeSpan? drainTimeout = null,
            IPluginLogSink? logSink = null)
        {
            ArgumentNullException.ThrowIfNull(flushPendingSaves);
            _flushPendingSaves = flushPendingSaves;
            _drainTimeout = drainTimeout ?? DefaultDrainTimeout;
            _logSink = logSink ?? DebugPluginLogSink.Instance;
        }

        /// <summary>执行一次安全点卸载。</summary>
        /// <param name="request">卸载请求（由装载结果交接）。</param>
        /// <param name="cancellationToken">排空等待与 StopAsync 的取消令牌；取消按卸载失败处理。</param>
        /// <returns>
        /// 卸载结果两态之一：<see cref="PluginUnloadStatus.Unloaded"/>（各步完成且回收判定通过）或
        /// <see cref="PluginUnloadStatus.Quarantined"/>（任一步失败、在途未归零，或插件已处于隔离终态）。
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// 插件既不是活动态也不是隔离终态（卸载在活动态发起；隔离终态只做资源回收，不动隔离结论）。
        /// </exception>
        public async Task<PluginUnloadResult> UnloadAsync(
            PluginUnloadRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var diagnostics = new List<string>();
            var failures = new List<string>();
            PluginLifecycleStateMachine lifecycle = request.Lifecycle;
            if (lifecycle.Current is not (PluginLifecycleState.Active or PluginLifecycleState.Quarantined))
            {
                throw new InvalidOperationException(
                    $"卸载只接受活动态插件或隔离终态的资源回收：{request.PluginId} 当前为 {lifecycle.Current}");
            }

            // 1. 配置落盘：安全点的第一个动作，确保插件读写的配置段已冲刷。
            string? flushFailure = TryRun(_flushPendingSaves);
            if (flushFailure is null)
            {
                diagnostics.Add("配置落盘：已冲刷");
            }
            else
            {
                diagnostics.Add($"配置落盘：失败（{flushFailure}）");
                failures.Add($"配置落盘失败：{flushFailure}");
            }

            // 2. 拒绝新调用 + 能力摘除 + 在途归零（危险区之前的三道门）。
            //    并发熔断可能已把状态机置为隔离：此时不再转移，但摘除与回收照做（隔离是终态，转移必抛）。
            TransitionOrSkip(lifecycle, PluginLifecycleState.Stopping, diagnostics);

            // 摘除失败不阻断回收（守卫在 Stopping 起已拒绝新调用，Dispose 还会再摘一次），但按失败记账。
            string? detachFailure = TryRun(request.Scope.DetachCapabilities);
            if (detachFailure is null)
            {
                diagnostics.Add("能力条目：已摘除");
            }
            else
            {
                diagnostics.Add($"能力条目：摘除失败（{detachFailure}）");
                failures.Add($"能力摘除失败：{detachFailure}");
            }

            string? drainFailure = await DrainAsync(request, cancellationToken).ConfigureAwait(false);
            if (drainFailure is not null)
            {
                // 在途未归零即不进危险区：不释放作用域、不卸载 ALC，直接隔离收口。
                diagnostics.Add($"{drainFailure}：中止于危险区之前");
                return QuarantineResult(request, $"卸载中止：{drainFailure}", lifecycle, diagnostics);
            }

            diagnostics.Add("在途调用：已归零");

            // 3. 插件自清理。
            string? stopFailure = await StopPluginAsync(request, cancellationToken).ConfigureAwait(false);
            if (stopFailure is null)
            {
                diagnostics.Add("StopAsync：已完成");
            }
            else
            {
                diagnostics.Add($"StopAsync：失败（{stopFailure}）");
                failures.Add($"停用失败：{stopFailure}");
            }

            // 4. 服务作用域释放（ALC 卸载的前置）。账本残留是防御性断言：Dispose 先清账本再释放句柄，
            //    结构上必为零；不为零说明 Dispose 的清账语义被改动，按失败记账而不是放过。
            int handlesBefore = request.Scope.HandleCount;
            string? scopeFailure = TryRun(() => request.Scope.Dispose());
            int handlesAfter = request.Scope.HandleCount;
            if (scopeFailure is null && handlesAfter == 0)
            {
                diagnostics.Add($"作用域：已释放（账本 {handlesBefore}→{handlesAfter}）");
            }
            else
            {
                string scopeReason = scopeFailure is null
                    ? $"释放后仍有句柄残留：{handlesAfter}"
                    : $"句柄清理失败：{scopeFailure}";
                diagnostics.Add($"作用域：释放失败（{scopeReason}）");
                failures.Add($"作用域释放失败：{scopeReason}");
            }

            // 5. 交出强引用 → ALC 卸载 → 回收判定（headless 硬判据）。
            TransitionOrSkip(lifecycle, PluginLifecycleState.Unloading, diagnostics);

            ReclaimAndJudge(request, diagnostics);
            string? residual = DescribeResidual(request);
            if (residual is not null)
            {
                failures.Add(residual);
            }

            if (failures.Count > 0)
            {
                return QuarantineResult(request, string.Join("；", failures), lifecycle, diagnostics);
            }

            if (lifecycle.Current == PluginLifecycleState.Unloading)
            {
                lifecycle.Transition(PluginLifecycleState.Unloaded);
                return new PluginUnloadResult(
                    request.PluginId,
                    PluginUnloadStatus.Unloaded,
                    null,
                    lifecycle,
                    diagnostics);
            }

            // 6. 无失败但状态机处于隔离终态（调用守卫在卸载前或卸载中途熔断）：回收已照做，
            //    如实回报隔离与既有原因，不谎报已卸载。
            return QuarantineResult(
                request,
                $"插件已隔离（{lifecycle.QuarantineReason ?? "未记录原因"}），资源已回收",
                lifecycle,
                diagnostics);
        }

        /// <summary>
        /// 在途归零等待：成功返回 null；未归零、等待被取消或等待自身异常都返回可读原因，
        /// 三者一律视同未归零（不进入危险区）。
        /// </summary>
        private async Task<string?> DrainAsync(
            PluginUnloadRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                bool drained = await request.Scope.Guard
                    .WaitForInFlightAsync(_drainTimeout, cancellationToken)
                    .ConfigureAwait(false);
                return drained
                    ? null
                    : $"在途调用未归零（计数 {request.Scope.Guard.InFlightCount}）";
            }
            catch (OperationCanceledException)
            {
                return $"在途调用排空等待被取消（计数 {request.Scope.Guard.InFlightCount}）";
            }
            catch (Exception exception)
            {
                return $"在途调用排空失败（{DescribeException(exception)}）";
            }
        }

        /// <summary>
        /// 调用 StopAsync：失败降为可读文本随结果带出，异常实例只经 <see cref="PluginLogEntry.FromException"/>
        /// 的字符串三段进日志，不随结果与诊断结构留存。
        /// </summary>
        private async Task<string?> StopPluginAsync(
            PluginUnloadRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await request.Plugin.StopAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
            catch (Exception exception)
            {
                WriteLog(
                    request.PluginId,
                    "插件停用失败",
                    PluginLogLevel.Error,
                    exception);
                return DescribeException(exception);
            }
        }

        /// <summary>写一条宿主诊断日志；sink 按契约线程安全且不抛异常。</summary>
        private void WriteLog(
            string pluginId,
            string message,
            PluginLogLevel level = PluginLogLevel.Error,
            Exception? exception = null)
            => _logSink.Write(exception is null
                ? PluginLogEntry.Create(pluginId, level, message)
                : PluginLogEntry.FromException(pluginId, level, message, exception));

        /// <summary>GC + ALC.Unload + 二次 GC：把插件对象、ALC 与程序集的存活情况写进诊断。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReclaimAndJudge(PluginUnloadRequest request, List<string> diagnostics)
        {
            request.ReleaseLoadedPlugin();

            CollectGarbage();
            diagnostics.Add(Describe("插件对象", request.PluginProbe));

            // Unload 必须在独立帧里做：调用帧的临时栈槽在 Debug 下会 root ALC，帧退出后再 GC 才判得准。
            UnloadLoadContext(request);

            CollectUntilDead(request.LoadContextProbe, request.EntryAssemblyProbe);
            diagnostics.Add(Describe("ALC", request.LoadContextProbe));
            diagnostics.Add(Describe("程序集", request.EntryAssemblyProbe));
        }

        /// <summary>取出 ALC 并卸载；本帧退出后进程内不再有指向该 ALC 的栈上引用。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UnloadLoadContext(PluginUnloadRequest request)
        {
            PluginLoadContext loadContext = request.TakeLoadContext();
            loadContext.Unload();
            loadContext = null!;
        }

        /// <summary>有界重试 GC 直至 ALC 与程序集被判定回收（回收发生在卸载后的 GC 轮次里）。</summary>
        private static void CollectUntilDead(WeakReference alcProbe, WeakReference assemblyProbe)
        {
            const int MaxRounds = 10;
            for (int round = 0; round < MaxRounds; round++)
            {
                CollectGarbage();
                if (!alcProbe.IsAlive && !assemblyProbe.IsAlive)
                {
                    return;
                }
            }
        }

        /// <summary>回收判定未过时的失败描述；全部死亡时返回 null。</summary>
        private static string? DescribeResidual(PluginUnloadRequest request)
        {
            string[] surviving = new[]
                {
                    (Name: "入口实例", Probe: request.PluginProbe),
                    (Name: "ALC", Probe: request.LoadContextProbe),
                    (Name: "入口程序集", Probe: request.EntryAssemblyProbe),
                }
                .Where(entry => entry.Probe.IsAlive)
                .Select(entry => entry.Name)
                .ToArray();
            return surviving.Length == 0
                ? null
                : $"回收判定未通过（仍存活：{string.Join("、", surviving)}），已隔离；请重启宿主后重试";
        }

        private static string Describe(string what, WeakReference probe)
            => probe.IsAlive ? $"{what}：仍存活（泄漏）" : $"{what}：已回收";

        /// <summary>两轮 GC：ALC 回收需要在终结器排空后再次收集。</summary>
        private static void CollectGarbage()
        {
            for (int round = 0; round < 2; round++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        /// <summary>执行动作并把异常降为可读文本；成功时返回 null。</summary>
        private static string? TryRun(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception exception)
            {
                return DescribeException(exception);
            }
        }

        /// <summary>异常的可读单行形态（类型全名 + message）；异常实例不进入结果与诊断。</summary>
        private static string DescribeException(Exception exception)
            => $"{exception.GetType().FullName}：{exception.Message}";

        /// <summary>
        /// 转移一步生命周期：状态机已被并发熔断置为终态时记诊断并跳过，卸载不因隔离抛异常——
        /// 隔离是终态，资源回收仍照常走完，最终结果如实报隔离与既有原因。
        /// </summary>
        private static void TransitionOrSkip(
            PluginLifecycleStateMachine lifecycle,
            PluginLifecycleState next,
            List<string> diagnostics)
        {
            if (lifecycle.IsTerminal)
            {
                diagnostics.Add($"生命周期：已处于终态 {lifecycle.Current}，跳过转移到 {next}");
                return;
            }

            lifecycle.Transition(next);
        }

        /// <summary>
        /// 隔离收口：记诊断与日志（幂等置隔离，并发熔断可能已先隔离），返回隔离结果。
        /// </summary>
        private PluginUnloadResult QuarantineResult(
            PluginUnloadRequest request,
            string reason,
            PluginLifecycleStateMachine lifecycle,
            List<string> diagnostics)
        {
            diagnostics.Add($"卸载未完成：{reason}");
            WriteLog(request.PluginId, $"卸载未完成：{reason}");
            Quarantine(lifecycle, reason, diagnostics);
            return new PluginUnloadResult(
                request.PluginId,
                PluginUnloadStatus.Quarantined,
                reason,
                lifecycle,
                diagnostics);
        }

        /// <summary>置隔离：终态时幂等忽略（并发熔断可能已先隔离）。</summary>
        private static void Quarantine(
            PluginLifecycleStateMachine lifecycle,
            string reason,
            List<string> diagnostics)
        {
            if (lifecycle.IsTerminal)
            {
                diagnostics.Add($"隔离：已处于终态 {lifecycle.Current}（{lifecycle.QuarantineReason}）");
                return;
            }

            lifecycle.Quarantine(reason);
        }
    }
}
