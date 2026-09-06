using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinPieGestures.Services.Actions;
using WinPieGestures.Services.Configuration;
using WinPieGestures.Services.Dialogs;
using WinPieGestures.Services.Gestures;
using WinPieGestures.Services.Localization;
using WinPieGestures.Services.Navigation;
using WinPieGestures.ViewModels.Pages;

namespace WinPieGestures.Modules
{
    /// <summary>
    /// M1 手势与动作模块注册器（B9/#82：随 M1 抽取为独立模块程序集 StarPie.Gestures 后新立，
    /// 沿用 B6/#79 ShellModuleRegistrar / B7/#80 ThemeModuleRegistrar / B8/#81
    /// WheelModuleRegistrar 的模块注册器样板，ADR-0016 决策 8/9/11）——模块侧注册自治：
    /// <see cref="RegisterNavigation"/> 自报本模块页面（触发与场景 / 手势与动作）写入
    /// <see cref="NavigationCatalog"/>（槽位/标题键/图标/目标类型），页面 DataTemplate 收进
    /// GesturesPageTemplates.xaml（Host App.xaml 经跨程序集 pack URI 每模块一次静态合并）；
    /// <see cref="RegisterServices"/> 把本模块手势管线服务、页面 VM 与 M1 只读预览契约
    /// <see cref="IProfilePreviewSource"/> 别名的 DI 注册下放本程序集（组合根仍唯一
    /// BuildServiceProvider，本注册器只注册不解析）。新增页面/动作/触发规则只动本模块内部，
    /// 不再触碰 Host（目标态 assemblies.md §5/§6，B9 后除 Host 外观聚合页外不再有 exe 内页面）。
    /// 依赖方向：StarPie.Gestures → StarPie.Core 单向 + StarPie.Gestures → StarPie.Wheel
    /// 允许边（M1→M2，assemblies.md §3）——手势侧 GestureEngine/GestureController 只经 M2 侧
    /// <c>IWheelFactory</c>/<c>IWheelViewModel</c> 接口消费瞬态轮盘（D5，ADR-0016 决策 11）；
    /// MouseHook 的 dev 分支改读 Core <see cref="AppDataPaths.IsDevInstance"/> 回填缝
    /// （组合根装配前以 DevInstance.IsActive 回填），本集不反向引用 Host。
    /// </summary>
    public static class GesturesModuleRegistrar
    {
        /// <summary>向导航目录注册 M1 页面（槽位 0 触发与场景 / 2 手势与动作）。</summary>
        public static void RegisterNavigation(NavigationCatalog catalog)
        {
            catalog.RegisterPage<BehaviorSettingsViewModel>(
                NavigationSlot.Trigger,
                NavigationSlots.GetAutomationId(NavigationSlot.Trigger),
                "TabTrigger",
                IconTrigger);
            catalog.RegisterPage<ProfileListViewModel>(
                NavigationSlot.Gestures,
                NavigationSlots.GetAutomationId(NavigationSlot.Gestures),
                "TabGestures",
                IconGestures);
        }

        /// <summary>
        /// 注册 M1 手势管线服务与页面 VM（容器单例，B9/#82 自 Composition 下放）：鼠标钩子/窗口
        /// 上下文/动作执行器/引擎/控制器与触发+手势两页 VM 全部在本注册器接线；工厂经
        /// ServiceProvider 惰性解析共享内核件（S2/S3/S4/S6 契约）。<see cref="ProfileListViewModel"/>
        /// 另以 M1 只读契约 <see cref="IProfilePreviewSource"/> 注册别名（#69/B8/#81 起接口上提
        /// Core）——消费方 M2 轮盘外观设置子 VM 只依赖 Core 契约，不引用本集具体 VM。
        /// </summary>
        public static void RegisterServices(IServiceCollection services)
        {
            // M1 手势管线：MouseHook 为无参单例（构造即选 dev/正式触发键，dev 分支读 Core
            // AppDataPaths.IsDevInstance 回填缝）；GestureController 构造即订阅钩子事件，由
            // CreateAppHost 在 Run 前 eager 解析保活（语义与迁移前一致）。
            services.AddSingleton<MouseHook>();
            services.AddSingleton<IActionExecutorService, ActionExecutorService>();
            services.AddSingleton<IWindowContext, WindowContext>();
            services.AddSingleton<GestureEngine>();
            services.AddSingleton<GestureController>();

            // M1 页面 VM（T19 容器单例，状态跨导航常驻；解析时机在 Config.Load 之后 CreateAppHost）。
            services.AddSingleton(sp => new BehaviorSettingsViewModel(
                sp.GetRequiredService<IConfigService>().Current,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton(sp => new ProfileListViewModel(
                sp.GetRequiredService<IConfigService>().Current.Profiles,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IActionExecutorService>(),
                sp.GetRequiredService<ILocalizationService>()));
            // #69（B2）/B9：配置方案列表 VM 以 M1 只读契约 IProfilePreviewSource 暴露给轮盘侧——
            // 轮盘外观设置子 VM 经接口解析，不引用具体 VM 类型（契约驻 Core，见 assemblies.md §6）。
            services.AddSingleton<IProfilePreviewSource>(sp => sp.GetRequiredService<ProfileListViewModel>());
        }

        // 导航图标（迁移前 MainViewModel 的 NavTab0/NavTab2 Path Data 原样搬迁，B3/#76）
        private const string IconTrigger =
            "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z";
        private const string IconGestures =
            "M4,6c0-1.1,0.9-2,2-2h12c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H6c-1.1,0-2-0.9-2-2V6z M6,8h12V6H6V8z M6,12h12v-2H6V12z M6,16h6v-2H6V16z";
    }
}
