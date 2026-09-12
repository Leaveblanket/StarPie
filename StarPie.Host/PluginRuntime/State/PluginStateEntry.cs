using StarPie.PluginRuntime.Admission;

namespace StarPie.PluginRuntime.State
{
    /// <summary>
    /// 宿主状态里的单个插件条目：用户在宿主侧的权威记录，与插件自己的配置段分离。
    /// </summary>
    public sealed class PluginStateEntry
    {
        /// <summary>用户启用/停用意图；新发现包默认启用，拒绝与隔离不改变该意图。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>最近一次扫描所见版本（清单不可用时为空）。</summary>
        public string? Version { get; set; }

        /// <summary>
        /// 已就位但尚未装载的新版本（界面插件更新后留待下次启动装载）；无挂起时为空。
        /// 与 <see cref="Version"/> 分开：后者是包目录里的当前版本，前者是本进程不能立即生效的那一份。
        /// </summary>
        public string? PendingVersion { get; set; }

        /// <summary>最近一次扫描所见包目录（同 id 冲突时记安装目录中的那份）。</summary>
        public string? PackagePath { get; set; }

        /// <summary>最近一次扫描的准入结果四态。</summary>
        public PluginAdmission Admission { get; set; } = PluginAdmission.Rejected;

        /// <summary>准入结果原因（拒绝时含具体违规）。</summary>
        public string? AdmissionReason { get; set; }

        /// <summary>隔离状态；非空表示该插件被隔离（停用 + 不再调用 + 重启不自动重试）。</summary>
        public PluginQuarantineState? Quarantine { get; set; }
    }
}
