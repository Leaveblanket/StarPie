using StarPie.Kernel.Configuration;

namespace StarPie.PluginRuntime
{
    /// <summary>
    /// 插件子系统的默认落盘位置：随包插件目录、用户插件目录、宿主状态与启动报告。
    /// </summary>
    /// <remarks>
    /// 路径推导只此一处，组合根按需取用；用户侧目录随 dev 实例切换沙箱目录
    /// （见 <see cref="AppDataPaths"/>），dev 实例绝不触碰正式版插件。
    /// </remarks>
    public static class PluginPaths
    {
        /// <summary>随包插件目录（宿主安装目录旁 <c>plugins/</c>；与宿主一同分发）。</summary>
        public static string InstallDirectory => Path.Combine(AppContext.BaseDirectory, "plugins");

        /// <summary>用户插件目录（应用数据目录下 <c>plugins/</c>）。</summary>
        public static string UserDirectory => Path.Combine(AppDataPaths.GetAppDataFolder(), "plugins");

        /// <summary>宿主插件状态文件（宿主唯一权威，插件不可读写）。</summary>
        public static string StateFilePath => Path.Combine(AppDataPaths.GetAppDataFolder(), "plugin-state.json");

        /// <summary>插件启动报告文件（每次启动扫描覆写，诊断与准入结果可见面）。</summary>
        public static string StartupReportFilePath => Path.Combine(AppDataPaths.GetAppDataFolder(), "plugin-startup-report.json");
    }
}
