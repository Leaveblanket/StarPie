using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.PluginHosting;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Hosting;
using StarPie.Services;
using StarPie.Kernel.Localization;

namespace StarPie
{
    /// <summary>
    /// 组合根：容器装配与解析集中在本类——注册期遍历内置贡献者有序清单（导航目录 +
    /// 服务/页面 ViewModel），随后调用 <c>BuildServiceProvider</c>；解析点只出现在组合根
    /// （含 <see cref="CreateShellHost"/> 与它交付的设置台工厂）。
    /// </summary>
    /// <remarks>
    /// 三个阶段在本类显式分离（**注册顺序 ≠ 解析时机**）：
    /// <list type="number">
    /// <item>注册期：<see cref="BuiltInContributors.CreateAll"/> 的有序清单驱动——先写导航目录并收口，
    /// 再写容器描述符（贡献者只注册不解析）；</item>
    /// <item>容器构建：唯一 <c>BuildServiceProvider</c>；</item>
    /// <item>解析：<see cref="CreateShellHost"/> 在配置加载后解析常驻件；设置台的会话对象图
    ///（含会话作用域内的页面 VM 与设置子 VM）由组合根交付的工厂在每次开窗时解析——
    /// 页面 VM 不再启动期 eager 解析。</item>
    /// </list>
    /// 插件贡献者在装载期适配成同一 <see cref="ICompositionContributor"/> 接口追加进同一管线（P2/P3）。
    /// 运行与退出编排在 <see cref="ShellHost"/>，本类不持有托盘/主窗口/语言字典等宿主状态；
    /// 装配顺序（钩子先启 → 配置加载 → 建窗）由 ShellHost.Run 保持，配置加载由
    /// App.OnStartup 在本组合根创建后驱动。
    /// 生命周期：服务为单例（常驻）；页面 View 瞬态，由 DataTemplate 无参构造实例化、不经容器。
    /// 测试不经容器（直接 new + mock）。
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
            // 阶段 1｜注册期：有序列表驱动（贡献者只登记不解析；注册顺序 ≠ 解析时机）。
            _contributors = BuiltInContributors.CreateAll(_hostDelegates);

            var services = new ServiceCollection();

            // 1a 导航目录：各贡献者自报导航页（M1 槽位 0/2、Host 聚合页槽位 1、M5 槽位 3；
            // 其余贡献者无导航页）。Validate 在 BuildServiceProvider 前收口四个槽位完整，
            // 供 CreateAppHost 目录驱动 eager 解析与导航 VM/导航执行消费。
            var navigationCatalog = new NavigationCatalog();
            foreach (ICompositionContributor contributor in _contributors)
            {
                contributor.RegisterNavigation(navigationCatalog);
            }
            navigationCatalog.Validate();
            services.AddSingleton(navigationCatalog);

            // 1b 容器：各贡献者的服务与页面 VM 登记（组合根仍唯一 BuildServiceProvider）；
            // 跨模块消费一律经 SDK/Sdk.Wpf 契约面，贡献者之间不引用彼此的实现类型。
            foreach (ICompositionContributor contributor in _contributors)
            {
                contributor.RegisterServices(services);
            }

            // 阶段 2｜容器构建：解析点仍只在组合根。
            _provider = services.BuildServiceProvider();

            _config = _provider.GetRequiredService<JsonConfigService>();
        }

        /// <summary>解析全部常驻依赖并创建 <see cref="ShellHost"/>；解析点仍集中在本组合根。</summary>
        internal ShellHost CreateShellHost(bool background = false, bool testInstance = false)
        {
            // 阶段 3｜解析：时机在配置加载后、ShellHost.Run 前，与贡献者注册顺序无关
            //（页面清单由导航目录驱动，不逐个硬编码页面类型）。
            var messenger = _provider.GetRequiredService<IMessenger>();
            var mouseHook = _provider.GetRequiredService<MouseHook>();
            var dialogService = _provider.GetRequiredService<DialogService>();
            var themeService = _provider.GetRequiredService<ThemeService>();
            var localization = _provider.GetRequiredService<ILocalizationService>();
            var saveOrchestrator = _provider.GetRequiredService<SettingsSaveOrchestrator>();
            var navigation = _provider.GetRequiredService<INavigationExecutor>();
            var navigationCatalog = _provider.GetRequiredService<NavigationCatalog>();
            // 轮盘预热（启动编排末尾）所需：配置运行态与图标资产服务
            IConfigService config = _config;
            var iconAssets = _provider.GetRequiredService<IIconAssetService>();
            var navigationStore = _provider.GetRequiredService<NavigationStore>();

            // 手势控制器需在钩子启动前实例化并保持订阅（构造即接线鼠标事件）。
            _ = _provider.GetRequiredService<GestureController>();

            // 页面 VM 不再在启动期 eager 解析：它们的作用域是设置台会话，首次进入该页时
            // 由导航执行缝经 ConsolePageSession 构造（见 CreateSettingsConsole）。
            // 设置台会话缓存：开/结束会话由设置台租户驱动，实例边界是组合根交付的 DI 作用域。
            var pageSession = _provider.GetRequiredService<ConsolePageSession>();
            // 插件运行时（扫描 + 装载/停用/再启用）：由 ShellHost 在启动序列里驱动。
            var pluginRuntime = _provider.GetRequiredService<PluginRuntimeHost>();
            // 插件 UI 托管门面：ShellHost 用它合成托盘菜单的插件条目、插件管理页用它呈现设置区块。
            var pluginUi = _provider.GetRequiredService<PluginUiCoordinator>();
            // 导航视图出账与恢复重放（设置台关闭出账、重开重放；构造点收在组合根）。
            var navigationSuspension = new NavigationSuspension(navigationStore, navigationCatalog, navigation);

            // 设置台会话工厂：会话级对象图（导航区 VM + 壳区 VM + 会话作用域内的页面/设置子 VM）
            // 在每次开窗时新建，解析仍只发生在组合根——壳层拿到的只是这个闭包。
            // 工厂在构造租户前开启会话作用域（Begin），结束由租户释放时执行（End）。
            SettingsConsole CreateSettingsConsole(System.Windows.Window anchor, Func<bool> isExiting)
            {
                pageSession.Begin();
                IServiceProvider sessionServices = pageSession.Services;
                return new SettingsConsole(
                    new MainViewModel(navigationStore, navigationCatalog, navigation, localization),
                    new ShellViewModel(messenger, dialogService, localization),
                    themeService,
                    dialogService,
                    sessionServices.GetRequiredService<InterfaceThemeSettingsViewModel>(),
                    saveOrchestrator,
                    iconAssets,
                    navigationSuspension,
                    messenger,
                    anchor,
                    background,
                    pageSession,
                    isExiting);
            }

            return new ShellHost(
                messenger,
                mouseHook,
                dialogService,
                themeService,
                localization,
                config,
                iconAssets,
                saveOrchestrator,
                navigation,
                _hostDelegates,
                pluginRuntime,
                pluginUi,
                navigationSuspension,
                CreateSettingsConsole,
                background,
                testInstance);
        }

        public void Dispose()
        {
            // 容器随组合根释放；托盘/钩子/设置台由 ShellHost.Dispose 先行释放。
            _provider.Dispose();
        }
    }
}
