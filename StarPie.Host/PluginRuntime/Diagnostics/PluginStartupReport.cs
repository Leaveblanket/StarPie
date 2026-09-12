using StarPie.PluginRuntime.Admission;

namespace StarPie.PluginRuntime.Diagnostics
{
    /// <summary>
    /// 插件启动报告：每次启动扫描的可见结果——扫描目录、开发者模式开关、逐插件准入结果与四态计数。
    /// </summary>
    public sealed class PluginStartupReport
    {
        /// <summary>报告生成时刻。</summary>
        public DateTimeOffset GeneratedAt { get; set; }

        /// <summary>本次扫描时开发者模式是否开启。</summary>
        public bool DeveloperModeEnabled { get; set; }

        /// <summary>随包插件目录。</summary>
        public string? InstallDirectory { get; set; }

        /// <summary>用户插件目录。</summary>
        public string? UserDirectory { get; set; }

        /// <summary>逐插件扫描结果（按 id 稳定序）。</summary>
        public List<PluginStartupReportEntry> Plugins { get; set; } = new();

        /// <summary>准入结果四态计数；零计数项也在列，四态在任何一次扫描里都可见。</summary>
        public Dictionary<PluginAdmission, int> AdmissionCounts { get; set; } = new();
    }
}
