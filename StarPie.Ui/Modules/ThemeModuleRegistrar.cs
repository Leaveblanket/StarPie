using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace StarPie.Modules
{
    /// <summary>
    /// 界面主题模块注册器：模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 把主题服务与主题设置子 VM 的 DI 注册下放本程序集
    /// （组合根仍唯一 BuildServiceProvider，本注册器只注册不解析）。
    /// 本模块无导航页，不提供 <c>RegisterNavigation</c>。
    /// 主题状态与解析在内核 <see cref="ThemeEngine"/>；调色板换入由宿主
    /// <c>AppHost</c> 装配面把 Ui 侧 <see cref="IThemeApplier"/> 适配器接到主题服务上，
    /// 本注册器不触碰 Application 资源。
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
