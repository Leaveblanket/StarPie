using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.State;

namespace StarPie.PluginRuntime.Diagnostics
{
    /// <summary>
    /// 启动报告里的单个插件条目：准入结果四态、原因、启用意图、隔离状态与全部候选包路径。
    /// </summary>
    public sealed class PluginStartupReportEntry
    {
        /// <summary>插件 id（清单 id；清单不可用时回落包目录名）。</summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>清单展示名（清单可用时）。</summary>
        public string? Name { get; set; }

        /// <summary>清单版本（清单可用时）。</summary>
        public string? Version { get; set; }

        /// <summary>
        /// 清单是否声明了 ui 段（界面插件）。更新语义按此分档：界面插件的程序集留在进程内，
        /// 新版本须下次启动生效；无界面插件可就地卸载后装载新版本。
        /// </summary>
        public bool HasUi { get; set; }

        /// <summary>准入结果四态。</summary>
        public PluginAdmission Admission { get; set; }

        /// <summary>准入结果原因；拒绝时即拒绝理由（含全部违规）。</summary>
        public string AdmissionReason { get; set; } = string.Empty;

        /// <summary>签名主体（入口程序集签名校验所得；无签名或未配置校验器时为 null）。</summary>
        public string? SignatureSubject { get; set; }

        /// <summary>用户的启用/停用意图（宿主状态）。</summary>
        public bool Enabled { get; set; }

        /// <summary>隔离状态；非空表示被隔离。</summary>
        public PluginQuarantineState? Quarantine { get; set; }

        /// <summary>同 id 的全部候选包目录；安装目录与用户目录同 id 冲突时列出两份。</summary>
        public List<string> PackagePaths { get; set; } = new();

        /// <summary>清单/包校验与冲突违规；非空即拒绝。</summary>
        public List<string> Violations { get; set; } = new();
    }
}
