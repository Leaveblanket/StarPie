using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Icons;

namespace StarPie.Modules
{
    /// <summary>
    /// 图标模块注册器：S1 实现（StarPie.Icons）的模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 把 <see cref="IconAssetService"/>（<see cref="IIconAssetService"/>
    /// 实现）的 DI 注册下放本程序集（组合根仍唯一 BuildServiceProvider，本注册器只注册不解析）。
    /// ADR-0023/#95：契约 <see cref="IIconAssetService"/> 与纯资产目录 <see cref="IconCatalog"/> 驻
    /// <c>StarPie.Icons.Contracts</c>；.lnk 解析契约 <see cref="IShortcutTargetResolver"/> 自
    /// ADR-0023/#96 起随 M3 下沉 <c>StarPie.Programs.Contracts</c>（本 runtime 经契约边消费，
    /// Core 引用仅余 S2 AppDataPaths 共享基建）。
    /// 本模块无导航页，不提供 RegisterNavigation。
    /// </remarks>
    public static class IconsModuleRegistrar
    {
        /// <summary>注册 S1 图标资产实例服务（容器单例）：工厂经 ServiceProvider 惰性解析
        /// Programs.Contracts 的 <see cref="IShortcutTargetResolver"/>（由 ProgramsModuleRegistrar
        /// 注册的 M3 实现提供）。</summary>
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IIconAssetService>(sp => new IconAssetService(
                sp.GetRequiredService<IShortcutTargetResolver>()));
        }
    }
}
