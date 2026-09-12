using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace StarPie.Modules
{
    /// <summary>
    /// M4 界面主题贡献者：模块侧注册自治（无导航页）。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 把主题服务与主题设置子 VM 的 DI 注册下放本程序集
    /// （组合根仍唯一 BuildServiceProvider，本贡献者只注册不解析）；本模块无导航页，
    /// <see cref="ICompositionContributor.RegisterNavigation"/> 保持默认空实现。
    /// 主题状态与解析在内核 <see cref="ThemeEngine"/>；调色板换入由宿主
    /// <c>AppHost</c> 装配面把 Ui 侧 <see cref="IThemeApplier"/> 适配器接到主题服务上，
    /// 本贡献者不触碰 Application 资源。
    /// </remarks>
    internal sealed class ThemeContributor : ICompositionContributor
    {
        public string Id => "theme";

        public int Order => 20;

        /// <summary>注册主题服务与主题设置子 VM（容器单例）。</summary>
        public void RegisterServices(IServiceCollection services)
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
