using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Dialogs;
using StarPie.Services.Icons;
using StarPie.Kernel.Localization;
using StarPie.Services.Programs;
using StarPie.Services.Shell;

namespace StarPie.Modules
{
    /// <summary>
    /// 对话框模块注册器：S6 实现的模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 把 <see cref="DialogService"/>（<see cref="IDialogService"/>
    /// 实现）的 DI 注册下放本程序集（组合根仍唯一 BuildServiceProvider，本注册器只注册不解析）。
    /// 本模块无导航页，不提供 RegisterNavigation。扫描/.lnk 能力经 <c>StarPie.Sdk</c> 契约
    /// （<see cref="IProgramScanner"/>/<see cref="IShortcutTargetResolver"/>）注入，图标资产经
    /// <c>StarPie.Sdk.Wpf</c> 的 <see cref="IIconAssetService"/> 注入，实现与注册均在组合根；
    /// 窗口主题应用消费 <see cref="IThemeService"/>。
    /// <see cref="DialogService"/> 裁决 public——宿主 AppHost 建窗后调
    /// <c>SetOwner(MainView)</c> 惰性回填 Owner（ADR-0004）。
    /// </remarks>
    public static class DialogsModuleRegistrar
    {
        /// <summary>注册对话框服务（容器单例）：工厂经 ServiceProvider 惰性解析
        /// SDK（IDialogService 与程序扫描/.lnk 契约）/Sdk.Wpf（图标资产）与主题服务；
        /// 宿主在窗口创建后回填 Owner。</summary>
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton(sp => new DialogService(
                sp.GetRequiredService<IThemeService>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<IIconAssetService>(),
                sp.GetRequiredService<IShortcutTargetResolver>(),
                sp.GetRequiredService<IProgramScanner>()));
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        }
    }
}
