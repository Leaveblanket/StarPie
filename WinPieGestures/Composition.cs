using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace WinPieGestures
{
    /// <summary>
    /// Composition root (ADR-0005)：容器装配与解析集中在本类——构造函数里
    /// <c>ServiceCollection</c> 注册全部服务与根 ViewModel 后 <c>BuildServiceProvider</c>，
    /// 解析点只出现在组合根（含 <see cref="Run"/>）。取代 ADR-0002 的手动 <c>new</c> 装配，
    /// 装配仍集中在独立组合根、装配顺序（ADR-0003：钩子先启 → 配置 Load → 建窗）不变。
    /// 生命周期：服务与根 ViewModel 单例；测试不经容器（直接 new + mock，ADR-0002 保留判据）。
    /// Also owns the app-level exit coordination (ADR-0003): tray exit disposes the tray and
    /// shuts the application down, while closing the settings window only hides it to the tray.
    /// </summary>
    internal sealed class Composition : IDisposable
    {
        private readonly ServiceProvider _provider;
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
            // T18（ADR-0005）：组合根改容器装配——注册集中在 ConfigureServices，解析点
            // 只在本类；构造出解析时机与手动形态一致（根 VM 仍待 Config.Load 后在 Run 解析）。
            var services = new ServiceCollection();
            ConfigureServices(services);
            _provider = services.BuildServiceProvider();

            _config = (JsonConfigService)_provider.GetRequiredService<IConfigService>();
            _mouseHook = _provider.GetRequiredService<MouseHook>();
            _themeService = _provider.GetRequiredService<IThemeService>();
            _actionExecutor = _provider.GetRequiredService<IActionExecutorService>();
            _dialogService = _provider.GetRequiredService<DialogService>();
            _gestureController = _provider.GetRequiredService<GestureController>();
        }

        /// <summary>容器注册表（T18, ADR-0005）：全部单例；运行态配置服务以构造实例注册，
        /// 路径解析（dev 沙箱与 legacy 目录迁移）收在 AppDataPaths。宿主副作用委托
        /// （托盘气泡、退出、自启注册表）与根 VM 保留工厂注册，闭包收在组合根。</summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // 运行态配置服务以具体类注册（Import/Export 留在具体实现，未入 IConfigService 缝），
            // 接口经转发注册供手势侧与根 VM 消费——容器内同一单例。
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

            // T16/T17：根 VM 装配保持手动委托接线（宿主副作用：托盘气泡、退出、自启注册表、
            // 导入导出经配置服务）。容器只承担解析，委托闭包仍住组合根——行为与 T17 等价。
            services.AddSingleton(sp => new RootSettingsViewModel(
                sp.GetRequiredService<IConfigService>(),
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<ISaveDebouncer>(),
                () => sp.GetRequiredService<IConfigService>().Current,
                (title, text) => _trayIcon?.ShowBalloonTip(title, text),
                ExitApplication,
                isAutoStartEnabled: AutostartRegistry.IsAutoStartEnabled,
                setAutoStart: AutostartRegistry.SetAutoStart,
                exportConfig: path => sp.GetRequiredService<JsonConfigService>().Export(path),
                importConfig: path => sp.GetRequiredService<JsonConfigService>().Import(path)));
        }

        /// <summary>Starts the mouse hook, creates the tray and the initial settings
        /// window and shows the latter — what StartupUri used to do implicitly, now
        /// with the assembly order under explicit control.</summary>
        public void Run()
        {
            _mouseHook.Start();

            // T16：设置根 ViewModel 装配点上移到组合根（T14 曾在窗口构造函数内装配，属迁移期
            // 过渡形态）。根 VM 经容器解析（T18）：其构造读运行态配置，须待 Config.Load 之后。
            _root = _provider.GetRequiredService<RootSettingsViewModel>();

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
            // T18：容器随组合根释放（单例未持非托管资源，语义与手动形态一致）。
            _provider.Dispose();
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
