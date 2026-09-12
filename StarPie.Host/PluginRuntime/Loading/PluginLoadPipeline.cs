using System.Reflection;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.Registry;
using StarPie.PluginRuntime.Ui;

namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 插件装载管线：准入/校验 → 建 collectible ALC → 载入入口程序集并实例化入口类型 → 启动。
    /// </summary>
    /// <remarks>
    /// 每次装载尝试都新建 ALC 与状态机；拒绝路径不创建 ALC、不加载任何程序集，
    /// 重校验、装载与启动的失败都进入隔离并在结果中给出可读原因，不中断宿主启动流程。
    /// 启动前建立该插件的服务作用域（宿主服务、能力注册与句柄账本），插件只经
    /// <see cref="IPluginContext"/> 取用宿主面；活动态结果携带作用域，由卸载管线按
    /// "先释放作用域、再 Unload ALC"的顺序回收。
    /// 清单声明 ui 段时，启动成功后经 <see cref="IPluginUiCoordinator"/> 在宿主 UI 线程调用一次
    /// <c>IPluginUiModule.RegisterUi</c>：UI 注册失败与启动失败同路——先经同一端口回收半注册的
    /// 资产，再进入隔离，不把半成品留给运行期。
    /// </remarks>
    public sealed class PluginLoadPipeline
    {
        private readonly CapabilityRegistry _capabilityRegistry;
        private readonly IPluginLogSink _logSink;
        private readonly CapabilityGuardOptions _guardOptions;
        private readonly IPluginUiCoordinator? _uiCoordinator;

        /// <summary>构造装载管线。</summary>
        /// <param name="capabilityRegistry">宿主能力表（插件在 StartAsync 内经上下文注册能力）。</param>
        /// <param name="logSink">插件日志落点；缺省写 Debug。</param>
        /// <param name="guardOptions">能力守卫阈值；缺省 5 秒超时、连续 3 次失败熔断。</param>
        /// <param name="uiCoordinator">
        /// UI 托管端口（UI 插件装载期注册资产）；null 表示本宿主不托管插件 UI——此时清单声明
        /// ui 段的插件按装载失败隔离，不静默降级成"无界面插件"。
        /// </param>
        public PluginLoadPipeline(
            CapabilityRegistry capabilityRegistry,
            IPluginLogSink? logSink = null,
            CapabilityGuardOptions? guardOptions = null,
            IPluginUiCoordinator? uiCoordinator = null)
        {
            ArgumentNullException.ThrowIfNull(capabilityRegistry);
            _capabilityRegistry = capabilityRegistry;
            _logSink = logSink ?? DebugPluginLogSink.Instance;
            _guardOptions = guardOptions ?? CapabilityGuardOptions.Default;
            _uiCoordinator = uiCoordinator;
        }

        /// <summary>执行一次装载尝试。</summary>
        /// <param name="request">装载请求（清单、包目录与准入结果）。</param>
        /// <param name="cancellationToken">
        /// 传入插件启动方法的取消令牌；取消按中止处理（进入隔离，不保留半启动实例）。
        /// </param>
        /// <returns>装载结果三态之一，带生命周期机的转移记录与作用域。</returns>
        public async Task<PluginLoadResult> LoadAsync(
            PluginLoadRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var lifecycle = new PluginLifecycleStateMachine();
            string pluginId = ResolvePluginId(request);

            // 拒绝路径：准入拒绝不创建 ALC、不加载任何程序集。
            if (request.Admission.Status == PluginAdmission.Rejected)
            {
                return Reject(pluginId, lifecycle, request.Admission.Reason);
            }

            PluginLoadContext? loadContext = null;
            PluginServiceScope? scope = null;
            try
            {
                // 装载前重校验：发现到装载之间包内容可能变化，校验不通过同样走拒绝路径。
                IReadOnlyList<string> violations =
                    PluginManifestValidator.Validate(request.Manifest, request.PackageDirectory);
                if (violations.Count > 0)
                {
                    return Reject(pluginId, lifecycle, string.Join("；", violations));
                }

                lifecycle.Transition(PluginLifecycleState.Validated);
                lifecycle.Transition(PluginLifecycleState.Loading);
                string entryAssemblyPath = Path.Combine(
                    request.PackageDirectory,
                    request.Manifest.EntryAssembly!);
                loadContext = new PluginLoadContext(pluginId, entryAssemblyPath);
                Assembly entryAssembly = loadContext.LoadFromAssemblyPath(entryAssemblyPath);
                IPlugin plugin = CreateEntryInstance(entryAssembly, request.Manifest.EntryType!);

                lifecycle.Transition(PluginLifecycleState.Starting);
                scope = new PluginServiceScope(
                    request.Manifest,
                    lifecycle,
                    _capabilityRegistry,
                    _logSink,
                    _guardOptions);
                await plugin
                    .StartAsync(scope.Context, cancellationToken)
                    .ConfigureAwait(false);

                // 启动期也可能已被守卫熔断隔离（能力调用失败达到阈值）。
                if (lifecycle.Current == PluginLifecycleState.Quarantined)
                {
                    DisposeScopeQuietly(scope);
                    return Quarantined(
                        pluginId,
                        lifecycle.QuarantineReason ?? "启动期进入隔离",
                        loadContext,
                        lifecycle,
                        hasUi: request.Manifest.Ui is not null);
                }

                // UI 注册在启动成功后、活动态之前：注册期抛异常即装载失败（入口契约），
                // 半注册资产经同一端口回收，避免下一次装载叠在残留上。
                string? uiFailure = await AttachUiAsync(
                    request, pluginId, entryAssembly, lifecycle, cancellationToken).ConfigureAwait(false);
                if (uiFailure is not null)
                {
                    if (!lifecycle.IsTerminal)
                    {
                        lifecycle.Quarantine(uiFailure);
                    }

                    DisposeScopeQuietly(scope);
                    return Quarantined(
                        pluginId,
                        lifecycle.QuarantineReason ?? uiFailure,
                        loadContext,
                        lifecycle,
                        hasUi: true);
                }

                lifecycle.Transition(PluginLifecycleState.Active);
                return new PluginLoadResult(
                    pluginId,
                    PluginLoadStatus.Active,
                    null,
                    plugin,
                    loadContext,
                    lifecycle,
                    scope)
                {
                    HasUi = request.Manifest.Ui is not null,
                };
            }
            catch (Exception ex)
            {
                // 重校验、装载与启动的任意异常都收在隔离态：宿主不因单个插件失败而中断启动。
                string reason = ex is OperationCanceledException
                    ? $"装载/启动被取消：{ex.Message}"
                    : $"装载/启动失败：{ex.GetType().Name}：{ex.Message}";
                string? uiReleaseFailure = await ReleaseUiQuietlyAsync(pluginId, cancellationToken)
                    .ConfigureAwait(false);
                if (uiReleaseFailure is not null)
                {
                    reason += $"；半注册 UI 资产回收失败（{uiReleaseFailure}）";
                }

                DisposeScopeQuietly(scope);
                if (!lifecycle.IsTerminal)
                {
                    lifecycle.Quarantine(reason);
                }

                return Quarantined(
                    pluginId,
                    lifecycle.QuarantineReason ?? reason,
                    loadContext,
                    lifecycle,
                    hasUi: request.Manifest.Ui is not null);
            }
        }

        /// <summary>
        /// 装载期 UI 注册：清单无 ui 段时直接成功；声明了 ui 段而宿主不托管插件 UI 时按装载失败处理。
        /// </summary>
        /// <returns>成功返回 null，失败返回可读原因（此时已尽力回收半注册资产）。</returns>
        private async Task<string?> AttachUiAsync(
            PluginLoadRequest request,
            string pluginId,
            Assembly entryAssembly,
            PluginLifecycleStateMachine lifecycle,
            CancellationToken cancellationToken)
        {
            if (request.Manifest.Ui is not { } ui)
            {
                return null;
            }

            if (_uiCoordinator is null)
            {
                return $"宿主未托管插件 UI，无法装载界面插件（ui.entryType：{ui.EntryType}）";
            }

            PluginUiAttachResult attach;
            try
            {
                attach = await _uiCoordinator
                    .AttachAsync(
                        new PluginUiAttachRequest(pluginId, entryAssembly, ui.Sdk, ui.EntryType!),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                attach = PluginUiAttachResult.Failed(
                    $"UI 注册失败：{exception.GetType().Name}：{exception.Message}");
            }

            if (attach.Succeeded)
            {
                return null;
            }

            string reason = $"UI 注册失败：{attach.FailureReason}";
            string? releaseFailure = await ReleaseUiQuietlyAsync(pluginId, cancellationToken)
                .ConfigureAwait(false);
            if (releaseFailure is not null)
            {
                reason += $"；半注册 UI 资产回收失败（{releaseFailure}）";
            }

            return reason;
        }

        /// <summary>半注册资产的回收：失败只降为可读文本附进装载失败原因，不掩盖原始失败。</summary>
        private async Task<string?> ReleaseUiQuietlyAsync(string pluginId, CancellationToken cancellationToken)
        {
            if (_uiCoordinator is null)
            {
                return null;
            }

            try
            {
                PluginUiReleaseResult release = await _uiCoordinator
                    .ReleaseAsync(pluginId, cancellationToken)
                    .ConfigureAwait(false);
                return release.Succeeded ? null : string.Join("；", release.Residuals);
            }
            catch (Exception exception)
            {
                return $"{exception.GetType().Name}：{exception.Message}";
            }
        }

        /// <summary>失败路径的作用域释放：清理失败只记日志，不掩盖原始装载失败原因。</summary>
        private void DisposeScopeQuietly(PluginServiceScope? scope)
        {
            if (scope is null)
            {
                return;
            }

            try
            {
                scope.Dispose();
            }
            catch (Exception exception)
            {
                _logSink.Write(PluginLogEntry.FromException(
                    scope.PluginId,
                    PluginLogLevel.Warning,
                    "装载失败路径释放服务作用域时句柄清理失败",
                    exception));
            }
        }

        /// <summary>
        /// 从入口程序集解析入口类型并实例化：类型只在本次调用内存在，不写入任何宿主结构。
        /// </summary>
        private static IPlugin CreateEntryInstance(Assembly entryAssembly, string entryTypeName)
        {
            Type? entryType = entryAssembly.GetType(entryTypeName, throwOnError: false, ignoreCase: false);
            if (entryType is null)
            {
                throw new InvalidOperationException($"入口类型未找到：{entryTypeName}");
            }

            if (!typeof(IPlugin).IsAssignableFrom(entryType))
            {
                throw new InvalidOperationException($"入口类型未实现 IPlugin：{entryTypeName}");
            }

            return Activator.CreateInstance(entryType) as IPlugin
                ?? throw new InvalidOperationException($"入口类型无法实例化（需要公开无参构造函数）：{entryTypeName}");
        }

        /// <summary>隔离态装载结果：无插件对象与作用域流出（入口对象只在活动态结果携带）。</summary>
        private static PluginLoadResult Quarantined(
            string pluginId,
            string reason,
            PluginLoadContext loadContext,
            PluginLifecycleStateMachine lifecycle,
            bool hasUi)
            => new(pluginId, PluginLoadStatus.Quarantined, reason, null, loadContext, lifecycle, null)
            {
                HasUi = hasUi,
            };

        private static string ResolvePluginId(PluginLoadRequest request)
            => string.IsNullOrWhiteSpace(request.Manifest.Id)
                ? Path.GetFileName(request.PackageDirectory)
                : request.Manifest.Id!;

        private static PluginLoadResult Reject(
            string pluginId,
            PluginLifecycleStateMachine lifecycle,
            string reason)
            => new(pluginId, PluginLoadStatus.Rejected, reason, null, null, lifecycle, null);
    }
}
