using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinPieGestures.Services.Localization;

namespace WinPieGestures
{
    /// <summary>
    /// Composition root (ADR-0005/0011)：容器装配与解析集中在本类——构造函数里
    /// <c>ServiceCollection</c> 注册全部服务、页面 ViewModel 与导航件后 <c>BuildServiceProvider</c>；
    /// 解析点只出现在组合根（含 <see cref="CreateAppHost"/>）。运行与退出编排已移出到
    /// <see cref="AppHost"/>（ADR-0011），本类不再持有托盘/主窗口/语言字典等宿主状态。
    /// 装配顺序（ADR-0003：钩子先启 → 配置 Load → 建窗）由 AppHost.Run 保持；
    /// 配置 Load 仍由 App.OnStartup 在本组合根创建后驱动。
    /// 生命周期（T19）：服务与页面 ViewModel 单例——状态跨导航常驻；页面 View 瞬态，
    /// 由 DataTemplate 无参构造实例化、不经容器。测试不经容器（直接 new + mock，
    /// ADR-0002 保留判据）。
    /// </summary>
    internal sealed class Composition : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly JsonConfigService _config;
        // ADR-0011：宿主回调委托包——页面 VM 注册不直接引用 AppHost 状态，
        // 而是经转发委托在调用时读取 AppHost 构造后回填的托盘气泡/退出回调。
        private readonly AppHostDelegates _hostDelegates = new();

        /// <summary>The config service handed to gesture-side consumers; the app
        /// layer drives Load on startup and Save on exit through it.</summary>
        internal IConfigService Config => _config;

        public Composition()
        {
            // T18/T19（ADR-0005）：组合根容器装配——注册集中在 ConfigureServices，解析点只在本类。
            var services = new ServiceCollection();
            ConfigureServices(services);
            _provider = services.BuildServiceProvider();

            // ADR-0013/#44：本地化服务单例注册到静态门面——先于任何配置 Load/页面 VM，
            // 保证 JsonConfigService.Load() 的 I18n.SetLanguage 与声明式投影都走同一实例。
            I18n.Initialize(_provider.GetRequiredService<ILocalizationService>());

            _config = _provider.GetRequiredService<JsonConfigService>();
        }

        /// <summary>解析全部宿主依赖并创建 <see cref="AppHost"/>；解析点仍集中在本组合根。</summary>
        internal AppHost CreateAppHost()
        {
            var messenger = _provider.GetRequiredService<IMessenger>();
            var mouseHook = _provider.GetRequiredService<MouseHook>();
            var dialogService = _provider.GetRequiredService<DialogService>();
            var themeService = _provider.GetRequiredService<ThemeService>();
            var localization = _provider.GetRequiredService<ILocalizationService>();
            var saveOrchestrator = _provider.GetRequiredService<SettingsSaveOrchestrator>();

            var navTrigger = _provider.GetRequiredService<INavigationService<BehaviorSettingsViewModel>>();
            var navAppearance = _provider.GetRequiredService<INavigationService<AppearanceSettingsViewModel>>();
            var navGestures = _provider.GetRequiredService<INavigationService<ProfileListViewModel>>();
            var navAdvanced = _provider.GetRequiredService<INavigationService<GeneralSettingsViewModel>>();
            var navAbout = _provider.GetRequiredService<INavigationService<AboutViewModel>>();

            // 手势控制器需在钩子启动前实例化并保持订阅（构造即接线鼠标事件）。
            _ = _provider.GetRequiredService<GestureController>();

            // 页面 VM 解析点（组合根）：VM 构造即订阅导入广播与落盘消息；时机在
            // Config.Load 之后、AppHost.Run 之前，与迁移前根 VM 构造语义等价。
            _ = _provider.GetRequiredService<BehaviorSettingsViewModel>();
            _ = _provider.GetRequiredService<ProfileListViewModel>();
            var appearance = _provider.GetRequiredService<AppearanceSettingsViewModel>();
            var general = _provider.GetRequiredService<GeneralSettingsViewModel>();
            _ = _provider.GetRequiredService<AboutViewModel>();
            var mainViewModel = _provider.GetRequiredService<MainViewModel>();

            return new AppHost(
                messenger,
                mouseHook,
                dialogService,
                themeService,
                localization,
                saveOrchestrator,
                navTrigger,
                navAppearance,
                navGestures,
                navAdvanced,
                navAbout,
                appearance,
                general,
                mainViewModel,
                _hostDelegates);
        }

        /// <summary>容器注册表 (T19, ADR-0005/0011)：全部单例。运行态配置服务以具体类注册
        /// （Import/Export 留在具体实现）；页面 VM 经工厂注册——需要宿主能力的委托（托盘气泡、
        /// 退出）经 <see cref="AppHostDelegates"/> 延迟指向 AppHost；自启注册表、导入前冲刷等
        /// 无宿主状态副作用仍由组合根接线。</summary>
        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new JsonConfigService(Path.Combine(AppDataPaths.GetAppDataFolder(), "config.json")));
            services.AddSingleton<IConfigService>(sp => sp.GetRequiredService<JsonConfigService>());
            services.AddSingleton<MouseHook>();
            services.AddSingleton(new ThemeService());
            services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddSingleton<IActionExecutorService, ActionExecutorService>();
            services.AddSingleton<IWindowContext, WindowContext>();
            services.AddSingleton<IWheelFactory, WheelFactory>();
            services.AddSingleton<GestureEngine>();
            services.AddSingleton<DialogService>();
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
            services.AddSingleton<GestureController>();
            services.AddSingleton<ISaveDebouncer, DispatcherSaveDebouncer>();

            // T19：消息总线（WeakReferenceMessenger，实例注入便于测试替换）与落盘编排订阅者。
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<SettingsSaveOrchestrator>();

            // T19：导航件——NavigationStore 单例 + 泛型导航服务开放泛型注册。
            services.AddSingleton<NavigationStore>();
            services.AddSingleton(typeof(INavigationService<>), typeof(NavigationService<>));

            // 页面 VM（T19）：容器单例，状态跨导航常驻。注意解析时机在 Config.Load 之后（CreateAppHost）。
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
                // 宿主回调经委托包转发（ADR-0011）：AppHost 构造后回填，VM 不反向依赖宿主类。
                (title, text) => _hostDelegates.ShowTrayBalloonTip?.Invoke(title, text),
                () => _hostDelegates.ExitApplication?.Invoke(),
                isAutoStartEnabled: AutostartRegistry.IsAutoStartEnabled,
                setAutoStart: AutostartRegistry.SetAutoStart,
                exportConfig: path => sp.GetRequiredService<JsonConfigService>().Export(path),
                importConfig: path =>
                {
                    // Spec 冲刷时机"导入前"：先冲刷挂起的防抖，再替换运行态配置。
                    sp.GetRequiredService<SettingsSaveOrchestrator>().FlushPendingSave();
                    return sp.GetRequiredService<JsonConfigService>().Import(path);
                },
                currentConfig: () => sp.GetRequiredService<IConfigService>().Current,
                messenger: sp.GetRequiredService<IMessenger>(),
                isAdministrator: IsRunningAsAdministrator));
            services.AddSingleton<AboutViewModel>(sp => new AboutViewModel(
                sp.GetRequiredService<IDialogService>(),
                () =>
                {
                    string changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
                    if (!File.Exists(changelogPath)) return false;
                    Process.Start(new ProcessStartInfo(changelogPath) { UseShellExecute = true });
                    return true;
                }));

            services.AddSingleton<MainViewModel>();
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            // T18/ADR-0011：容器随组合根释放；托盘/钩子/壳层 VM 由 AppHost.Dispose 先行释放。
            _provider.Dispose();
        }
    }

    /// <summary>宿主回调委托包（ADR-0011）：GeneralSettingsViewModel 注册所需的托盘气泡/退出
    /// 回调由 AppHost 构造后回填；VM 持稳定转发委托，调用时读取当前回调。</summary>
    internal sealed class AppHostDelegates
    {
        public Action<string, string>? ShowTrayBalloonTip { get; set; }
        public Action? ExitApplication { get; set; }
    }
}
