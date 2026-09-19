using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using SharpHook;
using SharpHook.Data;
using StarPie.Ui.Services.Actions;
using StarPie.Host.Configuration;
using StarPie.Sdk.Services.Dialogs;
using StarPie.Ui.Services.Dialogs;
using StarPie.Ui.Services.Input;
using StarPie.Host.Localization;
using StarPie.Sdk.Services.Navigation;
using StarPie.Ui.Services.Navigation;
using StarPie.Sdk.ViewModels.Pages;
using StarPie.Ui.ViewModels.Pages;

namespace StarPie.Ui.Modules
{
    /// <summary>
    /// M1 轮盘与动作贡献者：模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterNavigation"/> 把本模块页面（触发与场景 / 轮盘与动作）写入
    /// <see cref="NavigationCatalog"/>（槽位/标题键/图标/目标类型），页面 DataTemplate 收进
    /// WheelInteractionPageTemplates.xaml(App.xaml 以本地相对 Source 每模块一次静态合并)。
    /// <see cref="RegisterServices"/> 把本模块输入栈（捕获/看门狗）、页面 VM 与只读预览契约
    /// <see cref="IProfilePreviewSource"/> 别名的 DI 注册下放本程序集（组合根仍唯一
    /// BuildServiceProvider，本贡献者只注册不解析）。新增页面/动作/触发规则只动本模块内部。
    /// 依赖方向：本模块依赖共享内核契约，仅经 SDK 的
    /// <c>IWheelFactory</c>/<c>IWheelViewModel</c> 契约接口消费瞬态轮盘（M1→M2 runtime
    /// 允许边清零，ADR-0023），不反向引用宿主。
    /// </remarks>
    internal sealed class WheelInteractionContributor : ICompositionContributor
    {
        private readonly MouseButton _triggerButton;

        /// <param name="triggerButton">轮盘触发键（默认右键；测试实例经命令行覆盖，
        /// 解析见 <see cref="TestInstanceSwitches"/>——本贡献者只消费结果，不接触命令行）。</param>
        public WheelInteractionContributor(MouseButton triggerButton = TestInstanceSwitches.DefaultButton)
            => _triggerButton = triggerButton;

        public string Id => "wheel.interaction";

        public int Order => 40;

        /// <summary>向导航目录注册本模块页面（槽位 0 触发与场景 / 2 轮盘与动作）。</summary>
        public void RegisterNavigation(NavigationCatalog catalog)
        {
            catalog.RegisterPage<BehaviorSettingsViewModel>(
                NavigationSlot.Trigger,
                NavigationSlots.GetAutomationId(NavigationSlot.Trigger),
                "PageTrigger",
                IconTrigger);
            catalog.RegisterPage<ProfileListViewModel>(
                NavigationSlot.WheelInteraction,
                NavigationSlots.GetAutomationId(NavigationSlot.WheelInteraction),
                "PageWheelActions",
                IconWheelInteraction);
        }

        /// <summary>
        /// 注册本模块轮盘交互管线服务与页面 VM（容器单例）：鼠标钩子/窗口上下文/动作执行器/
        /// 引擎与触发+轮盘交互两页 VM 全部在本贡献者接线；工厂经 ServiceProvider
        /// 惰性解析 SDK 契约。<see cref="ProfileListViewModel"/> 另以只读契约
        /// <see cref="IProfilePreviewSource"/>（ADR-0023；驻 <c>StarPie.Sdk</c>）
        /// 注册别名——消费方轮盘外观设置子 VM 只依赖契约程序集，不引用本集具体 VM。
        /// </summary>
        public void RegisterServices(IServiceCollection services)
        {
            // 输入栈（ADR-0052）：捕获走 SharpHook 的 SimpleGlobalHook——只有它支持与钩子
            // 同线程同步设置抑制；抑制决策、回放窗口与看门狗留在本集自研侧。
            // 钩子独占专用线程，松手副作用经调度接缝回 UI 线程——适配器不引用 UI 框架类型。
            services.AddSingleton<IActionExecutorService, ActionExecutorService>();
            services.AddSingleton<IWindowContext, WindowContext>();
            services.AddSingleton<WheelInteractionEngine>();
            services.AddSingleton(sp => new MouseInputHook(
                new SimpleGlobalHook(),
                sp.GetRequiredService<WheelInteractionEngine>(),
                sp.GetRequiredService<IActionExecutorService>(),
                callback => System.Windows.Application.Current?.Dispatcher?.BeginInvoke(callback),
                triggerButton: _triggerButton));

            // 页面 VM 的作用域是设置台会话：同一会话内保留实例（切页保状态），会话结束整批释放；
            // 解析只经导航执行缝（ADR-0039 决策 9）。
            services.AddScoped(sp => new BehaviorSettingsViewModel(
                sp.GetRequiredService<IConfigService>().Current,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddScoped(sp => new ProfileListViewModel(
                sp.GetRequiredService<IConfigService>().Current.Profiles,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IActionExecutorService>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<IIconAssetService>()));
            // 配置方案列表 VM 以只读契约 IProfilePreviewSource 暴露给轮盘侧：
            // 轮盘外观设置子 VM 经接口解析，不引用本集具体 VM 类型
            //（契约驻 StarPie.Sdk，ADR-0023）。
            services.AddScoped<IProfilePreviewSource>(sp => sp.GetRequiredService<ProfileListViewModel>());
        }

        // 导航图标路径数据（NavPage0 / NavPage2）。
        private const string IconTrigger =
            "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z";
        private const string IconWheelInteraction =
            "M4,6c0-1.1,0.9-2,2-2h12c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H6c-1.1,0-2-0.9-2-2V6z M6,8h12V6H6V8z M6,12h12v-2H6V12z M6,16h6v-2H6V16z";
    }
}
