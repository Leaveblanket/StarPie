using StarPie.Services;

namespace StarPie.Modules
{
    /// <summary>
    /// 内置贡献者清单：组合根注册期唯一遍历的有序列表。
    /// </summary>
    /// <remarks>
    /// 新增内置装配单元 = 在此登记一行（Id/Order/注册体），组合根不做任何逐模块硬编码；
    /// 插件贡献者在装载期按同一 <see cref="ICompositionContributor"/> 接口追加进同一管线
    /// （P2/P3）。清单顺序由 <see cref="ICompositionContributor.Order"/> 升序决定，
    /// <see cref="ICompositionContributor.Id"/> 在清单内唯一。
    /// </remarks>
    public static class BuiltInContributors
    {
        /// <summary>
        /// 构造内置贡献者有序清单。宿主回调委托包由组合根持有并注入（宿主状态不归贡献者），
        /// 其余贡献者无状态。
        /// </summary>
        public static IReadOnlyList<ICompositionContributor> CreateAll(AppHostDelegates hostDelegates)
        {
            ICompositionContributor[] contributors =
            {
                // 宿主编排与内核接入（配置/本地化/图标/扫描/导航运行时/壳层 VM）。
                new HostCoreContributor(hostDelegates),
                // Host 外观聚合页（槽位 1）。
                new HostPageContributor(),
                // M4 界面主题（无导航页）。
                new ThemeContributor(),
                // M2 轮盘与渲染（无导航页）。
                new WheelContributor(),
                // M1 手势与动作（槽位 0/2）。
                new GesturesContributor(),
                // M5 壳层与系统设置面（槽位 3）。
                new ShellContributor(),
                // S6 对话框（无导航页）。
                new DialogsContributor(),
            };

            return contributors.OrderBy(contributor => contributor.Order).ToArray();
        }
    }
}
