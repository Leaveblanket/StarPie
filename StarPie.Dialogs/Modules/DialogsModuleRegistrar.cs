using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Dialogs;
using StarPie.Services.Icons;
using StarPie.Services.Localization;
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
    /// 本模块无导航页，不提供 RegisterNavigation。契约 <see cref="IDialogService"/> 与结果
    /// record 驻共享内核 Core；扫描能力经 Core 契约 <see cref="IProgramScanner"/> 注入（M3
    /// 注册器提供实现）；窗口主题应用消费 <see cref="IThemeService"/>（M4 允许边）。
    /// <see cref="DialogService"/> 裁决 public——宿主 AppHost 建窗后调
    /// <c>SetOwner(MainView)</c> 惰性回填 Owner（ADR-0004）。
    /// </remarks>
    public static class DialogsModuleRegistrar
    {
        /// <summary>注册对话框服务（容器单例）：工厂经 ServiceProvider 惰性解析共享内核
        /// 契约与 M3 扫描契约；宿主在窗口创建后回填 Owner。</summary>
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
