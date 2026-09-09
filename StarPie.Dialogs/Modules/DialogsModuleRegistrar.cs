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
    /// 本模块无导航页，不提供 RegisterNavigation。ADR-0023/#96：契约 <see cref="IDialogService"/>
    /// 与结果 record 随实现方下沉 <c>StarPie.Dialogs.Contracts</c>；扫描/.lnk 能力经
    /// <c>StarPie.Programs.Contracts</c> 契约（<see cref="IProgramScanner"/>/
    /// <see cref="IShortcutTargetResolver"/>）注入（M3 注册器提供实现），Dialogs→Programs
    /// 仅经契约边；窗口主题应用消费 <see cref="IThemeService"/>（Theme.Contracts 契约边，
    /// Dialogs→M4 runtime 允许边清零，ADR-0023/#97）。
    /// <see cref="DialogService"/> 裁决 public——宿主 AppHost 建窗后调
    /// <c>SetOwner(MainView)</c> 惰性回填 Owner（ADR-0004）。
    /// </remarks>
    public static class DialogsModuleRegistrar
    {
        /// <summary>注册对话框服务（容器单例）：工厂经 ServiceProvider 惰性解析
        /// Dialogs.Contracts/Programs.Contracts/Icons.Contracts 契约与 M4 主题服务；
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
