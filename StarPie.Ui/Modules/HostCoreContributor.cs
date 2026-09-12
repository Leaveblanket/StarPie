using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Events;
using StarPie.Kernel.Localization;
using StarPie.PluginHosting;
using StarPie.PluginRuntime;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Discovery;
using StarPie.PluginRuntime.Hosting;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Registry;
using StarPie.PluginRuntime.State;
using StarPie.PluginRuntime.Unloading;
using StarPie.Programs;
using StarPie.Services;
using StarPie.Services.Navigation;
using StarPie.ViewModels.Pages;

namespace StarPie.Modules
{
    /// <summary>
    /// 宿主编排与内核接入贡献者：把宿主直持的运行时件（配置/本地化/图标资产/程序扫描/
    /// 导航运行时/壳层 VM）按贡献者接口登记，组合根不再逐行硬编码注册体。
    /// </summary>
    /// <remarks>
    /// 宿主回调委托包 <see cref="AppHostDelegates"/> 由组合根持有并在 AppHost 构造后回填
    /// （宿主状态不归贡献者，本贡献者只负责把同一实例注册为单例）；插件管理页（槽位 4）随本贡献者登记。
    /// 注册的可解析件：内核实现驻 <c>StarPie.Host</c>，WPF 适配件（<c>DispatcherSaveDebouncer</c>）
    /// 与图像构造（<c>IconAssetService</c>）驻本集，契约在 <c>StarPie.Sdk</c>/<c>StarPie.Sdk.Wpf</c>。
    /// </remarks>
    internal sealed class HostCoreContributor : ICompositionContributor
    {
        private readonly AppHostDelegates _hostDelegates;

        public HostCoreContributor(AppHostDelegates hostDelegates)
        {
            _hostDelegates = hostDelegates;
        }

        public string Id => "host.core";

        public int Order => 0;

        /// <summary>向导航目录注册插件管理页（槽位 4）。</summary>
        public void RegisterNavigation(NavigationCatalog catalog)
        {
            catalog.RegisterPage<PluginManagerViewModel>(
                NavigationSlot.Plugins,
                NavigationSlots.GetAutomationId(NavigationSlot.Plugins),
                "PagePlugins",
                IconPlugins);
        }

        public void RegisterServices(IServiceCollection services)
        {
            // 宿主回调委托包：模块的页面 VM 经它转发托盘气泡/退出，AppHost 构造后回填实现。
            services.AddSingleton(_hostDelegates);

            // 程序扫描与 .lnk 解析：实现驻宿主内核（StarPie.Host/Programs），
            // 契约（IProgramScanner/IShortcutTargetResolver）在 StarPie.Sdk。
            services.AddSingleton<IShortcutTargetResolver, ShortcutResolver>();
            // 能力表：宿主声明「程序来源」契约并登记内置来源（永远排在插件条目之前）。
            services.AddSingleton(sp =>
            {
                var registry = new CapabilityRegistry();
                registry.DeclareBuiltin(
                    ProgramSourceCapability.Contract,
                    new ProgramScanner(sp.GetRequiredService<IShortcutTargetResolver>()));
                return registry;
            });
            // 消费者面：内置来源 + 插件来源的聚合（扩展点降级——插件停用/缺席只剩内置来源）。
            services.AddSingleton<IProgramScanner, ProgramSourceAggregator>();
            // 图标资产：自定义图标目录（宿主内核 CustomIconStore）之上由 Ui 侧
            // IconAssetService 做 WPF 图像构造，实现 Sdk.Wpf 的 IIconAssetService 契约。
            services.AddSingleton<CustomIconStore>();
            services.AddSingleton<IIconAssetService>(sp => new IconAssetService(
                sp.GetRequiredService<CustomIconStore>(),
                sp.GetRequiredService<IShortcutTargetResolver>()));

            // 配置与本地化（内核实现；路径与语言服务在解析期惰性取用）。
            services.AddSingleton(sp => new JsonConfigService(
                Path.Combine(AppDataPaths.GetAppDataFolder(), "config.json"),
                sp.GetRequiredService<ILocalizationService>()));
            services.AddSingleton<IConfigService>(sp => sp.GetRequiredService<JsonConfigService>());
            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddSingleton<ISaveDebouncer, DispatcherSaveDebouncer>();

            // 消息总线（WeakReferenceMessenger 实例注入，便于测试替换）与落盘编排订阅者。
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<SettingsSaveOrchestrator>();

            // 导航运行时（ADR-0021/#92 起为 Host 内部件，共享内核仅留目录/槽位契约）：
            // NavigationStore 单例 + 目录执行缝按槽位注册。
            services.AddSingleton<NavigationStore>();
            services.AddSingleton<INavigationExecutor, NavigationExecutor>();

            // 插件运行时：路径、发现、清单校验、准入判定与宿主状态（含开发者模式开关）。
            // 启动扫描只做发现/准入与启动报告落盘，不装载插件代码；路径经 PluginPaths 单一来源，
            // 用户侧目录随 dev 实例落沙箱。
            services.AddSingleton(_ => new PluginStateStore(PluginPaths.StateFilePath));
            services.AddSingleton<IPluginReviewCatalog, EmptyPluginReviewCatalog>();
            services.AddSingleton(sp => new PluginAdmissionPolicy(
                PluginAdmissionPolicy.DefaultBuiltInPluginIds,
                sp.GetRequiredService<IPluginReviewCatalog>()));
            services.AddSingleton<PluginDeveloperModeService>();
            services.AddSingleton(_ => new PluginDiscovery(PluginPaths.InstallDirectory, PluginPaths.UserDirectory));
            services.AddSingleton(sp => new PluginStartupScanner(
                sp.GetRequiredService<PluginDiscovery>(),
                sp.GetRequiredService<PluginAdmissionPolicy>(),
                sp.GetRequiredService<PluginStateStore>(),
                new PluginStartupReportWriter(PluginPaths.StartupReportFilePath)));

            // 插件装载/卸载与宿主侧运行时：两条管线只在组合根装配一次，
            // 卸载管线的配置落盘接缝直接接共享内核的防抖落盘编排（安全点第一步）。
            // 回收判定走降级档：本进程是 WPF 宿主，System.Xaml 的 BAML 架构上下文会经
            // AppDomain 程序集加载事件收拢全部程序集并强引用，插件程序集不可能在本进程内回收；
            // 入口实例仍硬判，ALC 与程序集存活只记诊断，重启后释放。
            services.AddSingleton(sp => new PluginLoadPipeline(sp.GetRequiredService<CapabilityRegistry>()));
            services.AddSingleton(sp => new PluginUnloadPipeline(
                sp.GetRequiredService<SettingsSaveOrchestrator>().FlushPendingSave,
                reclaimPolicy: PluginReclaimPolicy.Diagnostic));
            // 「彻底移除」的三条宿主侧接缝：配置段删除走配置服务、插件数据目录走 PluginPaths、
            // 删除后立即冲刷配置落盘（挂起的防抖落盘会把旧段写回磁盘）。
            services.AddSingleton(sp => new PluginRuntimeHost(
                sp.GetRequiredService<PluginStartupScanner>(),
                sp.GetRequiredService<PluginStateStore>(),
                sp.GetRequiredService<PluginLoadPipeline>(),
                sp.GetRequiredService<PluginUnloadPipeline>(),
                new PluginUninstallOptions
                {
                    RemoveConfigSection = pluginId =>
                        sp.GetRequiredService<IConfigService>().Current.Plugins.Remove(pluginId),
                    PluginDataRoot = PluginPaths.DataDirectory,
                    FlushPendingSaves = sp.GetRequiredService<SettingsSaveOrchestrator>().FlushPendingSave,
                }));

            // 插件 UI 托管门面：宿主应用实例与 UI 调度器取自进程内唯一 Application（组合根在
            // Application 启动后解析），导航目录经构造注入——插件注册的导航页直接进目录。
            services.AddSingleton(sp => new PluginUiCoordinator(
                Application.Current,
                Application.Current.Dispatcher,
                sp.GetService<IPluginEvents>(),
                sp.GetRequiredService<NavigationCatalog>()));

            // 壳层 VM：状态跨导航常驻；解析时机在配置加载后（组合根 eager 解析阶段）。
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ShellViewModel>();

            // 插件管理页 VM：数据源是宿主报告快照，页面每次被导航到时经导航状态刷新。
            services.AddSingleton(sp => new PluginManagerViewModel(
                sp.GetRequiredService<PluginRuntimeHost>(),
                sp.GetRequiredService<NavigationStore>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<PluginUiCoordinator>(),
                sp.GetService<IDialogService>()));
        }

        // 插件槽位导航图标 Path Data（拼图）
        private const string IconPlugins =
            "M20.5,11H19V7C19,5.89 18.1,5 17,5H13V3.5A2.5,2.5 0 0,0 10.5,1A2.5,2.5 0 0,0 8,3.5V5H4A2,2 0 0,0 2,7V10.8H3.5C5,10.8 6.2,12 6.2,13.5C6.2,15 5,16.2 3.5,16.2H2V20A2,2 0 0,0 4,22H7.8V20.5C7.8,19 9,17.8 10.5,17.8C12,17.8 13.2,19 13.2,20.5V22H17A2,2 0 0,0 19,20V16H20.5A2.5,2.5 0 0,0 23,13.5A2.5,2.5 0 0,0 20.5,11Z";
    }
}
