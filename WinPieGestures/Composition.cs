using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinPieGestures.Services;
using WinPieGestures.ViewModels;
using WinPieGestures.Views.Navigation;

namespace WinPieGestures
{
    /// <summary>
    /// Composition root (ADR-0005)：容器装配与解析集中在本类——构造函数里
    /// <c>ServiceCollection</c> 注册全部服务、页面 ViewModel 与导航件后 <c>BuildServiceProvider</c>，
    /// 解析点只出现在组合根（含 <see cref="Run"/>）。装配顺序（ADR-0003：钩子先启 → 配置 Load →
    /// 建窗）不变。生命周期（T19）：服务与页面 ViewModel 单例——状态跨导航常驻；页面 View 瞬态，
    /// 由 DataTemplate 无参构造实例化、不经容器。测试不经容器（直接 new + mock，ADR-0002 保留判据）。
    /// Also owns the app-level exit coordination (ADR-0003): tray exit disposes the tray and
    /// shuts the application down, while closing the settings window only hides it to the tray.
    /// </summary>
    internal sealed class Composition : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly JsonConfigService _config;
        private readonly IMessenger _messenger;
        private readonly MouseHook _mouseHook;
        // DialogService (T06, ADR-0002/0004)：Owner 惰性回填——先建服务、后建主框架，
        // Run() 里窗口创建完成后回填引用，化解"服务要 Owner ↔ 窗口要服务"的循环。
        private readonly DialogService _dialogService;
        private readonly IActionExecutorService _actionExecutor;
        // Kept alive for its hook-event subscriptions; the hook roots the controller.
        private readonly GestureController? _gestureController;
        private readonly IThemeService _themeService;
        // T19：落盘编排订阅者与类型化导航服务（托盘直达改类型化导航，ShowSettings(int) 已删）。
        private readonly SettingsSaveOrchestrator _saveOrchestrator;
        private readonly INavigationService<BehaviorSettingsViewModel> _navTrigger;
        private readonly INavigationService<AppearanceSettingsViewModel> _navAppearance;
        private readonly INavigationService<ProfileListViewModel> _navGestures;
        private readonly INavigationService<GeneralSettingsViewModel> _navAdvanced;
        private readonly INavigationService<AboutViewModel> _navAbout;

        private TrayIconManager? _trayIcon;
        private MainView? _mainView;
        // 通用分区 VM：托盘提权重启与托盘驻留气泡由组合根直调/订阅（Spec：托盘气泡/提权由组合根订阅或直调）。
        private GeneralSettingsViewModel? _general;

        /// <summary>True while an app-level exit is in flight; the settings window
        /// consults it to close for real instead of hiding to the tray.</summary>
        public static bool IsExiting { get; private set; }

        /// <summary>The config service handed to gesture-side consumers; the app
        /// layer drives Load on startup and Save on exit through it.</summary>
        internal IConfigService Config => _config;

        public Composition()
        {
            // T18/T19（ADR-0005）：组合根容器装配——注册集中在 ConfigureServices，解析点只在本类。
            var services = new ServiceCollection();
            ConfigureServices(services);
            _provider = services.BuildServiceProvider();

            _config = (JsonConfigService)_provider.GetRequiredService<IConfigService>();
            _messenger = _provider.GetRequiredService<IMessenger>();
            _mouseHook = _provider.GetRequiredService<MouseHook>();
            _themeService = _provider.GetRequiredService<IThemeService>();
            _actionExecutor = _provider.GetRequiredService<IActionExecutorService>();
            _dialogService = _provider.GetRequiredService<DialogService>();
            _gestureController = _provider.GetRequiredService<GestureController>();
            _saveOrchestrator = _provider.GetRequiredService<SettingsSaveOrchestrator>();
            _navTrigger = _provider.GetRequiredService<INavigationService<BehaviorSettingsViewModel>>();
            _navAppearance = _provider.GetRequiredService<INavigationService<AppearanceSettingsViewModel>>();
            _navGestures = _provider.GetRequiredService<INavigationService<ProfileListViewModel>>();
            _navAdvanced = _provider.GetRequiredService<INavigationService<GeneralSettingsViewModel>>();
            _navAbout = _provider.GetRequiredService<INavigationService<AboutViewModel>>();
        }

        /// <summary>容器注册表 (T19, ADR-0005)：全部单例。运行态配置服务以具体类注册
        /// （Import/Export 留在具体实现）；页面 VM 经工厂注册——迁移前手动装配的宿主副作用委托
        /// （托盘气泡、退出、自启注册表、导入前冲刷）闭包仍收在组合根。</summary>
        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new JsonConfigService(Path.Combine(AppDataPaths.GetAppDataFolder(), "config.json")));
            services.AddSingleton<IConfigService>(sp => sp.GetRequiredService<JsonConfigService>());
            services.AddSingleton<MouseHook>();
            services.AddSingleton(new ThemeService());
            services.AddSingleton<IActionExecutorService, ActionExecutorService>();
            services.AddSingleton<IWindowContext, WindowContext>();
            services.AddSingleton<IWheelFactory, WheelFactory>();
            services.AddSingleton<GestureEngine>();
            services.AddSingleton<DialogService>();
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
            services.AddSingleton<GestureController>();
            services.AddSingleton<ISaveDebouncer, DispatcherSaveDebouncer>();

            // T19：消息总线（WeakReferenceMessenger，实例注入便于测试替换）与落盘编排订阅者。
            services.AddSingleton<IMessenger>(new WeakReferenceMessenger());
            services.AddSingleton<SettingsSaveOrchestrator>();

            // T19：导航件——NavigationStore 单例 + 泛型导航服务开放泛型注册。
            services.AddSingleton<NavigationStore>();
            services.AddSingleton(typeof(INavigationService<>), typeof(NavigationService<>));

            // 页面 VM（T19）：容器单例，状态跨导航常驻。注意解析时机在 Config.Load 之后（Run）。
            services.AddSingleton(sp => new BehaviorSettingsViewModel(
                sp.GetRequiredService<IConfigService>().Current,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton(sp => new ProfileListViewModel(
                sp.GetRequiredService<IConfigService>().Current.Profiles,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IActionExecutorService>()));
            services.AddSingleton(sp => new AppearanceSettingsViewModel(
                sp.GetRequiredService<IConfigService>(),
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<ProfileListViewModel>()));
            services.AddSingleton(sp => new GeneralSettingsViewModel(
                sp.GetRequiredService<IConfigService>().Current,
                sp.GetRequiredService<IDialogService>(),
                (title, text) => _trayIcon?.ShowBalloonTip(title, text),
                ExitApplication,
                isAutoStartEnabled: AutostartRegistry.IsAutoStartEnabled,
                setAutoStart: AutostartRegistry.SetAutoStart,
                exportConfig: path => sp.GetRequiredService<JsonConfigService>().Export(path),
                importConfig: path =>
                {
                    // Spec 冲刷时机"导入前"：先冲刷挂起的防抖，再替换运行态配置。
                    _saveOrchestrator.FlushPendingSave();
                    return sp.GetRequiredService<JsonConfigService>().Import(path);
                },
                currentConfig: () => sp.GetRequiredService<IConfigService>().Current,
                messenger: sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<AboutViewModel>();

            services.AddSingleton<MainViewModel>();
        }

        /// <summary>Starts the mouse hook, resolves the page ViewModels (import broadcast
        /// subscriptions live from here, as Root did before), creates the tray and the
        /// main view and shows the latter — assembly order under explicit control.</summary>
        public void Run()
        {
            _mouseHook.Start();

            // 页面 VM 解析点（组合根）：VM 构造即订阅导入广播与落盘消息，时机与迁移前根 VM 构造等价。
            _ = _provider.GetRequiredService<BehaviorSettingsViewModel>();
            _ = _provider.GetRequiredService<ProfileListViewModel>();
            _ = _provider.GetRequiredService<AppearanceSettingsViewModel>();
            _general = _provider.GetRequiredService<GeneralSettingsViewModel>();
            _ = _provider.GetRequiredService<AboutViewModel>();

            // 托盘驻留气泡：组合根订阅消息后直调通用 VM（文案与编排仍在 VM）。
            _messenger.Register<MinimizedToTrayMessage>(this, (_, _) => _general?.NotifyMinimizedToTray());

            var mainViewModel = _provider.GetRequiredService<MainViewModel>();

            // 初始页：触发与场景（迁移前 NavTab0 默认选中）。
            _navTrigger.Navigate();

            _mainView = new MainView(mainViewModel, _themeService, _messenger);
            _mainView.ApplyAppTheme(_provider.GetRequiredService<AppearanceSettingsViewModel>().AppTheme);
            // 惰性回填 Owner：此后所有模态对话框归属主框架。
            _dialogService.SetOwner(_mainView);

            _trayIcon = new TrayIconManager(
                _themeService,
                onDoubleClick: () => NavigateAndShow(_navTrigger),
                menuProvider: BuildTrayMenuEntries);
            _trayIcon.SetTooltip(DefaultTooltip);

            _mainView.Show();
        }

        public void Dispose()
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            _mouseHook.Stop();
            // T18：容器随组合根释放（单例未持非托管资源，语义与手动形态一致）。
            _provider.Dispose();
        }

        /// <summary>类型化导航 + 窗口激活（托盘直达；淡入淡出在 <see cref="MainView.ShowAndActivate"/>）。</summary>
        private void NavigateAndShow(INavigationService navigation)
        {
            navigation.Navigate();
            _mainView?.ShowAndActivate();
        }

        private List<TrayMenuEntry> BuildTrayMenuEntries()
        {
            var entries = new List<TrayMenuEntry>
            {
                TrayMenuEntry.Header("StarPie v1.4.1" + DevInstance.Suffix),
                TrayMenuEntry.Separator()
            };

            string pauseText = _mouseHook.IsPaused ? I18n.T("TrayResume") : I18n.T("TrayPause");
            entries.Add(TrayMenuEntry.Item(pauseText, TogglePauseGestures));
            // T19：托盘四项直达改类型化导航（原 ShowSettings(0/1/2/4) 的页面映射保持不变）。
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayPreferences"), () => NavigateAndShow(_navTrigger)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayAppearance"), () => NavigateAndShow(_navAppearance)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayGestures"), () => NavigateAndShow(_navGestures)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayAbout"), () => NavigateAndShow(_navAbout)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayElevate"), () => _general?.ElevateAndRestart()));
            entries.Add(TrayMenuEntry.Separator());
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayExit"), ExitApplication));

            return entries;
        }

        private void TogglePauseGestures()
        {
            _mouseHook.IsPaused = !_mouseHook.IsPaused;
            _trayIcon?.SetTooltip(_mouseHook.IsPaused
                ? $"StarPie ({I18n.T("TrayPause")})"
                : DefaultTooltip);
        }

        private static string DefaultTooltip => I18n.T("TrayTooltip") + DevInstance.Suffix;

        private void ExitApplication()
        {
            try
            {
                // T19：退出前兜底落盘直调编排订阅者（冲刷挂起防抖 + 立即落盘）。
                _saveOrchestrator.FlushPendingSave();
            }
            catch { }

            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            IsExiting = true;
            Application.Current.Shutdown();
        }
    }
}
