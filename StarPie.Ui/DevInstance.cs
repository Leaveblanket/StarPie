using StarPie.Kernel.Configuration;

namespace StarPie
{
    /// <summary>
    /// 开发实例标记：Debug 构建即开发实例（判定唯一真相在共享内核
    /// <see cref="AppDataPaths.IsDevInstance"/>，按构建配置编译期定死），使其可与已安装的
    /// 正式版并行运行——独立配置目录、独立的单实例互斥名、中键手势触发，且不写真实
    /// 开机自启注册表项。本类只是 Ui 侧投影。
    /// </summary>
    public static class DevInstance
    {
        /// <summary>追加到窗口标题与托盘 tooltip 的可见标记。</summary>
        public static string Suffix => AppDataPaths.IsDevInstance ? " (Dev)" : string.Empty;

        /// <summary>
        /// 按实例类型区分的单实例互斥名：开发实例与正式实例可并行运行，
        /// 同类型实例之间互斥。
        /// </summary>
        public static string MutexName => AppDataPaths.IsDevInstance
            ? @"Global\StarPie_DevInstance_Mutex_9B8A7D"
            : @"Global\StarPie_SingleInstance_Mutex_9B8A7C";
    }
}
