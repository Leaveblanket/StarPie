using System;
using System.Windows;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Windows
{
    /// <summary>
    /// 窗口托管：登记插件窗口描述符（纯数据），并在宿主创建窗口时记账实例；
    /// 摘除 = 清 Owner/DataContext，已显示的窗口关闭（关闭等待由宿主编排）。
    /// </summary>
    internal sealed class PluginWindowRegistry
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;

        internal PluginWindowRegistry(PluginUiAssetRegistry assets, string pluginId)
        {
            _assets = assets;
            _pluginId = pluginId;
        }

        /// <summary>登记窗口入口（工厂，不创建实例）。</summary>
        internal IDisposable Register(PluginWindowDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Window,
                    $"窗口注册 {descriptor.WindowKey}",
                    () => true));
        }

        /// <summary>宿主创建窗口：调用工厂、登记实例并返回。</summary>
        internal Window Create(PluginWindowDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            Window window = descriptor.WindowFactory();
            Track(window);
            return window;
        }

        /// <summary>登记已有窗口实例。</summary>
        internal IDisposable Track(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Window,
                    $"窗口 {window.GetType().Name}",
                    () =>
                    {
                        window.Owner = null;
                        window.DataContext = null;
                        if (window.IsLoaded || window.IsVisible)
                        {
                            window.Close();

                            // 插件的 Closing 处理器取消关闭时窗口仍在：如实报残留，等下一次安全点。
                            if (window.IsLoaded || window.IsVisible)
                            {
                                return false;
                            }
                        }

                        return true;
                    }));
        }
    }
}
