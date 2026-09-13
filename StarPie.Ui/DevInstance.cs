using StarPie.Kernel.Configuration;

namespace StarPie
{
    /// <summary>
    /// 开发实例标记：Debug 构建即开发实例（判定唯一真相在共享内核
    /// <see cref="AppDataPaths.IsDevInstance"/>，按构建配置编译期定死）。
    /// dev 沙箱隔离配置目录且不写真实开机自启注册表项；与正式实例同闸互斥、
    /// 同为右键触发，不并行运行。本类只承担可见标识，是 Ui 侧投影。
    /// </summary>
    public static class DevInstance
    {
        /// <summary>追加到窗口标题与托盘 tooltip 的可见标记。</summary>
        public static string Suffix => AppDataPaths.IsDevInstance ? " (Dev)" : string.Empty;
    }
}
