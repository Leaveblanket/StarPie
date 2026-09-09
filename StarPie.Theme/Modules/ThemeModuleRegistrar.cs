using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Configuration;
using StarPie.Services.Localization;
using StarPie.Services.Shell;
using StarPie.ViewModels.Pages;

namespace StarPie.Modules
{
    /// <summary>
    /// 界面主题模块注册器：模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 把本模块服务与主题设置子 VM 的 DI 注册下放本程序集
    /// （组合根仍唯一 BuildServiceProvider，本注册器只注册不解析）。
    /// 本模块无导航页，不提供 RegisterNavigation（模块内无页面时组合根不登记导航）。
    /// 主题服务（<see cref="ThemeService"/>，实现 <see cref="IThemeService"/>）与
    /// <see cref="InterfaceThemeSettingsViewModel"/> 只依赖共享内核与自身契约程序集
    /// （ADR-0023/#97：IThemeService 驻 StarPie.Theme.Contracts，Wheel/Dialogs 消费方经
    /// 契约边引用，本 runtime 不再被其它业务 runtime 引用），不反向引用宿主；
    /// 调色板换入（AppThemePaletteManager，public）由宿主 <c>AppHost</c> 装配面编排
    /// （AttachPaletteApplier + Apply），本注册器不触碰 Application 资源。
    /// </remarks>
    public static class ThemeModuleRegistrar
    {
        /// <summary>注册主题服务与主题设置子 VM（容器单例）。</summary>
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
