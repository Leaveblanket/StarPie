using System;

namespace StarPie
{
    /// <summary>
    /// 标识以 "--dev" 启动的开发实例，使其可与已安装的正式版并行运行：
    /// 独立配置目录、独立的单实例互斥名、中键手势触发，且不写真实开机自启注册表项。
    /// </summary>
    public static class DevInstance
    {
        private const string Flag = "--dev";

        /// <summary>追加到窗口标题与托盘 tooltip 的可见标记。</summary>
        public static string Suffix => IsActive ? " (Dev)" : string.Empty;

        /// <summary>
        /// 按实例类型区分的单实例互斥名：开发实例与正式实例可并行运行，
        /// 同类型实例之间互斥。
        /// </summary>
        public static string MutexName => IsActive
            ? @"Global\StarPie_DevInstance_Mutex_9B8A7D"
            : @"Global\StarPie_SingleInstance_Mutex_9B8A7C";

        public static bool IsActive =>
            Environment.CommandLine.Contains(Flag, StringComparison.OrdinalIgnoreCase);
    }
}
