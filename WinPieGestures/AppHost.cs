using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures.Services.Localization;

namespace WinPieGestures
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
        private readonly INavigationService<BehaviorSettingsViewModel> _navTrigger;
        private readonly INavigationService<AppearanceSettingsViewModel> _navAppearance;
        private readonly INavigationService<ProfileListViewModel> _navGestures;
        private readonly INavigationService<GeneralSettingsViewModel> _navAdvanced;
        private readonly INavigationService<AboutViewModel> _navAbout;
        private readonly AppearanceSettingsViewModel _appearance;
        // 通用分区 VM：托盘提权重启与托盘驻留气泡由宿主直调/订阅。
        private readonly GeneralSettingsViewModel _general;
        // #27：壳层 VM（MainViewModel）承担 App 退出状态，主框架 Closing 据此放行真关窗而非隐藏到托盘。
        private readonly MainViewModel _mainViewModel;
        private readonly AppHostDelegates _hostDelegates;
        // ADR-0013/#46：主题调色板换入下沉到 ThemePaletteManager（整项替换活动主题槽），
        // AppHost 只编排（Attach 回调），不再实现直接键覆盖。
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
            INavigationService<BehaviorSettingsViewModel> navTrigger,
            INavigationService<AppearanceSettingsViewModel> navAppearance,
            INavigationService<ProfileListViewModel> navGestures,
            INavigationService<GeneralSettingsViewModel> navAdvanced,
            INavigationService<AboutViewModel> navAbout,
            AppearanceSettingsViewModel appearance,
            GeneralSettingsViewModel general,
            MainViewModel mainViewModel,
            AppHostDelegates hostDelegates)
        {
            _messenger = messenger;
            _mouseHook = mouseHook;
            _dialogService = dialogService;
            _themeService = themeService;
            _localization = localization;
            _saveOrchestrator = saveOrchestrator;
            _navTrigger = navTrigger;
            _navAppearance = navAppearance;
            _navGestures = navGestures;
            _navAdvanced = navAdvanced;
            _navAbout = navAbout;
            _appearance = appearance;
            _general = general;
            _mainViewModel = mainViewModel;
            _hostDelegates = hostDelegates;

            // ADR-0013/#46：主题画刷换入归宿主层 ThemePaletteManager（整项替换 MergedDictionaries 主题槽；
            // ThemeService 仍不接触 Views 资源，只经回调触发换入）。
            themeService.AttachPaletteApplier(effectiveTheme => _paletteManager.Apply(effectiveTheme, Application.Current!));

            // 回填宿主回调：GeneralSettingsViewModel 注册时持的是转发委托，此刻起
            // 托盘气泡与退出动作指向本宿主实例（ADR-0011）。
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

            // 初始页：触发与场景（迁移前 NavTab0 默认选中）。
            _navTrigger.Navigate();

            _mainView = new MainView(_mainViewModel, _themeService);
            _mainView.IsVisibleChanged += (_, _) =>
            {
                if (_mainView is { IsVisible: false } && !_mainViewModel.IsExiting)
                {
                    _saveOrchestrator.FlushPendingSave();
                    MemoryOptimizer.TrimMemory();
                    _messenger.Send(MinimizedToTrayMessage.Instance);
                }
            };
            _mainView.ApplyAppTheme(_appearance.AppTheme);
            // ADR-0013/#48：初始主题就绪后监听 Windows 深浅色变化（System 模式自动跟随）。
            _themeService.EnableSystemThemeTracking();
            // 惰性回填 Owner：此后所有模态对话框归属主框架。
            _dialogService.SetOwner(_mainView);

            _trayIcon = new TrayIconManager(
                _themeService,
                onDoubleClick: () => NavigateAndShow(_navTrigger),
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
                // T25（ADR-0010 壳外文案）：托盘 Header 为品牌/版本名（StarPie v1.4.1 + Dev 标记），锁死不翻译。
                TrayMenuEntry.Header("StarPie v1.4.1" + DevInstance.Suffix),
                TrayMenuEntry.Separator()
            };

            string pauseText = _mouseHook.IsPaused ? _localization.GetString("TrayResume") : _localization.GetString("TrayPause");
            entries.Add(TrayMenuEntry.Item(pauseText, TogglePauseGestures));
            // T19：托盘四项直达改类型化导航（原 ShowSettings(0/1/2/4) 的页面映射保持不变）。
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayPreferences"), () => NavigateAndShow(_navTrigger)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayAppearance"), () => NavigateAndShow(_navAppearance)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayGestures"), () => NavigateAndShow(_navGestures)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayAbout"), () => NavigateAndShow(_navAbout)));
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
            _mainViewModel.IsExiting = true;
            Application.Current.Shutdown();
        }
    }
}
