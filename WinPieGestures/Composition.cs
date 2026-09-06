using System;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinPieGestures.Modules;
using WinPieGestures.Services;
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
        // ADR-0011/0016（B6/#79）：宿主回调委托包（Core 公开契约，Services/AppHostDelegates.cs）——
        // 页面 VM 注册不直接引用 AppHost 状态，而是经转发委托在调用时读取 AppHost 构造后
        // 回填的托盘气泡/退出回调；实例以单例注册进容器供模块注册器工厂解析。
        private readonly AppHostDelegates _hostDelegates = new();

        /// <summary>The config service handed to gesture-side consumers; the app
        /// layer drives Load on startup and Save on exit through it.</summary>
        internal IConfigService Config => _config;

        public Composition()
        {
            // B2/#75（Core 抽取）跨程序集回填缝：Core 的 S2 AppDataPaths dev 目录分支依赖 H1
            // DevInstance，S1 IconAssets 的 .lnk 提取依赖 M3 ShortcutResolver（B4/#77 起驻
            // StarPie.Programs）——共享内核不能反向引用宿主/业务程序集，故装配前由组合根回填
            // （方向见 assemblies.md §3）。
            AppDataPaths.IsDevInstance = DevInstance.IsActive;
            IconAssets.ResolveShortcutTarget = ShortcutResolver.ResolveShortcutTarget;

            // T18/T19（ADR-0005）：组合根容器装配——注册集中在 ConfigureServices，解析点只在本类。
            var services = new ServiceCollection();

            // B3/#76（导航自治）+ B6/#79（M5 拆集）+ B9/#82（M1 拆集）：导航目录由
            // StarPie.Gestures 的 GesturesModuleRegistrar、StarPie.Shell 的
            // ShellModuleRegistrar 与 exe 内 HostModuleRegistrar（外观聚合页留 Host）按固定
            // 顺序装配——页面类型不再出现在导航装配/解析清单；Validate 在 BuildServiceProvider
            // 前收口五槽完整，供 CreateAppHost 目录驱动 eager 解析与
            // MainViewModel/INavigationExecutor 消费。
            var navigationCatalog = new NavigationCatalog();
            GesturesModuleRegistrar.RegisterNavigation(navigationCatalog);
            ShellModuleRegistrar.RegisterNavigation(navigationCatalog);
            HostModuleRegistrar.RegisterNavigation(navigationCatalog);
            navigationCatalog.Validate();
            services.AddSingleton(navigationCatalog);

            // B8/#81：M2 轮盘与渲染的 DI 注册（轮盘工厂 IWheelFactory→WheelFactory、轮盘外观设置
            // 子 VM WheelAppearanceSettingsViewModel）由 WheelModuleRegistrar.RegisterServices
            // 下放 StarPie.Wheel（D5/ADR-0016 决策 11：工厂随 M2 收编、接口留 M2 侧；M1 手势侧
            // GestureEngine 只经 IWheelFactory 接口消费；预览 Profile 契约 IProfilePreviewSource
            // 已上提 Core——实现方 M1 ProfileListViewModel 随 B9/#82 迁入 StarPie.Gestures，
            // 别名注册由 GesturesModuleRegistrar 下放模块，组合根不再登记）。
            WheelModuleRegistrar.RegisterServices(services);

            ConfigureServices(services);
            _provider = services.BuildServiceProvider();

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
            var navigation = _provider.GetRequiredService<INavigationExecutor>();
            var navigationCatalog = _provider.GetRequiredService<NavigationCatalog>();

            // 手势控制器需在钩子启动前实例化并保持订阅（构造即接线鼠标事件）。
            _ = _provider.GetRequiredService<GestureController>();

            // B3/#76：页面 VM eager 解析清单目录化——遍历 NavigationCatalog 槽位注册解析全部页面
            // VM（VM 构造即订阅导入广播/落盘消息；时机在 Config.Load 之后、AppHost.Run 之前，
            // 与迁移前根 VM 构造语义等价）。外观聚合解析时经工厂构造两个设置子 VM；新增页面
            // 注册进目录后自动纳入 eager 解析，组合根不再逐个硬编码页面类型。
            foreach (NavigationPageRegistration entry in navigationCatalog.Entries)
            {
                _ = _provider.GetRequiredService(entry.ViewModelType);
            }

            // 宿主直持的页面 VM（非目录解析清单的一部分）：初始主题取界面主题子 VM（外观聚合已构造，
            // 此处取回单例）与托盘/驻留气泡直调的通用 VM（B6/#79 起由 ShellModuleRegistrar 注册，
            // 此处仅解析取回单例，AppHost 直调语义不变）。
            var interfaceTheme = _provider.GetRequiredService<InterfaceThemeSettingsViewModel>();
            var general = _provider.GetRequiredService<GeneralSettingsViewModel>();
            var mainViewModel = _provider.GetRequiredService<MainViewModel>();
            // B1/D3（ADR-0016 决策 7）：壳层 VM 独立注册/解析——AppHost 退出链与主框架
            // 分区 DataContext 指向壳层 VM；导航 VM 只持导航状态。
            var shellViewModel = _provider.GetRequiredService<ShellViewModel>();

            return new AppHost(
                messenger,
                mouseHook,
                dialogService,
                themeService,
                localization,
                saveOrchestrator,
                navigation,
                interfaceTheme,
                general,
                mainViewModel,
                shellViewModel,
                _hostDelegates);
        }

        /// <summary>容器注册表 (T19, ADR-0005/0011)：全部单例。运行态配置服务以具体类注册
        /// （Import/Export 留在具体实现）；需要宿主能力的委托（托盘气泡、退出）经 Core 的
        /// <see cref="AppHostDelegates"/>（B6/#79 上提）延迟指向 AppHost；M5/M1 页面 VM 与
        /// 手势管线的注册已分别由 <c>ShellModuleRegistrar.RegisterServices</c>（B6/#79）与
        /// <c>GesturesModuleRegistrar.RegisterServices</c>（B9/#82）下放模块程序集，Host
        /// 页面 VM（外观聚合页壳）等无宿主状态副作用项仍由组合根接线。</summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // B6/#79：宿主回调委托包以单例注册进容器（原 Composition internal 字段；上提 Core 后
            // 供 ShellModuleRegistrar 的 VM 工厂经 ServiceProvider 惰性解析），AppHost 构造后回填。
            services.AddSingleton(_hostDelegates);

            // B7/#80：M4 主题服务（ThemeService/IThemeService）与界面主题设置子 VM 的 DI 注册
            // 由 ThemeModuleRegistrar.RegisterServices 下放 StarPie.Theme（组合根仍唯一
            // BuildServiceProvider；ThemePaletteManager 换入面由 AppHost 装配，见 AppHost.cs）。
            ThemeModuleRegistrar.RegisterServices(services);

            services.AddSingleton(sp => new JsonConfigService(
                Path.Combine(AppDataPaths.GetAppDataFolder(), "config.json"),
                sp.GetRequiredService<ILocalizationService>()));
            services.AddSingleton<IConfigService>(sp => sp.GetRequiredService<JsonConfigService>());
            services.AddSingleton<ILocalizationService, LocalizationService>();
            // T3c/#67（R6/R7）：M3 程序扫描能力经委托注入对话框服务——S6 不再直连
            // ProgramScanner 静态内部，DialogService 只持委托并转发给程序选择器 VM。
            // B4/#77：M3 迁入 StarPie.Programs 且零 Core 依赖——S1 图标补全（IconAssets.GetIcon）
            // 由组合根在此以委托注入扫描编排，行为与迁移前一致。
            services.AddSingleton(sp => new DialogService(
                sp.GetRequiredService<IThemeService>(),
                sp.GetRequiredService<ILocalizationService>(),
                () => ProgramScanner.ScanInstalledPrograms(IconAssets.GetIcon)));
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
            services.AddSingleton<ISaveDebouncer, DispatcherSaveDebouncer>();

            // T19：消息总线（WeakReferenceMessenger，实例注入便于测试替换）与落盘编排订阅者。
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<SettingsSaveOrchestrator>();

            // T19：导航件——NavigationStore 单例 + 泛型导航服务开放泛型注册。
            services.AddSingleton<NavigationStore>();
            services.AddSingleton(typeof(INavigationService<>), typeof(NavigationService<>));
            // B3/#76：导航目录执行缝（目录驱动主入口，AppHost/MainViewModel 经槽位导航）；
            // 开放泛型 INavigationService<> 保留为类型化解析缝（NavigationServiceTests 覆盖，
            // 与目录执行缝同为已批准解析点，ADR-0016 决策 8）。
            services.AddSingleton<INavigationExecutor, NavigationExecutor>();

            // 页面 VM（T19）：容器单例，状态跨导航常驻。注意解析时机在 Config.Load 之后（CreateAppHost）。
            // B6/#79：M5（高级/关于）两页的 VM 注册已由 ShellModuleRegistrar.RegisterServices 下放
            // StarPie.Shell（首个带 DI 的模块程序集，样板见 assemblies.md §6/ADR-0016 决策 8）。
            ShellModuleRegistrar.RegisterServices(services);

            // B9/#82：M1 手势与动作的 DI 注册（MouseHook/IActionExecutorService/IWindowContext/
            // GestureEngine/GestureController 与触发+手势两页 VM、IProfilePreviewSource 别名）由
            // GesturesModuleRegistrar.RegisterServices 下放 StarPie.Gestures（最后一个业务模块
            // 程序集；B9 后除 Host 外观聚合页 VM 外不再有组合根集中注册的页面 VM）。
            GesturesModuleRegistrar.RegisterServices(services);

            // #54/#56（ADR-0014 决策 6/7）：两个设置子 VM——界面主题模块设置子 VM（B7/#80 起由
            // ThemeModuleRegistrar 注册，随 StarPie.Theme 下放）与轮盘模块外观设置子 VM
            // （B8/#81 起由 WheelModuleRegistrar 注册，随 StarPie.Wheel 下放）——均由外观聚合
            // VM 构造注入，解析随 AppearanceSettingsViewModel（CreateAppHost）同步触发。
            services.AddSingleton(sp => new AppearanceSettingsViewModel(
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<InterfaceThemeSettingsViewModel>(),
                sp.GetRequiredService<WheelAppearanceSettingsViewModel>()));

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ShellViewModel>();
        }

        public void Dispose()
        {
            // T18/ADR-0011：容器随组合根释放；托盘/钩子/壳层 VM 由 AppHost.Dispose 先行释放。
            _provider.Dispose();
        }
    }
}
