using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.PluginRuntime.Diagnostics;
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
    /// 回收判定分两档（<see cref="PluginReclaimPolicy"/>）：纯 headless 宿主硬判入口实例、ALC 与
    /// 入口程序集的 <see cref="WeakReference"/> 全部死亡；WPF 宿主降级为只硬判入口实例——
    /// 宿主框架（System.Xaml 架构上下文经 AppDomain 程序集加载事件收拢全部程序集）必然强引用插件
    /// 程序集，ALC 与程序集存活属预期，记诊断而不隔离。两档都要求调用方经
    /// <see cref="PluginUnloadRequest"/> 交出插件对象（交接即清空装载结果的强引用）。
    /// 本管线只服务 headless 插件；UI 插件的卸载编排另有一套（资产清理与泄漏扫描），不在本管线内。
    /// </remarks>
    public sealed class PluginUnloadPipeline
    {
        private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);

        private readonly Action _flushPendingSaves;
        private readonly TimeSpan _drainTimeout;
        private readonly IPluginLogSink _logSink;
        private readonly PluginReclaimPolicy _reclaimPolicy;

        /// <summary>构造卸载管线。</summary>
        /// <param name="flushPendingSaves">
        /// 配置落盘冲刷接缝（组合根接安全点的配置落盘编排）；必填——安全点的第一步不设"跳过"路径。
        /// </param>
        /// <param name="drainTimeout">在途调用归零的最长等待；缺省 5 秒。</param>
        /// <param name="logSink">诊断日志落点；缺省写 Debug。</param>
        /// <param name="reclaimPolicy">
        /// 回收判定策略；缺省硬判（纯 headless 宿主）。WPF 宿主传
        /// <see cref="PluginReclaimPolicy.Diagnostic"/>：宿主框架缓存插件程序集，ALC 与程序集存活只记诊断。
        /// </param>
        public PluginUnloadPipeline(
            Action flushPendingSaves,
            TimeSpan? drainTimeout = null,
            IPluginLogSink? logSink = null,
            PluginReclaimPolicy reclaimPolicy = PluginReclaimPolicy.Hard)
        {
            ArgumentNullException.ThrowIfNull(flushPendingSaves);
            _flushPendingSaves = flushPendingSaves;
            _drainTimeout = drainTimeout ?? DefaultDrainTimeout;
            _logSink = logSink ?? DebugPluginLogSink.Instance;
            _reclaimPolicy = reclaimPolicy;
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

            // 启动失败的隔离装载结果没有实例与服务作用域：只回收 ALC 与已加载程序集。
            // 判定用构造期算好的布尔值——卸载帧直接读实例字段会把引用留在栈槽里，
            // 回收判定时被 GC 当作 root（headless 硬判据要求插件对象全部死亡）。
            if (!request.HasScope)
            {
                return UnloadIsolatedLoadAsync(request, lifecycle, diagnostics, failures);
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
                var inFlightResiduals = new List<PluginResidual>
                {
                    new() { Kind = PluginResidualKind.InFlightCall, Detail = drainFailure },
                };
                return QuarantineResult(
                    request,
                    $"卸载中止：{drainFailure}",
                    lifecycle,
                    diagnostics,
                    reclaimed: false,
                    inFlightResiduals);
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
            bool scopeReclaimed = scopeFailure is null && handlesAfter == 0;
            PluginResidual? scopeResidual = null;
            if (scopeReclaimed)
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
                scopeResidual = new PluginResidual
                {
                    Kind = PluginResidualKind.ScopeHandle,
                    Detail = scopeReason,
                };
            }

            // 5. 交出强引用 → ALC 卸载 → 回收判定（硬判或宿主降级，见 PluginReclaimPolicy）。
            TransitionOrSkip(lifecycle, PluginLifecycleState.Unloading, diagnostics);

            ReclaimAndJudge(request, diagnostics);
            List<PluginResidual> residuals = CollectResiduals(request);
            if (scopeResidual is not null)
            {
                residuals.Insert(0, scopeResidual);
            }

            List<PluginResidual> blocking = SplitResiduals(residuals, diagnostics, out string? reclaimNote);

            string? residual = DescribeResidual(blocking);
            if (residual is not null)
            {
                failures.Add(residual);
            }

            // 资源回收成功与否独立于隔离结论：作用域释放、账本清零与阻断残留清零才算回收成功。
            // 降级策略下 ALC 与程序集残留不算阻断（宿主框架缓存程序集），请求已交出全部强引用。
            bool reclaimed = scopeReclaimed && blocking.Count == 0;

            if (failures.Count > 0)
            {
                return QuarantineResult(
                    request,
                    string.Join("；", failures),
                    lifecycle,
                    diagnostics,
                    reclaimed,
                    residuals,
                    reclaimNote);
            }

            if (lifecycle.Current == PluginLifecycleState.Unloading)
            {
                lifecycle.Transition(PluginLifecycleState.Unloaded);
                return new PluginUnloadResult(
                    request.PluginId,
                    PluginUnloadStatus.Unloaded,
                    null,
                    lifecycle,
                    diagnostics)
                {
                    Reclaimed = reclaimed,
                    // 降级判定下停止是成功的，但宿主框架缓存的程序集仍在：残留清单随结果带出，
                    // 管理面的诊断报告据此展示"重启后释放"的现场；硬判成功时这里为空清单。
                    Residuals = residuals,
                    ReclaimNote = reclaimNote,
                };
            }

            // 6. 无失败但状态机处于隔离终态（调用守卫在卸载前或卸载中途熔断）：回收已照做，
            //    如实回报隔离与既有原因，不谎报已卸载。
            return QuarantineResult(
                request,
                $"插件已隔离（{lifecycle.QuarantineReason ?? "未记录原因"}），资源已回收",
                lifecycle,
                diagnostics,
                reclaimed,
                residuals,
                reclaimNote);
        }

        /// <summary>
        /// 启动失败的隔离装载结果回收：没有实例、服务作用域与在途调用可清，
        /// 交出 ALC 并做回收判定；结论仍是隔离，不改写既有隔离原因。
        /// </summary>
        private PluginUnloadResult UnloadIsolatedLoadAsync(
            PluginUnloadRequest request,
            PluginLifecycleStateMachine lifecycle,
            List<string> diagnostics,
            List<string> failures)
        {
            // 配置落盘是安全点第一步，已由 UnloadAsync 统一执行并记账，本路径不重复落盘。
            diagnostics.Add("能力与在途：未启动成功，无作用域（跳过）");
            diagnostics.Add("StopAsync：未启动成功（跳过）");
            diagnostics.Add("作用域：未创建（跳过）");

            TransitionOrSkip(lifecycle, PluginLifecycleState.Unloading, diagnostics);

            ReclaimAndJudge(request, diagnostics);
            List<PluginResidual> residuals = CollectResiduals(request);
            List<PluginResidual> blocking = SplitResiduals(residuals, diagnostics, out string? reclaimNote);
            string? residual = DescribeResidual(blocking);
            if (residual is not null)
            {
                failures.Add(residual);
            }

            bool reclaimed = blocking.Count == 0;
            if (failures.Count > 0)
            {
                return QuarantineResult(
                    request,
                    string.Join("；", failures),
                    lifecycle,
                    diagnostics,
                    reclaimed,
                    residuals,
                    reclaimNote);
            }

            return QuarantineResult(
                request,
                $"插件已隔离（{lifecycle.QuarantineReason ?? "未记录原因"}），资源已回收",
                lifecycle,
                diagnostics,
                reclaimed,
                residuals,
                reclaimNote);
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

        /// <summary>回收判定未过时收集可定位残留；全部死亡时返回空清单。</summary>
        private static List<PluginResidual> CollectResiduals(PluginUnloadRequest request)
        {
            var residuals = new List<PluginResidual>();
            if (request.PluginProbe.IsAlive)
            {
                residuals.Add(new PluginResidual
                {
                    Kind = PluginResidualKind.PluginObject,
                    Detail = $"入口实例 {DescribeTarget(request.PluginProbe)}",
                });
            }

            if (request.LoadContextProbe.IsAlive)
            {
                residuals.Add(new PluginResidual
                {
                    Kind = PluginResidualKind.LoadContext,
                    Detail = $"ALC {DescribeTarget(request.LoadContextProbe)}",
                });
                CollectLoadContextTypes(request.LoadContextProbe, residuals);
            }

            if (request.EntryAssemblyProbe.IsAlive)
            {
                residuals.Add(new PluginResidual
                {
                    Kind = PluginResidualKind.Assembly,
                    Detail = $"入口程序集 {DescribeTarget(request.EntryAssemblyProbe)}",
                });
            }

            return residuals;
        }

        /// <summary>
        /// 按判定策略分流残留：硬判下全部残留阻断；降级下只有插件自有对象（入口实例、作用域句柄、
        /// 在途调用）阻断，宿主框架缓存的 ALC、程序集与类型只记诊断。
        /// </summary>
        private List<PluginResidual> SplitResiduals(
            IReadOnlyList<PluginResidual> residuals,
            List<string> diagnostics,
            out string? reclaimNote)
        {
            reclaimNote = null;
            var blocking = new List<PluginResidual>();
            var deferred = new List<PluginResidual>();
            foreach (PluginResidual item in residuals)
            {
                if (_reclaimPolicy == PluginReclaimPolicy.Hard || IsPluginOwned(item.Kind))
                {
                    blocking.Add(item);
                }
                else
                {
                    deferred.Add(item);
                }
            }

            if (deferred.Count > 0)
            {
                reclaimNote = $"宿主框架缓存插件程序集（{DescribeKinds(deferred)} 仍存活），重启宿主后释放";
                diagnostics.Add(
                    $"回收判定（宿主降级）：{DescribeKinds(deferred)} 仍存活（宿主框架缓存程序集），"
                    + "只记诊断；重启宿主后释放");
            }

            return blocking;
        }

        /// <summary>插件自有残留：降级策略下仍按失败记账的类别。</summary>
        private static bool IsPluginOwned(PluginResidualKind kind)
            => kind is PluginResidualKind.PluginObject
                or PluginResidualKind.ScopeHandle
                or PluginResidualKind.InFlightCall;

        /// <summary>残留清单的可读摘要；无残留时返回 null。</summary>
        private static string? DescribeResidual(IReadOnlyList<PluginResidual> residuals)
        {
            if (residuals.Count == 0)
            {
                return null;
            }

            return $"回收判定未通过（仍存活：{DescribeKinds(residuals)}），已隔离；请重启宿主后重试";
        }

        /// <summary>残留类别的去重可读清单。</summary>
        private static string DescribeKinds(IReadOnlyList<PluginResidual> residuals)
            => string.Join(
                "、",
                residuals.Select(item => item.Kind switch
                {
                    PluginResidualKind.PluginObject => "入口实例",
                    PluginResidualKind.LoadContext => "ALC",
                    PluginResidualKind.Assembly => "入口程序集",
                    PluginResidualKind.Type => "类型",
                    PluginResidualKind.ScopeHandle => "作用域句柄",
                    PluginResidualKind.InFlightCall => "在途调用",
                    _ => item.Kind.ToString(),
                }).Distinct());

        /// <summary>ALC 仍存活时枚举其程序集与类型，给出"哪个类型还在"的定位清单。</summary>
        private static void CollectLoadContextTypes(
            WeakReference loadContextProbe,
            List<PluginResidual> residuals)
        {
            if (loadContextProbe.Target is not PluginLoadContext context)
            {
                return;
            }

            // 同名上下文不止一个：历史装载残留没有被回收，重试前必须先收干净。
            int sameNameCount = AssemblyLoadContext.All.Count(item => item.Name == context.Name);
            if (sameNameCount > 1)
            {
                residuals.Add(new PluginResidual
                {
                    Kind = PluginResidualKind.LoadContext,
                    Detail = $"同名装载上下文共 {sameNameCount} 个（存在历史装载残留）",
                });
            }

            foreach (Assembly assembly in context.Assemblies)
            {
                string assemblyName = assembly.GetName().Name ?? "未命名程序集";
                residuals.Add(new PluginResidual
                {
                    Kind = PluginResidualKind.Assembly,
                    Detail = $"上下文内程序集 {assemblyName}（{assembly.Location}）",
                });

                // 同一程序集若同时进了默认 ALC，插件 ALC 永远回收不掉——这是可定位的硬证据。
                if (AppDomain.CurrentDomain.GetAssemblies().Any(loaded =>
                        !ReferenceEquals(loaded, assembly)
                        && string.Equals(loaded.GetName().Name, assemblyName, StringComparison.Ordinal)))
                {
                    residuals.Add(new PluginResidual
                    {
                        Kind = PluginResidualKind.Assembly,
                        Detail = $"程序集 {assemblyName} 同时被默认 ALC 加载（位置 {assembly.Location}）",
                    });
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type is not null).ToArray()!;
                }
                catch (Exception)
                {
                    // 类型枚举本身失败时不阻断诊断：程序集条目已足够定位。
                    continue;
                }

                foreach (Type type in types)
                {
                    residuals.Add(new PluginResidual
                    {
                        Kind = PluginResidualKind.Type,
                        Detail = $"{assemblyName}：{type.FullName}",
                    });
                }
            }
        }

        /// <summary>弱引用目标的可读定位串。</summary>
        private static string DescribeTarget(WeakReference probe)
            => probe.Target switch
            {
                PluginLoadContext context => context.Name ?? "未命名 ALC",
                Assembly assembly => assembly.GetName().FullName ?? assembly.FullName ?? "未命名程序集",
                { } target => target.GetType().FullName ?? target.GetType().Name,
                null => "已不可达",
            };

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
            List<string> diagnostics,
            bool reclaimed,
            IReadOnlyList<PluginResidual>? residuals = null,
            string? reclaimNote = null)
        {
            diagnostics.Add($"卸载未完成：{reason}");
            WriteLog(request.PluginId, $"卸载未完成：{reason}");
            Quarantine(lifecycle, reason, diagnostics);
            return new PluginUnloadResult(
                request.PluginId,
                PluginUnloadStatus.Quarantined,
                reason,
                lifecycle,
                diagnostics)
            {
                Reclaimed = reclaimed,
                Residuals = residuals ?? Array.Empty<PluginResidual>(),
                ReclaimNote = reclaimNote,
            };
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
