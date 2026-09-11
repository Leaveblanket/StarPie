using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Services;
using StarPie.Kernel.Localization;

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
        // 后台/静默模式（--background，e2e 用）：窗口离屏 + 不可激活 + 无任务栏项，
        // 且不建托盘、不启全局鼠标钩子——用户同机工作时无可见/可感知打扰。
        private readonly bool _background;
        // 主题调色板换入经 Ui 侧适配器（实现内核主题应用端口）执行；
        // 宿主只负责装配，不做直接键覆盖。
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
            AppHostDelegates hostDelegates,
            bool background = false)
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
            _background = background;

            // 主题画刷换入经端口回填：整项替换合并字典的活动主题槽；
            // 主题服务不接触视图资源，只经端口触发换入。
            themeService.AttachApplier(_paletteManager);

            // 回填宿主回调：模块注册器装配页面 VM 时持转发委托，此刻起托盘气泡与
            // 退出动作指向本宿主实例。
            _hostDelegates.ShowTrayBalloonTip = ShowTrayBalloonTip;
            _hostDelegates.ExitApplication = ExitApplication;

            // 后台模式回填到对话框服务：提示框不呈现、确认框取"是"（见 DialogService）。
            _dialogService.SetBackgroundMode(background);
        }

        /// <summary>启动鼠标钩子、换入语言字典、创建托盘与主框架并显示——顺序显式可控。</summary>
        public void Run()
        {
            // 后台模式不启全局鼠标钩子：钩子属产品交互，e2e 不覆盖它，
            // 却可能在用户操作鼠标时把轮盘弹到屏幕上。
            if (!_background)
            {
                _mouseHook.Start();
            }

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
            if (_background)
            {
                ConfigureBackgroundWindow(_mainView);
            }
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
            // 后台模式不建托盘：通知区图标对同机用户可见，属"打扰"。
            if (!_background)
            {
                _trayIcon = new TrayIconManager(
                    windowsInDarkModeProbe: () => _themeService.IsWindowsInDarkTheme(),
                    onDoubleClick: () => NavigateAndShow(NavigationSlot.Trigger),
                    menuProvider: BuildTrayMenuEntries);
                _trayIcon.SetTooltip(CurrentTooltip());
            }

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

        // ==== 后台/静默模式（--background，e2e 用）====

        /// <summary>离屏定位坐标：远离所有显示器的固定点（窗口仍真实存在、仍可被 UIA 驱动）。</summary>
        private const int BackgroundCoordinate = -32000;
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;

        /// <summary>
        /// 把设置控制台窗口切成"后台形态"：不可激活（WS_EX_NOACTIVATE）、不进任务栏、
        /// 离屏定位。语义只影响窗口呈现与激活，不影响导航/配置/渲染，UIA 仍可完整驱动。
        /// </summary>
        private static void ConfigureBackgroundWindow(MainView view)
        {
            view.ShowActivated = false;
            view.ShowInTaskbar = false;
            view.WindowStartupLocation = WindowStartupLocation.Manual;
            view.Left = BackgroundCoordinate;
            view.Top = BackgroundCoordinate;

            // HWND 在 Show 时创建：SourceInitialized 早于窗口出现在屏幕上，此刻挂扩展样式最稳。
            view.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(view).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                int exStyle = GetWindowLong(hwnd, GwlExStyle);
                SetWindowLong(hwnd, GwlExStyle, exStyle | WsExNoActivate);
            };
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
