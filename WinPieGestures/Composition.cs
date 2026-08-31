using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace WinPieGestures
{
    /// <summary>
    /// Manual composition root (ADR-0002): assembles the runtime services — mouse hook,
    /// gesture controller, tray icon and the settings window — in one place, replacing
    /// the implicit StartupUri instantiation. Also owns the app-level exit coordination
    /// (ADR-0003): tray exit disposes the tray and shuts the application down, while
    /// closing the settings window only hides it to the tray.
    /// </summary>
    internal sealed class Composition : IDisposable
    {
        private readonly JsonConfigService _config;
        private readonly MouseHook _mouseHook;
        // DialogService (T06, ADR-0002/0004)：Owner 惰性回填——先建服务、后建设置窗口，
        // Run() 里窗口创建完成后回填引用，化解"服务要 Owner ↔ 窗口要服务"的循环。
        private readonly DialogService _dialogService;
        private readonly IActionExecutorService _actionExecutor;
        // Kept alive for its hook-event subscriptions; the hook roots the controller.
        private readonly GestureController? _gestureController;
        private readonly IThemeService _themeService;
        private TrayIconManager? _trayIcon;
        private SettingsWindow? _settingsWindow;
        // T17：设置根 ViewModel 上移为组合根字段——应用退出兜底落盘（FlushPendingSave）与
        // 托盘提权重启（General 分区 VM）由组合根直调，不再经窗口透传。
        private RootSettingsViewModel? _root;
        /// <summary>True while an app-level exit is in flight; the settings window
        /// consults it to close for real instead of hiding to the tray.</summary>
        public static bool IsExiting { get; private set; }

        /// <summary>The config service handed to gesture-side consumers; the app
        /// layer drives Load on startup and Save on exit through it.</summary>
        internal IConfigService Config => _config;

        public Composition()
        {
            // T16：组合根直接构造唯一配置服务实例（静态配置门面已删除）——
            // 路径解析（dev 沙箱与 legacy 目录迁移）收进 AppDataPaths，语义不变；
            // Load 仍由应用层在 Run 之前驱动（ADR-0003 装配顺序）。
            _config = new JsonConfigService(Path.Combine(AppDataPaths.GetAppDataFolder(), "config.json"));

            _mouseHook = new MouseHook();

            IThemeService themeService = new ThemeService();
            IActionExecutorService actionExecutor = new ActionExecutorService();
            IWindowContext windowContext = new WindowContext();
            IWheelFactory wheelFactory = new WheelFactory(_config, themeService);
            var engine = new GestureEngine(_config, windowContext, wheelFactory);
            _themeService = themeService;

            _dialogService = new DialogService(themeService);
            _actionExecutor = actionExecutor;
            _gestureController = new GestureController(_mouseHook, engine, actionExecutor);
        }

        /// <summary>Starts the mouse hook, creates the tray and the initial settings
        /// window and shows the latter — what StartupUri used to do implicitly, now
        /// with the assembly order under explicit control.</summary>
        public void Run()
        {
            _mouseHook.Start();

            // T16：设置根 ViewModel 装配点上移到组合根（T14 曾在窗口构造函数内装配，属迁移期
            // 过渡形态）。宿主副作用经既有委托接线：自启注册表走 AutostartRegistry，导入导出
            // 走配置服务，托盘气泡与退出复用应用层委托。T17：落盘防抖器由组合根提供
            // （DispatcherTimer 实现，UI 线程），自动保存编排住根 VM。
            _root = new RootSettingsViewModel(
                _config,
                _dialogService,
                new DispatcherSaveDebouncer(),
                () => _config.Current,
                (title, text) => _trayIcon?.ShowBalloonTip(title, text),
                ExitApplication,
                isAutoStartEnabled: AutostartRegistry.IsAutoStartEnabled,
                setAutoStart: AutostartRegistry.SetAutoStart,
                exportConfig: path => _config.Export(path),
                importConfig: path => _config.Import(path));

            _settingsWindow = new SettingsWindow(_root, _themeService, _dialogService, _actionExecutor);
            // 惰性回填 Owner：此后所有模态对话框归属设置窗口。
            _dialogService.SetOwner(_settingsWindow);

            _trayIcon = new TrayIconManager(
                _themeService,
                onDoubleClick: () => _settingsWindow.ShowSettings(0),
                menuProvider: BuildTrayMenuEntries);
            _trayIcon.SetTooltip(DefaultTooltip);

            _settingsWindow.Show();
        }

        public void Dispose()
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            _mouseHook.Stop();
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
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayPreferences"), () => _settingsWindow?.ShowSettings(0)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayAppearance"), () => _settingsWindow?.ShowSettings(1)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayGestures"), () => _settingsWindow?.ShowSettings(2)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayAbout"), () => _settingsWindow?.ShowSettings(4)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayElevate"), () => _root?.General.ElevateAndRestart()));
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
                // T17：退出前兜底落盘直调根 VM（冲刷挂起防抖 + 立即落盘，迁移前窗口透传已删）。
                _root?.FlushPendingSave();
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
