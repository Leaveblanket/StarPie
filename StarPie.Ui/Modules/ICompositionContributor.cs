using Microsoft.Extensions.DependencyInjection;
using StarPie.Services.Navigation;

namespace StarPie.Modules
{
    /// <summary>
    /// 组合贡献者：内置模块与后续插件贡献者共用的一条注册管线入口。
    /// </summary>
    /// <remarks>
    /// 贡献者**只登记不解析**：<see cref="RegisterServices"/> 写入 DI 描述符、
    /// <see cref="RegisterNavigation"/> 写入导航目录，两者都发生在组合根的注册期；
    /// 解析一律由组合根在容器构建后按目录 eager 驱动（注册顺序 ≠ 解析时机）。
    /// <see cref="Id"/> 是稳定身份（清单内唯一，用于诊断与排序核对），<see cref="Order"/>
    /// 是注册顺序权重（升序执行，仅决定注册序列）；插件装载把插件贡献适配成同一接口后
    /// 追加进同一管线（P2/P3）。
    /// </remarks>
    public interface ICompositionContributor
    {
        /// <summary>贡献者稳定标识（清单内唯一；不参与解析与导航寻址）。</summary>
        string Id { get; }

        /// <summary>注册顺序权重（升序执行；只影响注册序列，不影响解析时机）。</summary>
        int Order { get; }

        /// <summary>把本贡献者的服务与页面 VM 写入容器（注册单例，不解析）。</summary>
        void RegisterServices(IServiceCollection services);

        /// <summary>
        /// 把本贡献者的导航页写入目录；无导航页的贡献者保持默认空实现
        /// （目录完整性由 <see cref="NavigationCatalog.Validate"/> 在注册期收口）。
        /// </summary>
        void RegisterNavigation(NavigationCatalog catalog)
        {
        }
    }
}
