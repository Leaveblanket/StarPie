using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Services;
using StarPie.Services.Localization;

namespace StarPie
{
    /// <summary>
    /// 应用宿主：执行启动/退出编排——鼠标钩子启动、语言资源字典、主框架/托盘创建、
    /// 隐藏到托盘与退出协调。
    /// </summary>
    /// <remarks>
    /// DI 注册与解析仍归 <see cref="Composition"/>（组合根），本类不接触 ServiceProvider。
    /// 生命周期：App.OnStartup 经 <see cref="Composition.CreateAppHost"/> 取得本对象后调用
    /// Run；App.OnExit 先保存配置再释放本对象（释放托盘、停钩、退订语言、释放壳层 VM），
    /// Composition 最后释放容器。
    /// </remarks>
    internal sealed class AppHost : IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly MouseHook _mouseHook;
        private readonly DialogService _dialogService;
        private readonly ThemeService _themeService;
        private readonly ILocalizationService _localization;
        private readonly SettingsSaveOrchestrator _saveOrchestrator;
        // 目录执行缝按槽位导航——宿主不持有任何页面类型。
        private readonly INavigationExecutor _navigation;
        // 界面主题设置子 VM：壳层启动时读取 AppTheme 做初始主题应用；
        // 运行时变更经 AppThemeChangedMessage 由主框架订阅执行。
        private readonly InterfaceThemeSettingsViewModel _interfaceTheme;
        // 通用分区 VM：托盘提权重启与驻留气泡由宿主直调/订阅。
        private readonly GeneralSettingsViewModel _general;
        // 退出状态归壳层 VM：主框架 Closing 据此放行真关窗而非隐藏到托盘；
        // 导航 VM 只持导航状态（分区 DataContext 的导航区）。
        private readonly MainViewModel _mainViewModel;
        private readonly ShellViewModel _shellViewModel;
        private readonly AppHostDelegates _hostDelegates;
        // 主题调色板整项换入由 AppThemePaletteManager（public 装配面）执行；
        // 宿主只负责编排回调，不再做直接键覆盖。
        private readonly AppThemePaletteManager _paletteManager = new();
        private TrayIconManager? _trayIcon;
        private MainView? _mainView;

        public AppHost(
            IMessenger messenger,
            MouseHook mouseHook,
            DialogService dialogService,
            ThemeService themeService,
            ILocalizationService localization,
            SettingsSaveOrchestrator saveOrchestrator,
            INavigationExecutor navigation,
            InterfaceThemeSettingsViewModel interfaceTheme,
            GeneralSettingsViewModel general,
            MainViewModel mainViewModel,
            ShellViewModel shellViewModel,
            AppHostDelegates hostDelegates)
        {
            _messenger = messenger;
            _mouseHook = mouseHook;
            _dialogService = dialogService;
            _themeService = themeService;
            _localization = localization;
            _saveOrchestrator = saveOrchestrator;
            _navigation = navigation;
            _interfaceTheme = interfaceTheme;
            _general = general;
            _mainViewModel = mainViewModel;
            _shellViewModel = shellViewModel;
            _hostDelegates = hostDelegates;

            // 主题画刷换入经 AppThemePaletteManager 的 public 装配面：整项替换合并字典的
            // 活动主题槽；主题服务不接触视图资源，只经回调触发换入。
            themeService.AttachPaletteApplier(effectiveTheme => _paletteManager.Apply(effectiveTheme, Application.Current!));

            // 回填宿主回调：模块注册器装配页面 VM 时持转发委托，此刻起托盘气泡与
            // 退出动作指向本宿主实例。
            _hostDelegates.ShowTrayBalloonTip = ShowTrayBalloonTip;
            _hostDelegates.ExitApplication = ExitApplication;
        }

        /// <summary>启动鼠标钩子、换入语言字典、创建托盘与主框架并显示——顺序显式可控。</summary>
        public void Run()
        {
            _mouseHook.Start();

            // 语言资源字典换入——页面 XAML DynamicResource 的运行时数据源。
            // 订阅与首次应用先于任何页面创建（语言切换经服务事件同步重建，换入不累积）。
            _localization.LanguageChanged += ApplyLanguageDictionary;
            // 语言切换按当前暂停态即时刷新托盘 tooltip；
            // 托盘菜单每次打开时重建，无需在此刷新。
            _localization.LanguageChanged += RefreshTrayTooltip;
            ApplyLanguageDictionary();

            // 托盘驻留气泡：宿主订阅消息后直调通用 VM（文案与编排仍在 VM）。
            _messenger.Register<MinimizedToTrayMessage>(this, (_, _) => _general?.NotifyMinimizedToTray());

            // 初始页为“触发与场景”槽位。
            _navigation.Navigate(NavigationSlot.Trigger);

            _mainView = new MainView(_mainViewModel, _shellViewModel, _themeService);
            _mainView.IsVisibleChanged += (_, _) =>
            {
                if (_mainView is { IsVisible: false } && !_shellViewModel.IsExiting)
                {
                    _saveOrchestrator.FlushPendingSave();
                    MemoryOptimizer.TrimMemory();
                    _messenger.Send(MinimizedToTrayMessage.Instance);
                }
            };
            _mainView.ApplyAppTheme(_interfaceTheme.AppTheme);
            // 初始主题就绪后监听 Windows 深浅色变化（System 模式自动跟随）。
            _themeService.EnableSystemThemeTracking();
            // 惰性回填 Owner：此后所有模态对话框归属主框架。
            _dialogService.SetOwner(_mainView);

            // 托盘深色配色由宿主以委托注入深色探针，壳层模块不反向引用宿主/主题模块。
            _trayIcon = new TrayIconManager(
                windowsInDarkModeProbe: () => _themeService.IsWindowsInDarkTheme(),
                onDoubleClick: () => NavigateAndShow(NavigationSlot.Trigger),
                menuProvider: BuildTrayMenuEntries);
            _trayIcon.SetTooltip(CurrentTooltip());

            _mainView.Show();
        }

        public void Dispose()
        {
            // 成对退订语言字典换入（订阅在 Run()），避免宿主释放后事件仍持有引用。
            _localization.LanguageChanged -= ApplyLanguageDictionary;
            _localization.LanguageChanged -= RefreshTrayTooltip;
            _trayIcon?.Dispose();
            _trayIcon = null;
            _mouseHook.Stop();

            // 进程级 VM 成对退订本地化静态事件（容器释放亦覆盖，此处显式保证顺序）。
            _mainViewModel.Dispose();
            _shellViewModel.Dispose();
        }

        // 运行时语言字典：本地化服务的 XAML 投影，只持当前语言一份；
        // 原地清空重建，不向合并字典累积旧语言。
        private static readonly ResourceDictionary LanguageDictionary = new();

        /// <summary>用当前语言重建 Application 级语言字典（设置页文本 DynamicResource 的数据源）。</summary>
        private void ApplyLanguageDictionary()
        {
            if (Application.Current is not { } app)
            {
                return;
            }

            if (!app.Resources.MergedDictionaries.Contains(LanguageDictionary))
            {
                app.Resources.MergedDictionaries.Add(LanguageDictionary);
            }

            LanguageDictionary.Clear();
            foreach ((string key, string value) in _localization.EnumerateCurrentEntries())
            {
                LanguageDictionary[key] = value;
            }
        }

        /// <summary>按目录槽位导航 + 窗口激活（托盘直达；淡入淡出在 <see cref="MainView.ShowAndActivate"/>）。</summary>
        private void NavigateAndShow(NavigationSlot slot)
        {
            _navigation.Navigate(slot);
            _mainView?.ShowAndActivate();
        }

        private List<TrayMenuEntry> BuildTrayMenuEntries()
        {
            var entries = new List<TrayMenuEntry>
            {
                // 托盘 Header 为品牌/版本名（StarPie v1.4.1 + Dev 标记），不参与翻译。
                TrayMenuEntry.Header("StarPie v1.4.1" + DevInstance.Suffix),
                TrayMenuEntry.Separator()
            };

            string pauseText = _mouseHook.IsPaused ? _localization.GetString("TrayResume") : _localization.GetString("TrayPause");
            entries.Add(TrayMenuEntry.Item(pauseText, TogglePauseGestures));
            // 托盘直达项经目录槽位导航（触发/外观/手势）。
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayPreferences"), () => NavigateAndShow(NavigationSlot.Trigger)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayAppearance"), () => NavigateAndShow(NavigationSlot.Appearance)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayGestures"), () => NavigateAndShow(NavigationSlot.Gestures)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayElevate"), () => _general?.ElevateAndRestart()));
            entries.Add(TrayMenuEntry.Separator());
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayExit"), ExitApplication));

            return entries;
        }

        private void ShowTrayBalloonTip(string title, string text)
        {
            _trayIcon?.ShowBalloonTip(title, text);
        }

        private void TogglePauseGestures()
        {
            _mouseHook.IsPaused = !_mouseHook.IsPaused;
            _trayIcon?.SetTooltip(CurrentTooltip());
        }

        /// <summary>当前暂停态对应的托盘 tooltip；语言切换时由宿主按暂停态刷新。</summary>
        private string CurrentTooltip()
        {
            return _mouseHook.IsPaused ? $"StarPie ({_localization.GetString("TrayPause")})" : DefaultTooltip;
        }

        private void RefreshTrayTooltip()
        {
            if (_trayIcon != null)
            {
                _trayIcon.SetTooltip(CurrentTooltip());
            }
        }

        private string DefaultTooltip => _localization.GetString("TrayTooltip") + DevInstance.Suffix;

        private void ExitApplication()
        {
            try
            {
                // 退出前兜底落盘：直调编排订阅者冲刷挂起防抖并立即落盘。
                _saveOrchestrator.FlushPendingSave();
            }
            catch { }

            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            // 退出状态落壳层 VM，主框架 Closing 据此放行真关窗。
            _shellViewModel.IsExiting = true;
            Application.Current.Shutdown();
        }
    }
}
