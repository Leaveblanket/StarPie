using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Services;

namespace StarPie.Modules
{
    /// <summary>
    /// 宿主编排与内核接入贡献者：把宿主直持的运行时件（配置/本地化/图标资产/程序扫描/
    /// 导航运行时/壳层 VM）按贡献者接口登记，组合根不再逐行硬编码注册体。
    /// </summary>
    /// <remarks>
    /// 宿主回调委托包 <see cref="AppHostDelegates"/> 由组合根持有并在 AppHost 构造后回填
    /// （宿主状态不归贡献者，本贡献者只负责把同一实例注册为单例）；无导航页。
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

        public void RegisterServices(IServiceCollection services)
        {
            // 宿主回调委托包：模块的页面 VM 经它转发托盘气泡/退出，AppHost 构造后回填实现。
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

            // 壳层 VM：状态跨导航常驻；解析时机在配置加载后（组合根 eager 解析阶段）。
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ShellViewModel>();
        }
    }
}
