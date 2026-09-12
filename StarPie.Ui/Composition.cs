using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.Services;
using StarPie.Kernel.Localization;

namespace StarPie
{
    /// <summary>
    /// 组合根：容器装配与解析集中在本类——注册期遍历内置贡献者有序清单（导航目录 +
    /// 服务/页面 ViewModel），随后调用 <c>BuildServiceProvider</c>；解析点只出现在组合根
    /// （含 <see cref="CreateAppHost"/>）。
    /// </summary>
    /// <remarks>
    /// 四个阶段在本类显式分离（**注册顺序 ≠ 解析时机**）：
    /// <list type="number">
    /// <item>早期回填：共享内核的 dev 目录分支依赖宿主 <see cref="DevInstance"/>，装配前回填；</item>
    /// <item>注册期：<see cref="BuiltInContributors.CreateAll"/> 的有序清单驱动——先写导航目录并收口，
    /// 再写容器描述符（贡献者只注册不解析）；</item>
    /// <item>容器构建：唯一 <c>BuildServiceProvider</c>；</item>
    /// <item>eager 解析：<see cref="CreateAppHost"/> 在配置加载后按目录解析全部页面 VM 与宿主直持 VM。</item>
    /// </list>
    /// 插件贡献者在装载期适配成同一 <see cref="ICompositionContributor"/> 接口追加进同一管线（P2/P3）。
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
        // 调用时读取宿主构造后回填的托盘气泡/退出回调；以单例注册进容器供贡献者工厂解析。
        private readonly AppHostDelegates _hostDelegates = new();
        // 内置贡献者有序清单：注册期唯一遍历对象，解析时机与之无关。
        private readonly IReadOnlyList<ICompositionContributor> _contributors;

        /// <summary>供消费方使用的配置服务；应用层经它启动时加载、退出时保存。</summary>
        internal IConfigService Config => _config;

        public Composition()
        {
            // 阶段 1｜早期回填（注册前）：跨程序集环境参数回填缝——共享内核的 dev 目录分支
            // 依赖宿主 DevInstance，共享内核不能反向引用宿主，故装配前由组合根回填
            //（.lnk 图标提取的解析契约自 ADR-0019/#87 起经 DI 注册的
            //  IShortcutTargetResolver 注入，不再静态回填）。
            AppDataPaths.IsDevInstance = DevInstance.IsActive;

            // 阶段 2｜注册期：有序列表驱动（贡献者只登记不解析；注册顺序 ≠ 解析时机）。
            _contributors = BuiltInContributors.CreateAll(_hostDelegates);

            var services = new ServiceCollection();

            // 2a 导航目录：各贡献者自报导航页（M1 槽位 0/2、Host 聚合页槽位 1、M5 槽位 3；
            // 其余贡献者无导航页）。Validate 在 BuildServiceProvider 前收口四个槽位完整，
            // 供 CreateAppHost 目录驱动 eager 解析与导航 VM/导航执行消费。
            var navigationCatalog = new NavigationCatalog();
            foreach (ICompositionContributor contributor in _contributors)
            {
                contributor.RegisterNavigation(navigationCatalog);
            }
            navigationCatalog.Validate();
            services.AddSingleton(navigationCatalog);

            // 2b 容器：各贡献者的服务与页面 VM 登记（组合根仍唯一 BuildServiceProvider）；
            // 跨模块消费一律经 SDK/Sdk.Wpf 契约面，贡献者之间不引用彼此的实现类型。
            foreach (ICompositionContributor contributor in _contributors)
            {
                contributor.RegisterServices(services);
            }

            // 阶段 3｜容器构建：解析点仍只在组合根。
            _provider = services.BuildServiceProvider();

            _config = _provider.GetRequiredService<JsonConfigService>();
        }

        /// <summary>解析全部宿主依赖并创建 <see cref="AppHost"/>；解析点仍集中在本组合根。</summary>
        internal AppHost CreateAppHost(bool background = false)
        {
            // 阶段 4｜eager 解析：时机在配置加载后、AppHost.Run 前，与贡献者注册顺序无关
            //（页面清单由导航目录驱动，不逐个硬编码页面类型）。
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
            // （由 ShellContributor 登记，此处仅取回单例）。
            var interfaceTheme = _provider.GetRequiredService<InterfaceThemeSettingsViewModel>();
            var general = _provider.GetRequiredService<GeneralSettingsViewModel>();
            var mainViewModel = _provider.GetRequiredService<MainViewModel>();
            // 壳层 VM 独立注册/解析——AppHost 退出链与主框架分区 DataContext 指向壳层 VM；
            // 导航 VM 只持导航状态。
            var shellViewModel = _provider.GetRequiredService<ShellViewModel>();
            // 插件启动扫描器（发现/准入/启动报告）：由 AppHost 在启动序列里驱动，不装载插件代码。
            var pluginScanner = _provider.GetRequiredService<PluginStartupScanner>();

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
                pluginScanner,
                background);
        }

        public void Dispose()
        {
            // 容器随组合根释放；托盘/钩子/壳层 VM 由 AppHost.Dispose 先行释放。
            _provider.Dispose();
        }
    }
}
