using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Icons;
using StarPie.Services.Programs;

namespace StarPie.Modules
{
    /// <summary>
    /// 程序扫描与目录模块注册器：模块侧注册自治。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 把 M3 的快捷方式解析与程序扫描契约注册下放本程序集
    /// （组合根仍唯一 BuildServiceProvider，本注册器只注册不解析）。本模块无导航页，
    /// 不提供 RegisterNavigation。ADR-0019/#87 起 M3 单向依赖 Core；ADR-0023/#96 起契约
    /// 随实现方下沉：<see cref="IShortcutTargetResolver"/>（SPI）与 <see cref="IProgramScanner"/>
    /// /<see cref="ProgramEntry"/>/<see cref="ProgramCatalog"/> 驻
    /// <c>StarPie.Programs.Contracts</c>，实现 <see cref="ProgramScanner"/> 经容器注入
    /// Icons.Contracts 的 <see cref="IIconAssetService"/> 契约完成图标补全。
    /// </remarks>
    public static class ProgramsModuleRegistrar
    {
        /// <summary>注册 M3 服务（容器单例）：快捷方式解析实例实现 M3 契约程序集的 SPI。</summary>
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IShortcutTargetResolver, ShortcutResolver>();
            services.AddSingleton<IProgramScanner, ProgramScanner>();
        }
    }
}
