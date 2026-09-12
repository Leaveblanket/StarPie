using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarPie.Abstractions.Ui;
using StarPie.Events;
using StarPie.PluginHosting.Commands;
using StarPie.PluginHosting.Extensions;
using StarPie.PluginHosting.Resources;
using StarPie.PluginHosting.Timers;
using StarPie.PluginHosting.Views;
using StarPie.PluginHosting.Windows;
using StarPie.Services.Navigation;

namespace StarPie.PluginHosting
{
    /// <summary>
    /// 单插件的 UI 托管上下文：实现 <see cref="IPluginUiContext"/>，把插件的每次注册分派给
    /// 对应类别的托管件，并统一进资产登记表。
    /// </summary>
    /// <remarks>
    /// 全部注册与创建都在 UI 线程（宿主在 UI 线程调用 <see cref="IPluginUiModule.RegisterUi"/>）；
    /// 非 UI 线程调用直接抛异常，不做隐式封送——插件后台线程必须经 <see cref="IUiDispatcher"/>。
    /// <see cref="Release"/> 返回未能摘除的资产，供释放编排转成隔离诊断。
    /// </remarks>
    public sealed class PluginUiHost : IPluginUiContext
    {
        private readonly ResourceDictionary _hostResources;
        private readonly IUiDispatcher _dispatcher;
        private readonly IPluginEvents? _events;
        private readonly PluginViewHost _views;
        private readonly PluginWindowRegistry _windows;
        private readonly PluginCommandRegistry _commands;
        private readonly PluginTimerRegistry _timers;
        private readonly PluginExtensionRegistry _extensions;
        private IDisposable? _resourceRootHandle;

        internal PluginUiHost(
            string pluginId,
            ResourceDictionary hostResources,
            PluginUiAssetRegistry assets,
            IUiDispatcher dispatcher,
            IPluginEvents? events,
            NavigationCatalog? navigationCatalog = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            PluginId = pluginId;
            _hostResources = hostResources ?? throw new ArgumentNullException(nameof(hostResources));
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _events = events;

            ResourceRoot = new PluginResourceRoot(pluginId);
            _views = new PluginViewHost(assets, pluginId);
            _windows = new PluginWindowRegistry(assets, pluginId);
            _commands = new PluginCommandRegistry(assets, pluginId);
            _timers = new PluginTimerRegistry(assets, pluginId, dispatcher);
            _extensions = new PluginExtensionRegistry(assets, pluginId, navigationCatalog);
        }

        /// <inheritdoc/>
        public string PluginId { get; }

        /// <inheritdoc/>
        public IUiDispatcher Dispatcher => _dispatcher;

        /// <summary>本插件的 UI 资产登记表（宿主诊断与泄漏验证共用）。</summary>
        public PluginUiAssetRegistry Assets { get; }

        /// <summary>本插件资源根容器。</summary>
        public PluginResourceRoot ResourceRoot { get; }

        /// <summary>本插件注册的导航页（按注册顺序）。</summary>
        public IReadOnlyList<PluginPage> Pages => _extensions.Pages;

        /// <summary>本插件注册的设置区块（按注册顺序）。</summary>
        public IReadOnlyList<PluginSettingsSection> SettingsSections => _extensions.SettingsSections;

        /// <summary>本插件注册的托盘菜单项（按注册顺序）。</summary>
        public IReadOnlyList<PluginMenuItem> MenuItems => _extensions.MenuItems;

        /// <summary>执行本插件登记的指定命令；命令未登记时不动作。</summary>
        /// <param name="commandId">命令 id。</param>
        public void ExecuteCommand(string commandId)
        {
            EnsureUiThread();
            _commands.Find(commandId)?.Execute();
        }

        /// <summary>把插件资源根并入宿主资源合并表并登记；宿主在挂载插件 UI 时调用（UI 线程）。</summary>
        public void Attach()
        {
            EnsureUiThread();
            _resourceRootHandle ??= ResourceRoot.AttachTo(_hostResources, Assets);
        }

        /// <summary>把插件视图挂进宿主容器并记账（UI 线程）。</summary>
        /// <param name="container">宿主提供的容器（非 null）。</param>
        /// <param name="view">插件视图（非 null）。</param>
        public IDisposable AttachView(ContentControl container, FrameworkElement view)
        {
            EnsureUiThread();
            return _views.Attach(container, view);
        }

        /// <summary>登记宿主创建的插件窗口实例（UI 线程）。</summary>
        /// <param name="window">窗口实例（非 null）。</param>
        public IDisposable TrackWindow(Window window)
        {
            EnsureUiThread();
            return _windows.Track(window);
        }

        /// <summary>宿主创建插件窗口：调用工厂、登记实例并返回（UI 线程）。</summary>
        /// <param name="descriptor">窗口描述符（非 null）。</param>
        public Window CreateWindow(PluginWindowDescriptor descriptor)
        {
            EnsureUiThread();
            return _windows.Create(descriptor);
        }

        /// <summary>
        /// 执行本插件的资产摘除编排（UI 线程）；返回未能摘除的资产。调用方据此判定隔离，
        /// 调用后仍应经泄漏验证器做全局根扫描。
        /// </summary>
        public IReadOnlyList<PluginUiAsset> Release()
        {
            EnsureUiThread();
            return Assets.DetachAll(PluginId);
        }

        /// <inheritdoc/>
        public IDisposable RegisterPage(PluginPageDescriptor descriptor)
        {
            EnsureUiThread();
            return _extensions.RegisterPage(descriptor);
        }

        /// <inheritdoc/>
        public IDisposable RegisterSettingsSection(PluginSettingsSectionDescriptor descriptor)
        {
            EnsureUiThread();
            return _extensions.RegisterSettingsSection(descriptor);
        }

        /// <inheritdoc/>
        public IDisposable RegisterWindow(PluginWindowDescriptor descriptor)
        {
            EnsureUiThread();
            return _windows.Register(descriptor);
        }

        /// <inheritdoc/>
        public IDisposable RegisterMenuItem(PluginMenuItemDescriptor descriptor)
        {
            EnsureUiThread();
            return _extensions.RegisterMenuItem(descriptor);
        }

        /// <inheritdoc/>
        public IDisposable RegisterCommand(PluginCommandDescriptor descriptor)
        {
            EnsureUiThread();
            return _commands.Register(descriptor);
        }

        /// <inheritdoc/>
        public IDisposable MergeResourceDictionary(Uri packUri)
        {
            EnsureUiThread();
            ArgumentNullException.ThrowIfNull(packUri);
            return ResourceRoot.Merge(packUri, Assets);
        }

        /// <inheritdoc/>
        public IDisposable CreateTimer(TimeSpan interval, Action tick)
        {
            EnsureUiThread();
            return _timers.Create(interval, tick);
        }

        /// <inheritdoc/>
        public IDisposable CreateAnimation(FrameworkElement target, Storyboard storyboard)
        {
            EnsureUiThread();
            return _timers.AttachAnimation(target, storyboard);
        }

        /// <inheritdoc/>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            EnsureUiThread();
            ArgumentNullException.ThrowIfNull(handler);
            if (_events is null)
            {
                throw new InvalidOperationException(
                    $"宿主未提供事件中介，无法为 {PluginId} 注册 UI 订阅");
            }

            IDisposable subscription = _events.Subscribe(handler);
            return new PluginUiAssetHandle(
                Assets,
                Assets.Track(
                    PluginId,
                    PluginUiAssetKind.Subscription,
                    $"事件订阅 {typeof(TEvent).Name}",
                    () =>
                    {
                        subscription.Dispose();
                        return true;
                    }));
        }

        private void EnsureUiThread()
        {
            if (!_dispatcher.IsOnUiThread)
            {
                throw new InvalidOperationException(
                    $"插件 UI 注册必须在宿主 UI 线程执行：{PluginId}");
            }
        }
    }
}
