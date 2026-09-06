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
    /// Application host (ADR-0011)：承接原 Composition.Run 的宿主启动/退出编排——
    /// 鼠标钩子启动、语言资源字典、主框架/托盘创建、隐藏到托盘与退出协调。
    /// DI 注册与解析仍归 <see cref="Composition"/>（组合根），本类不接触 ServiceProvider。
    /// 生命周期：App.OnStartup 经 Composition.CreateAppHost 取得本对象后调用 Run；
    /// App.OnExit 先 Save 配置再 Dispose 本对象（释放托盘、停钩、退订语言、释放壳层 VM），
    /// Composition 最后释放容器。
    /// </summary>
    internal sealed class AppHost : IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly MouseHook _mouseHook;
        private readonly DialogService _dialogService;
        private readonly ThemeService _themeService;
        private readonly ILocalizationService _localization;
        private readonly SettingsSaveOrchestrator _saveOrchestrator;
        // B3/#76（导航自治）：目录执行缝按槽位导航——宿主不再持有任何页面类型。
        private readonly INavigationExecutor _navigation;
        // #54（ADR-0014 决策 6/7）：界面主题设置子 VM——壳层启动时读取 AppTheme 做初始主题应用；
        // 运行时变更经 AppThemeChangedMessage 由 MainView 订阅执行（本宿主不再直读外观聚合 VM）。
        private readonly InterfaceThemeSettingsViewModel _interfaceTheme;
        // 通用分区 VM：托盘提权重启与托盘驻留气泡由宿主直调/订阅。
        private readonly GeneralSettingsViewModel _general;
        // B1/D3：#27 起退出状态归壳层 VM（ShellViewModel），主框架 Closing 据此放行真关窗而非
        // 隐藏到托盘；MainViewModel 只持导航状态（分区 DataContext 的导航区）。
        private readonly MainViewModel _mainViewModel;
        private readonly ShellViewModel _shellViewModel;
        private readonly AppHostDelegates _hostDelegates;
        // ADR-0013/#46 + B7/#80：主题调色板换入下沉到 ThemePaletteManager（随 M4 迁
        // StarPie.Theme 并裁决 public——Host 装配面，B6/#79 TrayIconManager 先例；
        // 整项替换活动主题槽）。AppHost 只编排（Attach 回调），不再实现直接键覆盖。
        private readonly ThemePaletteManager _paletteManager = new();
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

            // ADR-0013/#46 + B7/#80：主题画刷换入归 StarPie.Theme 的 ThemePaletteManager
            // （跨程序集 public 装配面；整项替换 MergedDictionaries 主题槽；ThemeService 仍
            // 不接触 Views 资源，只经回调触发换入）。
            themeService.AttachPaletteApplier(effectiveTheme => _paletteManager.Apply(effectiveTheme, Application.Current!));

            // 回填宿主回调（B6/#79：AppHostDelegates 上提 Core 后经容器单例解析）：M5 注册器
            // 装配 GeneralSettingsViewModel 时持的是转发委托，此刻起托盘气泡与退出动作
            // 指向本宿主实例（ADR-0011/0016）。
            _hostDelegates.ShowTrayBalloonTip = ShowTrayBalloonTip;
            _hostDelegates.ExitApplication = ExitApplication;
        }

        /// <summary>启动鼠标钩子、换入语言字典、创建托盘与主框架并显示——顺序显式可控。</summary>
        public void Run()
        {
            _mouseHook.Start();

            // T24/ADR-0013：语言资源字典换入——页面 XAML DynamicResource 的运行时数据源。
            // 订阅与首次应用先于任何页面 View 创建（语言切换经服务 LanguageChanged 同步重建，换入不累积）。
            _localization.LanguageChanged += ApplyLanguageDictionary;
            // T25（ADR-0010 壳外文案）：语言切换按当前暂停态即时刷新托盘 tooltip；
            // 托盘菜单每次打开经 menuProvider 重建，无需在此刷新。
            _localization.LanguageChanged += RefreshTrayTooltip;
            ApplyLanguageDictionary();

            // 托盘驻留气泡：宿主订阅消息后直调通用 VM（文案与编排仍在 VM）。
            _messenger.Register<MinimizedToTrayMessage>(this, (_, _) => _general?.NotifyMinimizedToTray());

            // 初始页：触发与场景槽位（迁移前 NavTab0 默认选中；目录执行缝按槽位解析）。
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
            // ADR-0013/#48：初始主题就绪后监听 Windows 深浅色变化（System 模式自动跟随）。
            _themeService.EnableSystemThemeTracking();
            // 惰性回填 Owner：此后所有模态对话框归属主框架。
            _dialogService.SetOwner(_mainView);

            // B6/#79：TrayIconManager 随 M5 迁入 StarPie.Shell（M5 → Core 单向）；托盘菜单
            // 深色配色原直读 M4 IThemeService（B7/#80 起随 M4 驻 StarPie.Theme），此处由宿主
            // 以委托注入深色探针，Shell 不反向引用 Host/M4（与 M3 图标委托同模式）。
            _trayIcon = new TrayIconManager(
                windowsInDarkModeProbe: () => _themeService.IsWindowsInDarkTheme(),
                onDoubleClick: () => NavigateAndShow(NavigationSlot.Trigger),
                menuProvider: BuildTrayMenuEntries);
            _trayIcon.SetTooltip(CurrentTooltip());

            _mainView.Show();
        }

        public void Dispose()
        {
            // T24/ADR-0013：成对退订语言字典换入（订阅在 Run()），防事件在宿主释放后仍持有引用。
            _localization.LanguageChanged -= ApplyLanguageDictionary;
            _localization.LanguageChanged -= RefreshTrayTooltip;
            _trayIcon?.Dispose();
            _trayIcon = null;
            _mouseHook.Stop();

            // T25（ADR-0010 第 3 条）：进程级 VM 成对退订 I18n 静态事件（容器 dispose 亦覆盖，此处显式保证顺序）。
            _mainViewModel.Dispose();
            _shellViewModel.Dispose();
        }

        // T24/ADR-0013：运行时语言字典——resx 数据源（ILocalizationService）的 XAML 投影，只持当前语言一份；
        // 原地 Clear 重建（replace 语义），不向 MergedDictionaries 累积旧语言。
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
                // T25（ADR-0010 壳外文案）：托盘 Header 为品牌/版本名（StarPie v1.4.1 + Dev 标记），锁死不翻译。
                TrayMenuEntry.Header("StarPie v1.4.1" + DevInstance.Suffix),
                TrayMenuEntry.Separator()
            };

            string pauseText = _mouseHook.IsPaused ? _localization.GetString("TrayResume") : _localization.GetString("TrayPause");
            entries.Add(TrayMenuEntry.Item(pauseText, TogglePauseGestures));
            // B3/#76：托盘四项直达改目录槽位导航（原 ShowSettings(0/1/2/4) 的页面映射保持不变）。
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayPreferences"), () => NavigateAndShow(NavigationSlot.Trigger)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayAppearance"), () => NavigateAndShow(NavigationSlot.Appearance)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayGestures"), () => NavigateAndShow(NavigationSlot.Gestures)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayAbout"), () => NavigateAndShow(NavigationSlot.About)));
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

        /// <summary>当前暂停态对应的托盘 tooltip（ADR-0010 壳外文案：语言切换由宿主按暂停态刷新）。</summary>
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
                // T19：退出前兜底落盘直调编排订阅者（冲刷挂起防抖 + 立即落盘）。
                _saveOrchestrator.FlushPendingSave();
            }
            catch { }

            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            // #27：退出状态落壳层 VM；主框架 Closing 放行真关窗（语义与旧 Composition.IsExiting 一致）。
            _shellViewModel.IsExiting = true;
            Application.Current.Shutdown();
        }
    }
}
