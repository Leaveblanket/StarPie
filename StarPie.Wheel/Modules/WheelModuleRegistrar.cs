using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Kernel.Configuration;
using StarPie.Services.Dialogs;
using StarPie.Kernel.Localization;
using StarPie.Services.Shell;
using StarPie.Services.Wheel;
using StarPie.ViewModels.Pages;

namespace StarPie.Modules
{
    /// <summary>
    /// 轮盘与渲染模块注册器：模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 把本模块轮盘工厂与轮盘外观设置子 VM 的 DI 注册
    /// 下放本程序集（组合根仍唯一 BuildServiceProvider，本注册器只注册不解析）。
    /// 本模块无导航页，不提供 RegisterNavigation。轮盘工厂实现（<see cref="WheelFactory"/>）
    /// 与接口 <see cref="IWheelFactory"/> 契约（ADR-0023/#97 起独立成集，#112 收口入
    /// StarPie.Sdk）本模块实现之，手势侧（Gestures runtime）只经该契约消费，不反向组装瞬态轮盘；
    /// 预览 Profile 只读契约 <see cref="IProfilePreviewSource"/> 随实现方 M1 下沉、
    /// #112 收口入 StarPie.Sdk，实现方与消费方经其解耦。工厂/外观子 VM 只依赖共享内核、
    /// SDK 契约面与 Theme.Contracts 的 <see cref="IThemeService"/>（M2→M4 runtime
    /// 允许边清零），不反向引用宿主。
    /// </remarks>
    public static class WheelModuleRegistrar
    {
        /// <summary>注册轮盘工厂与轮盘外观设置子 VM（容器单例）。</summary>
        public static void RegisterServices(IServiceCollection services)
        {
            // IWheelFactory → WheelFactory 装配注册；手势侧仅经接口消费。
            services.AddSingleton<IWheelFactory, WheelFactory>();
            // 轮盘外观设置子 VM：单例注入外观聚合 VM；预览 Profile 上下文经 SDK 只读契约
            // IProfilePreviewSource 转发（实现方别名由 GesturesModuleRegistrar 注册），
            // 本注册器只解析 SDK 契约面，不引用具体方案列表 VM。
            services.AddSingleton(sp => new WheelAppearanceSettingsViewModel(
                sp.GetRequiredService<IConfigService>(),
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IProfilePreviewSource>(),
                sp.GetRequiredService<ILocalizationService>()));
        }
    }
}
