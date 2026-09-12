using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using StarPie.Events;
using StarPie.PluginHosting.Cleanup;
using StarPie.PluginHosting.Verification;
using StarPie.PluginRuntime.Ui;

namespace StarPie.PluginHosting
{
    /// <summary>
    /// UI 托管门面：为每个插件创建 <see cref="PluginUiHost"/>，并把宿主侧的释放请求封送到 UI 线程，
    /// 执行有序清理编排（登记表出账 → 全局根扫描）。
    /// </summary>
    /// <remarks>
    /// 实现 <see cref="IPluginUiCoordinator"/>（Host 的 WPF-free 端口）：调用方可以从任意线程发起，
    /// 清理本身在 UI 线程内完成且不因取消中断。清理未收敛时保留托管上下文与残留，等下一次安全点
    /// 再收，不谎报成功；未注册过 UI 资产的插件直接返回成功。
    /// </remarks>
    public sealed class PluginUiCoordinator : IPluginUiCoordinator
    {
        private readonly Application _application;
        private readonly WpfUiDispatcher _dispatcher;
        private readonly IPluginEvents? _events;
        private readonly PluginUiAssetRegistry _assets = new();
        private readonly PluginUiCleanup _cleanup;
        private readonly Dictionary<string, PluginUiHost> _hosts = new(StringComparer.Ordinal);

        /// <summary>构造门面。</summary>
        /// <param name="application">宿主应用实例（资源与窗口的全局根）。</param>
        /// <param name="dispatcher">宿主 UI 线程调度器。</param>
        /// <param name="events">宿主事件中介；为 null 时 UI 订阅注册不可用。</param>
        public PluginUiCoordinator(
            Application application,
            Dispatcher dispatcher,
            IPluginEvents? events = null)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _dispatcher = new WpfUiDispatcher(dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)));
            _events = events;
            _cleanup = new PluginUiCleanup(_assets, new PluginUiLeakVerifier(application));
        }

        /// <summary>全部插件的 UI 资产登记表（诊断入口）。</summary>
        public PluginUiAssetRegistry Assets => _assets;

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
                host = new PluginUiHost(pluginId, _application.Resources, _assets, _dispatcher, _events);
                _hosts[pluginId] = host;
            }

            return host;
        }

        /// <summary>该插件当前是否已有 UI 托管上下文（清理未收敛时保留）。</summary>
        /// <param name="pluginId">插件 id。</param>
        public bool HasHost(string pluginId) => _hosts.ContainsKey(pluginId);

        /// <inheritdoc/>
        public async Task<PluginUiReleaseResult> ReleaseAsync(
            string pluginId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            cancellationToken.ThrowIfCancellationRequested();

            return await _dispatcher
                .InvokeAsync(() => ReleaseCore(pluginId))
                .ConfigureAwait(false);
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
