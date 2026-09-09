using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Actions;
using StarPie.Services.Configuration;
using StarPie.Services.Dialogs;
using StarPie.Services.Gestures;
using StarPie.Services.Localization;
using StarPie.Services.Navigation;
using StarPie.ViewModels.Pages;

namespace StarPie.Modules
{
    /// <summary>
    /// 手势与动作模块注册器：模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterNavigation"/> 把本模块页面（触发与场景 / 手势与动作）写入
    /// <see cref="NavigationCatalog"/>（槽位/标题键/图标/目标类型），页面 DataTemplate 收进
    /// GesturesPageTemplates.xaml（宿主 App.xaml 经跨程序集 pack URI 每模块一次静态合并）。
    /// <see cref="RegisterServices"/> 把本模块手势管线服务、页面 VM 与只读预览契约
    /// <see cref="IProfilePreviewSource"/> 别名的 DI 注册下放本程序集（组合根仍唯一
    /// BuildServiceProvider，本注册器只注册不解析）。新增页面/动作/触发规则只动本模块内部。
    /// 依赖方向：本模块依赖共享内核契约，仅经 <c>StarPie.Wheel.Contracts</c> 的
    /// <c>IWheelFactory</c>/<c>IWheelViewModel</c> 契约接口消费瞬态轮盘（M1→M2 runtime
    /// 允许边清零，ADR-0023/#97），不反向引用宿主；MouseHook 的 dev 分支读共享内核
    /// <see cref="AppDataPaths.IsDevInstance"/> 回填缝（组合根装配前以 DevInstance.IsActive 回填）。
    /// </remarks>
    public static class GesturesModuleRegistrar
    {
        /// <summary>向导航目录注册本模块页面（槽位 0 触发与场景 / 2 手势与动作）。</summary>
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
        /// 注册本模块手势管线服务与页面 VM（容器单例）：鼠标钩子/窗口上下文/动作执行器/
        /// 引擎/控制器与触发+手势两页 VM 全部在本注册器接线；工厂经 ServiceProvider
        /// 惰性解析 Wheel.Contracts 契约。<see cref="ProfileListViewModel"/> 另以只读契约
        /// <see cref="IProfilePreviewSource"/>（驻 Gestures.Contracts，ADR-0023/#97）
        /// 注册别名——消费方轮盘外观设置子 VM 只依赖契约程序集，不引用本集具体 VM。
        /// </summary>
        public static void RegisterServices(IServiceCollection services)
        {
            // 手势管线：MouseHook 为无参单例（构造即选 dev/正式触发键，dev 分支读共享内核
            // AppDataPaths.IsDevInstance 回填缝）；GestureController 构造即订阅钩子事件，由
            // CreateAppHost 在 Run 前 eager 解析保活。
            services.AddSingleton<MouseHook>();
            services.AddSingleton<IActionExecutorService, ActionExecutorService>();
            services.AddSingleton<IWindowContext, WindowContext>();
            services.AddSingleton<GestureEngine>();
            services.AddSingleton<GestureController>();

            // 页面 VM 为容器单例，状态跨导航常驻；解析时机在 Config.Load 之后 CreateAppHost。
            services.AddSingleton(sp => new BehaviorSettingsViewModel(
                sp.GetRequiredService<IConfigService>().Current,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton(sp => new ProfileListViewModel(
                sp.GetRequiredService<IConfigService>().Current.Profiles,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IActionExecutorService>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<IIconAssetService>()));
            // 配置方案列表 VM 以只读契约 IProfilePreviewSource 暴露给轮盘侧：
            // 轮盘外观设置子 VM 经接口解析，不引用本集具体 VM 类型
            //（契约随实现方下沉 Gestures.Contracts，ADR-0023/#97）。
            services.AddSingleton<IProfilePreviewSource>(sp => sp.GetRequiredService<ProfileListViewModel>());
        }

        // 导航图标路径数据（NavTab0 / NavTab2）。
        private const string IconTrigger =
            "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z";
        private const string IconGestures =
            "M4,6c0-1.1,0.9-2,2-2h12c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H6c-1.1,0-2-0.9-2-2V6z M6,8h12V6H6V8z M6,12h12v-2H6V12z M6,16h6v-2H6V16z";
    }
}
