using System.Collections.Generic;
using StarPie.PluginRuntime.Lifecycle;

namespace StarPie.PluginRuntime.Unloading
{
    /// <summary>卸载结果：终态、失败原因（可读文本）与诊断清单（回收判定与残留线索）。</summary>
    /// <param name="PluginId">插件 id。</param>
    /// <param name="Status">卸载结果两态。</param>
    /// <param name="FailureReason">隔离原因；成功时为 null。</param>
    /// <param name="Lifecycle">该插件的状态机（保留转移到已卸载或已隔离的过程）。</param>
    /// <param name="Diagnostics">逐步诊断行（配置落盘/能力摘除/在途/停用/作用域/回收判定）。</param>
    public sealed record PluginUnloadResult(
        string PluginId,
        PluginUnloadStatus Status,
        string? FailureReason,
        PluginLifecycleStateMachine Lifecycle,
        IReadOnlyList<string> Diagnostics);
}
