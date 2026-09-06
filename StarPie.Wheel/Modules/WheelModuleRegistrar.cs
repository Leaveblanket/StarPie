using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinPieGestures.Services.Configuration;
using WinPieGestures.Services.Dialogs;
using WinPieGestures.Services.Localization;
using WinPieGestures.Services.Shell;
using WinPieGestures.Services.Wheel;
using WinPieGestures.ViewModels.Pages;

namespace WinPieGestures.Modules
{
    /// <summary>
    /// M2 轮盘与渲染模块注册器（B8/#81：随 M2 抽取为独立模块程序集 StarPie.Wheel 后新立，
    /// 沿用 B6/#79 ShellModuleRegistrar / B7/#80 ThemeModuleRegistrar 的模块注册器样板，
    /// ADR-0016 决策 8/9/11）——模块侧注册自治：<see cref="RegisterServices"/> 把本模块
    /// 轮盘工厂与轮盘外观设置子 VM 的 DI 注册下放本程序集（组合根仍唯一 BuildServiceProvider，
    /// 本注册器只注册不解析）。M2 无导航页，不提供 RegisterNavigation（与 M4 同构）。
    /// D5 解结（ADR-0016 决策 11）：轮盘工厂实现（<see cref="WheelFactory"/>）随 M2 收编，
    /// 接口 <see cref="IWheelFactory"/> 留 M2 侧——仍驻 Host 的 M1 手势侧（GestureEngine）
    /// 只经该接口消费，不再反向组装 M2 瞬态轮盘；预览 Profile 只读契约
    /// <see cref="IProfilePreviewSource"/> 已上提共享内核 Core（实现方 M1 ProfileListViewModel
    /// 仍在 Host、消费方本集 WheelAppearanceSettingsViewModel，均只依赖 Core）。
    /// 工厂/外观子 VM 只依赖 Core 契约与 M4 的 <see cref="IThemeService"/>（允许边），
    /// 不反向引用 Host。
    /// </summary>
    public static class WheelModuleRegistrar
    {
        /// <summary>注册 M2 轮盘工厂与轮盘外观设置子 VM（容器单例，B8/#81 自 Composition 下放）。</summary>
        public static void RegisterServices(IServiceCollection services)
        {
            // D5：IWheelFactory → WheelFactory 装配注册随工厂收编进本模块；M1 手势侧仅经接口消费。
            services.AddSingleton<IWheelFactory, WheelFactory>();
            // 轮盘外观设置子 VM（#56/ADR-0014 决策 6）：单例注入外观聚合 VM；预览 Profile 上下文
            // 经 Core 只读契约 IProfilePreviewSource 转发（实现方 M1 ProfileListViewModel 由
            // Host 组合根注册别名，本注册器只解析 Core 契约，不引用具体方案列表 VM）。
            services.AddSingleton(sp => new WheelAppearanceSettingsViewModel(
                sp.GetRequiredService<IConfigService>(),
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IProfilePreviewSource>(),
                sp.GetRequiredService<ILocalizationService>()));
        }
    }
}
