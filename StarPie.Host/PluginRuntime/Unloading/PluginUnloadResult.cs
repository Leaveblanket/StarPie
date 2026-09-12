using System.Collections.Generic;
using StarPie.PluginRuntime.Diagnostics;
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
        IReadOnlyList<string> Diagnostics)
    {
        /// <summary>
        /// 资源是否已回收：作用域已释放且句柄账本清零、阻断性残留清零。硬判策略下阻断性残留是
        /// 入口实例、ALC 与入口程序集；降级策略下 ALC 与程序集残留只记诊断（宿主框架缓存程序集），
        /// 请求同样视同耗尽。隔离结论与资源回收是两件事——隔离终态续做回收可以回收成功而结论仍是隔离；
        /// 显式重试据此判断能否在同一个进程里重新装载。
        /// </summary>
        public bool Reclaimed { get; init; }

        /// <summary>
        /// 可定位的残留清单（类别 + 类型/程序集全名或计数）；回收失败时非空。
        /// 降级判定下非空不代表隔离：宿主框架缓存程序集的现场同样记录在此，供管理面展示。
        /// </summary>
        public IReadOnlyList<PluginResidual> Residuals { get; init; } = System.Array.Empty<PluginResidual>();

        /// <summary>
        /// 降级判定的回收说明（宿主框架缓存程序集时非空）；硬判与完全回收时为 null。
        /// 与 <see cref="FailureReason"/> 分开：前者是预期现场的解释，后者是隔离原因。
        /// </summary>
        public string? ReclaimNote { get; init; }
    }
}
