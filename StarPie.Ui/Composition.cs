using System;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services;
using StarPie.Kernel.Localization;

namespace StarPie
{
    /// <summary>
    /// 组合根：容器装配与解析集中在本类——构造函数注册全部服务、页面 ViewModel 与
    /// 导航件后调用 <c>BuildServiceProvider</c>；解析点只出现在组合根（含
    /// <see cref="CreateAppHost"/>）。
    /// </summary>
    /// <remarks>
    /// 运行与退出编排在 <see cref="AppHost"/>，本类不持有托盘/主窗口/语言字典等宿主状态；
    /// 装配顺序（钩子先启 → 配置加载 → 建窗）由 AppHost.Run 保持，配置加载由
    /// App.OnStartup 在本组合根创建后驱动。
    /// 生命周期：服务与页面 ViewModel 为单例（状态跨导航常驻）；页面 View 瞬态，
    /// 由 DataTemplate 无参构造实例化、不经容器。测试不经容器（直接 new + mock）。
    /// </remarks>
    internal sealed class Composition : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly JsonConfigService _config;
        // 宿主回调委托包（共享内核契约）：页面 VM 不直接引用 AppHost 状态，经转发委托在
        // 调用时读取宿主构造后回填的托盘气泡/退出回调；以单例注册进容器供模块注册器工厂解析。
        private readonly AppHostDelegates _hostDelegates = new();

        /// <summary>供消费方使用的配置服务；应用层经它启动时加载、退出时保存。</summary>
        internal IConfigService Config => _config;

        public Composition()
        {
            // 跨程序集环境参数回填缝：共享内核的 dev 目录分支依赖宿主 DevInstance——
            // 共享内核不能反向引用宿主，故装配前由组合根回填（.lnk 图标提取的解析契约
            // 自 ADR-0019/#87 起经 DI 注册的 IShortcutTargetResolver 注入，不再静态回填）。
            AppDataPaths.IsDevInstance = DevInstance.IsActive;

            // 组合根容器装配：注册集中在 ConfigureServices，解析点只在本类。
            var services = new ServiceCollection();

            // 导航目录由 Gestures/Shell 模块注册器与宿主注册器（外观聚合页留宿主）按固定顺序
            // 装配——页面类型不再出现在导航装配/解析清单；Validate 在 BuildServiceProvider
            // 前收口四个槽位完整，供 CreateAppHost 目录驱动 eager 解析与导航 VM/导航执行消费。
            var navigationCatalog = new NavigationCatalog();
            GesturesModuleRegistrar.RegisterNavigation(navigationCatalog);
            ShellModuleRegistrar.RegisterNavigation(navigationCatalog);
            HostModuleRegistrar.RegisterNavigation(navigationCatalog);
            navigationCatalog.Validate();
            services.AddSingleton(navigationCatalog);

            // 轮盘与渲染的 DI 注册由 WheelModuleRegistrar 下放本集（组合根仍唯一
            // BuildServiceProvider）：手势侧只经 SDK 的 IWheelFactory 接口消费轮盘；
            // 预览 Profile 契约 IProfilePreviewSource 随实现方下沉、#112 收口入 SDK
            //（ADR-0023/#97），别名由 GesturesModuleRegistrar 注册。
            WheelModuleRegistrar.RegisterServices(services);

            ConfigureServices(services);
            _provider = services.BuildServiceProvider();

            _config = _provider.GetRequiredService<JsonConfigService>();
        }

        /// <summary>解析全部宿主依赖并创建 <see cref="AppHost"/>；解析点仍集中在本组合根。</summary>
        internal AppHost CreateAppHost(bool background = false)
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

            // 页面 VM eager 解析清单目录化：遍历导航目录槽位，解析全部注册的页面 VM
            // （VM 构造即订阅导入广播/落盘消息；时机在配置加载后、AppHost.Run 前）。
            // 外观聚合解析时经工厂构造两个设置子 VM；新增页面注册进目录后自动纳入
            // eager 解析，组合根不再逐个硬编码页面类型。
            foreach (NavigationPageRegistration entry in navigationCatalog.Entries)
            {
                _ = _provider.GetRequiredService(entry.ViewModelType);
            }

            // 宿主直持的页面 VM（非目录解析清单的一部分）：初始主题取界面主题子 VM
            // （外观聚合已构造，此处取回单例）与托盘/驻留气泡直调的通用 VM
            // （由 ShellModuleRegistrar 注册，此处仅取回单例）。
            var interfaceTheme = _provider.GetRequiredService<InterfaceThemeSettingsViewModel>();
            var general = _provider.GetRequiredService<GeneralSettingsViewModel>();
            var mainViewModel = _provider.GetRequiredService<MainViewModel>();
            // 壳层 VM 独立注册/解析——AppHost 退出链与主框架分区 DataContext 指向壳层 VM；
            // 导航 VM 只持导航状态。
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
                _hostDelegates,
                background);
        }

        /// <summary>容器注册表：全部单例。需要宿主能力的委托（托盘气泡、退出）经共享内核的
        /// <see cref="AppHostDelegates"/> 延迟指向 AppHost；各模块服务与页面 VM 由模块注册器
        /// 下放程序集，宿主页面 VM（外观聚合页壳）等无宿主状态副作用项仍由组合根接线。</summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // 宿主回调委托包以单例注册进容器，供模块注册器的 VM 工厂经 ServiceProvider
            // 惰性解析；AppHost 构造后回填。
            services.AddSingleton(_hostDelegates);

            // 程序扫描与 .lnk 解析：实现驻宿主内核（StarPie.Host/Programs），
            // 契约（IProgramScanner/IShortcutTargetResolver）在 StarPie.Sdk。
            services.AddSingleton<IShortcutTargetResolver, ShortcutResolver>();
            services.AddSingleton<IProgramScanner, ProgramScanner>();
            // 图标资产：自定义图标目录（宿主内核 CustomIconStore）之上由 Ui 侧
            // IconAssetService 做 WPF 图像构造，实现 Sdk.Wpf 的 IIconAssetService 契约。
            services.AddSingleton<CustomIconStore>();
            services.AddSingleton<IIconAssetService>(sp => new IconAssetService(
                sp.GetRequiredService<CustomIconStore>(),
                sp.GetRequiredService<IShortcutTargetResolver>()));

            // 主题服务与界面主题设置子 VM 由 ThemeModuleRegistrar 注册（组合根仍唯一
            // BuildServiceProvider；调色板换入面由 AppHost 装配）。
            ThemeModuleRegistrar.RegisterServices(services);

            services.AddSingleton(sp => new JsonConfigService(
                Path.Combine(AppDataPaths.GetAppDataFolder(), "config.json"),
                sp.GetRequiredService<ILocalizationService>()));
            services.AddSingleton<IConfigService>(sp => sp.GetRequiredService<JsonConfigService>());
            services.AddSingleton<ILocalizationService, LocalizationService>();
            // S6 对话框实现的 DI 注册由 DialogsModuleRegistrar 下放本集
            // （ADR-0020/#88）：扫描能力经 SDK 契约 IProgramScanner 注入（实现由组合根注册），
            // 组合根不再直接装配对话框服务。
            DialogsModuleRegistrar.RegisterServices(services);
            services.AddSingleton<ISaveDebouncer, DispatcherSaveDebouncer>();

            // 消息总线（WeakReferenceMessenger 实例注入，便于测试替换）与落盘编排订阅者。
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<SettingsSaveOrchestrator>();

            // 导航件（ADR-0021/#92 起为 Host 内部件，共享内核仅留目录/槽位契约）：
            // NavigationStore 单例 + 目录执行缝按槽位注册。
            services.AddSingleton<NavigationStore>();
            services.AddSingleton<INavigationExecutor, NavigationExecutor>();

            // 页面 VM：容器单例，状态跨导航常驻；解析时机在配置加载后（CreateAppHost）。
            // 高级页的注册已由 ShellModuleRegistrar 下放本集。
            ShellModuleRegistrar.RegisterServices(services);

            // 手势与动作的 DI 注册（鼠标钩子/动作执行/窗口上下文/手势引擎与控制器、
            // 触发与手势两页 VM、IProfilePreviewSource 别名）由 GesturesModuleRegistrar
            // 下放本集 M1 部件。
            GesturesModuleRegistrar.RegisterServices(services);

            // 两个设置子 VM（界面主题、轮盘外观）分别由 Theme/Wheel 模块注册器注册，
            // 均经外观聚合 VM 构造注入，解析随 AppearanceSettingsViewModel 同步触发。
            services.AddSingleton(sp => new AppearanceSettingsViewModel(
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<InterfaceThemeSettingsViewModel>(),
                sp.GetRequiredService<WheelAppearanceSettingsViewModel>(),
                sp.GetRequiredService<IIconAssetService>()));

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ShellViewModel>();
        }

        public void Dispose()
        {
            // 容器随组合根释放；托盘/钩子/壳层 VM 由 AppHost.Dispose 先行释放。
            _provider.Dispose();
        }
    }
}
