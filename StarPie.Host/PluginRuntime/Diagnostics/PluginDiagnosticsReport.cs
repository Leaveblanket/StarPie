using System;
using System.Collections.Generic;
using StarPie.PluginRuntime.Admission;

namespace StarPie.PluginRuntime.Diagnostics
{
    /// <summary>插件在宿主侧的运行状态（管理面列表与诊断报告共用）。</summary>
    public enum PluginRuntimeStatus
    {
        /// <summary>已装载且能力可见。</summary>
        Active,

        /// <summary>用户已停用（包与状态保留）。</summary>
        Disabled,

        /// <summary>已隔离：不再调用、不自动重试，等待显式重试或停用。</summary>
        Quarantined,

        /// <summary>新版本已就位但本进程不能生效（界面插件的程序集留在进程内）：重启后装载。</summary>
        PendingRestart,

        /// <summary>准入拒绝：包不装载。</summary>
        Rejected,

        /// <summary>启用但未活动（例如包不可用）。</summary>
        Inactive,
    }

    /// <summary>
    /// 单个插件的诊断报告：宿主状态 + 准入 + 隔离原因与可定位的残留清单。
    /// </summary>
    public sealed class PluginDiagnosticsReport
    {
        /// <summary>插件 id。</summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>清单展示名（清单可用时）。</summary>
        public string? Name { get; set; }

        /// <summary>清单版本（清单可用时）。</summary>
        public string? Version { get; set; }

        /// <summary>宿主侧运行状态。</summary>
        public PluginRuntimeStatus Status { get; set; }

        /// <summary>准入结果四态。</summary>
        public PluginAdmission Admission { get; set; }

        /// <summary>准入结果原因。</summary>
        public string AdmissionReason { get; set; } = string.Empty;

        /// <summary>生效包目录。</summary>
        public string? PackagePath { get; set; }

        /// <summary>本进程内正在生效的版本；未装载（停用/待重启/隔离）时为 null。</summary>
        public string? LoadedVersion { get; set; }

        /// <summary>已就位、等下次启动装载的新版本；无挂起更新时为 null。</summary>
        public string? PendingRestartVersion { get; set; }

        /// <summary>用户的启用/停用意图。</summary>
        public bool Enabled { get; set; }

        /// <summary>首次进入隔离的原因（隔离是持久状态，后续回收续做不改写它）。</summary>
        public string? QuarantineReason { get; set; }

        /// <summary>进入隔离的时间。</summary>
        public DateTimeOffset? QuarantinedAt { get; set; }

        /// <summary>可定位的残留清单（未回收资源）；无残留时为空。</summary>
        public IReadOnlyList<PluginResidual> Residuals { get; set; } = Array.Empty<PluginResidual>();

        /// <summary>
        /// 残留回收说明（降级判定下宿主框架缓存程序集时给出，解释重启后释放）；无说明时为 null。
        /// </summary>
        public string? ReclaimNote { get; set; }
    }
}
