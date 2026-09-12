using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace StarPie.PluginHosting.Views
{
    /// <summary>
    /// 视图托管：把插件视图挂进宿主容器并记账（容器 ↔ 视图 ↔ plugin id），
    /// 摘除时清容器 <c>Content</c>、清 <c>DataContext</c> 与全部绑定。
    /// </summary>
    internal sealed class PluginViewHost
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;

        internal PluginViewHost(PluginUiAssetRegistry assets, string pluginId)
        {
            _assets = assets;
            _pluginId = pluginId;
        }

        /// <summary>把视图挂进宿主容器并登记；返回摘除句柄。</summary>
        internal IDisposable Attach(ContentControl container, FrameworkElement view)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(view);

            container.Content = view;
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.View,
                    $"视图 {view.GetType().Name}",
                    () =>
                    {
                        if (ReferenceEquals(container.Content, view))
                        {
                            container.Content = null;
                        }

                        view.DataContext = null;
                        BindingOperations.ClearAllBindings(view);
                        return true;
                    }));
        }
    }
}
