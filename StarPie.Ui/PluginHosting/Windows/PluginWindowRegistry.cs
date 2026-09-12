using System;
using System.Collections.Generic;
using System.Windows;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Windows
{
    /// <summary>
    /// 窗口托管：登记插件窗口描述符（纯数据）并保留其工厂，宿主按窗口键创建实例、显示并记账；
    /// 摘除 = 关窗（该窗口未取消关闭）、清 Owner/DataContext，描述符随注册项出账。
    /// </summary>
    /// <remarks>
    /// 插件只注册工厂，创建与显示时机一律由宿主决定；描述符保留到注册项出账为止，
    /// 插件经 <see cref="PluginUiHost.ShowWindow"/> 请求打开自己注册的窗口时才算数。
    /// </remarks>
    internal sealed class PluginWindowRegistry
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;
        private readonly Dictionary<string, PluginWindowDescriptor> _descriptors =
            new(StringComparer.Ordinal);

        internal PluginWindowRegistry(PluginUiAssetRegistry assets, string pluginId)
        {
            _assets = assets;
            _pluginId = pluginId;
        }

        /// <summary>登记窗口入口（工厂，不创建实例）。</summary>
        internal IDisposable Register(PluginWindowDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (string.IsNullOrWhiteSpace(descriptor.WindowKey))
            {
                throw new ArgumentException("窗口键不能为空", nameof(descriptor));
            }
            if (descriptor.WindowFactory is null)
            {
                throw new ArgumentException("窗口工厂不能为空", nameof(descriptor));
            }
            if (_descriptors.ContainsKey(descriptor.WindowKey))
            {
                throw new ArgumentException($"窗口键重复注册：{descriptor.WindowKey}", nameof(descriptor));
            }

            _descriptors[descriptor.WindowKey] = descriptor;
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Window,
                    $"窗口注册 {descriptor.WindowKey}",
                    () =>
                    {
                        _descriptors.Remove(descriptor.WindowKey);
                        return true;
                    }));
        }

        /// <summary>
        /// 宿主按窗口键创建、显示并记账一个插件窗口；返回关闭句柄（Dispose 即关窗并出账）。
        /// </summary>
        /// <param name="windowKey">已注册的窗口键。</param>
        /// <exception cref="ArgumentException">
        /// 窗口键为空，或该键未注册（插件只能打开自己注册过的窗口）。
        /// </exception>
        internal IDisposable Show(string windowKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(windowKey);
            if (!_descriptors.TryGetValue(windowKey, out PluginWindowDescriptor? descriptor))
            {
                throw new ArgumentException($"窗口未注册：{windowKey}", nameof(windowKey));
            }

            Window window = Create(descriptor);
            window.Show();
            return new PluginWindowHandle(window);
        }

        /// <summary>宿主创建窗口：调用工厂、登记实例并返回（是否显示由调用方决定）。</summary>
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
                            // 关闭等待由宿主编排：Closed 落地即"关窗未取消且已收口"——窗口移出
                            // Application 窗口集、宿主强引用清空。视觉树 Unloaded 的收尾在调度器上
                            // 异步完成，IsLoaded 会在那一帧之后才翻假，不能作为摘除判定信号。
                            bool closed = false;
                            window.Closed += HandleClosed;
                            try
                            {
                                window.Close();
                                return closed;
                            }
                            finally
                            {
                                window.Closed -= HandleClosed;
                            }

                            void HandleClosed(object? sender, EventArgs args) => closed = true;
                        }

                        return true;
                    }));
        }

        /// <summary>窗口句柄：Dispose 即关窗；实例的资产登记项仍留在登记表里由卸载链统一出账。</summary>
        private sealed class PluginWindowHandle : IDisposable
        {
            private Window? _window;

            internal PluginWindowHandle(Window window) => _window = window;

            public void Dispose()
            {
                if (_window is not { } window)
                {
                    return;
                }

                _window = null;
                if (window.IsLoaded || window.IsVisible)
                {
                    window.Close();
                }
            }
        }
    }
}
