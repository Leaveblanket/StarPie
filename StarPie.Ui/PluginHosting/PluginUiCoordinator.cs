using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using StarPie.Abstractions.Ui;
using StarPie.Compatibility;
using StarPie.Events;
using StarPie.PluginHosting.Cleanup;
using StarPie.PluginHosting.Extensions;
using StarPie.PluginHosting.Verification;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Ui;
using StarPie.Services.Navigation;

namespace StarPie.PluginHosting
{
    /// <summary>
    /// UI 托管门面：为每个插件创建 <see cref="PluginUiHost"/>，并把宿主侧的释放请求封送到 UI 线程，
    /// 执行有序清理编排（登记表出账 → 全局根扫描）。
    /// </summary>
    /// <remarks>
    /// 实现 <see cref="IPluginUiCoordinator"/>（Host 的 WPF-free 端口）：调用方可以从任意线程发起，
    /// 封送与实做都在 UI 线程（调用方已在 UI 线程时就地执行，避免"阻塞 UI 线程等待 UI 线程"）。
    /// 装载期在 UI 线程调用一次插件的 <see cref="IPluginUiModule.RegisterUi"/> 并把资源根并入宿主
    /// 资源树；清理未收敛时保留托管上下文与残留，等下一次安全点再收，不谎报成功；未注册过 UI 资产的
    /// 插件释放直接成功。装配层提供导航目录时，插件注册的导航页进该目录；插件缺席（无宿主上下文）时
    /// 全部扩展点为空，不产生空壳。
    /// </remarks>
    public sealed class PluginUiCoordinator : IPluginUiCoordinator
    {
        /// <summary>公开无参构造：插件 UI 入口类型的唯一合法形态（<c>ui.entryType</c> 契约）。</summary>
        private static readonly Type[] UiEntryConstructorSignature = Type.EmptyTypes;

        private readonly Application _application;
        private readonly WpfUiDispatcher _dispatcher;
        private readonly IPluginEvents? _events;
        private readonly NavigationCatalog? _navigationCatalog;
        private readonly PluginUiAssetRegistry _assets = new();
        private readonly PluginUiCleanup _cleanup;
        private readonly Dictionary<string, PluginUiHost> _hosts = new(StringComparer.Ordinal);

        /// <summary>构造门面。</summary>
        /// <param name="application">宿主应用实例（资源与窗口的全局根）。</param>
        /// <param name="dispatcher">宿主 UI 线程调度器。</param>
        /// <param name="events">宿主事件中介；为 null 时 UI 订阅注册不可用。</param>
        /// <param name="navigationCatalog">宿主导航目录；为 null 时插件导航页只登记不挂载。</param>
        public PluginUiCoordinator(
            Application application,
            Dispatcher dispatcher,
            IPluginEvents? events = null,
            NavigationCatalog? navigationCatalog = null)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _dispatcher = new WpfUiDispatcher(dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)));
            _events = events;
            _navigationCatalog = navigationCatalog;
            _cleanup = new PluginUiCleanup(_assets, new PluginUiLeakVerifier(application));
        }

        /// <summary>全部插件的 UI 资产登记表（诊断入口）。</summary>
        public PluginUiAssetRegistry Assets => _assets;

        /// <summary>全部插件注册的设置区块（按插件 id 稳定序、区块权重升序）。</summary>
        public IReadOnlyList<PluginSettingsSection> SettingsSections => _hosts.Values
            .SelectMany(host => host.SettingsSections)
            .OrderBy(section => section.PluginId, StringComparer.Ordinal)
            .ThenBy(section => section.Descriptor.Order)
            .ToList();

        /// <summary>全部插件注册的托盘菜单项（按菜单权重升序、插件 id 稳定序）。</summary>
        public IReadOnlyList<PluginMenuItem> MenuItems => _hosts.Values
            .SelectMany(host => host.MenuItems)
            .OrderBy(item => item.Descriptor.Order)
            .ThenBy(item => item.PluginId, StringComparer.Ordinal)
            .ToList();

        /// <summary>执行已登记插件命令（UI 线程）；命令不存在或插件已出账时不动作。</summary>
        /// <param name="commandId">命令 id。</param>
        /// <remarks>命令 id 在宿主菜单内全局可达，故按插件 id 稳定序取首个登记该 id 的插件。</remarks>
        public void ExecuteCommand(string commandId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
            if (!_dispatcher.IsOnUiThread)
            {
                throw new InvalidOperationException(
                    $"执行插件命令必须在 UI 线程：{commandId}");
            }

            foreach (PluginUiHost host in _hosts.Values.OrderBy(
                item => item.PluginId, StringComparer.Ordinal))
            {
                host.ExecuteCommand(commandId);
            }
        }

        /// <summary>取（必要时创建）指定插件的 UI 托管上下文；须在 UI 线程调用。</summary>
        /// <param name="pluginId">插件 id。</param>
        public PluginUiHost GetOrCreateHost(string pluginId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            if (!_dispatcher.IsOnUiThread)
            {
                throw new InvalidOperationException($"创建插件 UI 上下文必须在 UI 线程：{pluginId}");
            }

            if (!_hosts.TryGetValue(pluginId, out PluginUiHost? host))
            {
                host = new PluginUiHost(
                    pluginId, _application.Resources, _assets, _dispatcher, _events, _navigationCatalog);
                _hosts[pluginId] = host;
            }

            return host;
        }

        /// <summary>该插件当前是否已有 UI 托管上下文（清理未收敛时保留）。</summary>
        /// <param name="pluginId">插件 id。</param>
        public bool HasHost(string pluginId) => _hosts.ContainsKey(pluginId);

        /// <inheritdoc/>
        public async Task<PluginUiAttachResult> AttachAsync(
            PluginUiAttachRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            // 已在 UI 线程时就地执行：宿主启动期可能在工作线程上等待装载结果，
            // 而 UI 线程正在等这条路走完（阻塞式等待），排队封送会直接死等。
            return _dispatcher.IsOnUiThread
                ? AttachCore(request)
                : await _dispatcher.InvokeAsync(() => AttachCore(request)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<PluginUiReleaseResult> ReleaseAsync(
            string pluginId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            cancellationToken.ThrowIfCancellationRequested();

            return _dispatcher.IsOnUiThread
                ? ReleaseCore(pluginId)
                : await _dispatcher.InvokeAsync(() => ReleaseCore(pluginId)).ConfigureAwait(false);
        }

        /// <summary>
        /// UI 注册编排（UI 线程）：校验 UI 侧 ABI → 解析入口类型 → 调 <c>RegisterUi</c> → 并入资源根。
        /// </summary>
        /// <remarks>
        /// 注册前要求本插件没有未收口的托管上下文：残留没清干净就再注册会把两代资产叠在同一张登记表上，
        /// 回收判定随之失真；此时报失败让装载进隔离，由管理面走重试/停用出口。
        /// </remarks>
        private PluginUiAttachResult AttachCore(PluginUiAttachRequest request)
        {
            if (HasHost(request.PluginId) && Assets.CountFor(request.PluginId) > 0)
            {
                return PluginUiAttachResult.Failed(
                    $"上一次卸载未收敛：仍有 {Assets.CountFor(request.PluginId)} 项未摘净的 UI 资产");
            }

            if (!TryParseUiAbi(request.UiSdk, out string abiFailure))
            {
                return PluginUiAttachResult.Failed(abiFailure);
            }

            Type? entryType = request.EntryAssembly.GetType(
                request.UiEntryType,
                throwOnError: false,
                ignoreCase: false);
            if (entryType is null)
            {
                return PluginUiAttachResult.Failed($"ui.entryType 未找到：{request.UiEntryType}");
            }

            if (!typeof(IPluginUiModule).IsAssignableFrom(entryType))
            {
                return PluginUiAttachResult.Failed(
                    $"ui.entryType 未实现 IPluginUiModule：{request.UiEntryType}");
            }

            if (entryType.GetConstructor(UiEntryConstructorSignature) is null)
            {
                return PluginUiAttachResult.Failed(
                    $"ui.entryType 需要公开无参构造函数：{request.UiEntryType}");
            }

            PluginUiHost host = GetOrCreateHost(request.PluginId);
            try
            {
                var module = (IPluginUiModule)Activator.CreateInstance(entryType)!;
                module.RegisterUi(host);
            }
            catch (Exception exception)
            {
                // 注册失败即装载失败：半注册资产由上层的回收路径经同一端口清理。
                return PluginUiAttachResult.Failed(
                    $"{exception.GetType().Name}：{exception.Message}");
            }

            // 注册完成才并入资源根：插件注册的资源字典与页面/窗口模板从此可被宿主资源查找命中。
            host.Attach();
            return PluginUiAttachResult.Success;
        }

        /// <summary>
        /// UI 侧 ABI 兼容判定：清单 <c>ui.sdk</c> 按"主.次"解析后须与宿主同主、次不高于宿主。
        /// 兼容判定归 UI 托管层——宿主内核不引用 WPF 契约面，读不懂 ui 段的版本政策。
        /// </summary>
        private static bool TryParseUiAbi(string? uiSdk, out string failure)
        {
            if (!UiSdkAbi.TryParseVersion(uiSdk, out int major, out int minor))
            {
                failure = $"ui.sdk 必须是「主.次」版本号：{uiSdk}";
                return false;
            }

            if (!UiSdkAbi.IsCompatibleWithCurrentHost(major, minor))
            {
                failure = $"UI SDK ABI 不兼容：清单声明 {uiSdk}，宿主为 {UiSdkAbi.Version}";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private PluginUiReleaseResult ReleaseCore(string pluginId)
        {
            _hosts.TryGetValue(pluginId, out PluginUiHost? host);
            PluginUiReleaseResult result = _cleanup.Release(pluginId, host);
            if (result.Succeeded)
            {
                _hosts.Remove(pluginId);
            }

            // 清理未收敛：保留托管上下文与残留，等待下一次显式安全点再收（不谎报成功）。
            return result;
        }
    }
}
