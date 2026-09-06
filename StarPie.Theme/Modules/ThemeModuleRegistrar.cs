using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Configuration;
using StarPie.Services.Localization;
using StarPie.Services.Shell;
using StarPie.ViewModels.Pages;

namespace StarPie.Modules
{
    /// <summary>
    /// M4 界面主题模块注册器（B7/#80：随 M4 抽取为独立模块程序集 StarPie.Theme 后新立，
    /// 沿用 B6/#79 ShellModuleRegistrar 的模块注册器样板，ADR-0016 决策 8/9）——
    /// 模块侧注册自治：<see cref="RegisterServices"/> 把本模块服务与主题设置子 VM 的 DI 注册
    /// 下放本程序集（组合根仍唯一 BuildServiceProvider，本注册器只注册不解析）。
    /// M4 无导航页，不提供 RegisterNavigation（与 M3 同构：模块内无页面时组合根不登记导航）。
    /// 主题服务（<see cref="ThemeService"/>/<see cref="IThemeService"/>）与
    /// <see cref="InterfaceThemeSettingsViewModel"/> 只依赖 Core 契约（S2/S3/S4），不反向引用 Host；
    /// 调色板换入（<see cref="ThemePaletteManager"/>，本集 public）仍由 Host <c>AppHost</c>
    /// 装配面编排（AttachPaletteApplier + Apply），本注册器不触碰 Application 资源。
    /// </summary>
    public static class ThemeModuleRegistrar
    {
        /// <summary>注册 M4 主题服务与主题设置子 VM（容器单例，B7/#80 自 Composition 下放）。</summary>
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton(new ThemeService());
            services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
            services.AddSingleton(sp => new InterfaceThemeSettingsViewModel(
                sp.GetRequiredService<IConfigService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<ILocalizationService>()));
        }
    }
}
