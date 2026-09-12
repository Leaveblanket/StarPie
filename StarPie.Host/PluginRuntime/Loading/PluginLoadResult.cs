using StarPie.Abstractions;
using StarPie.PluginRuntime.Lifecycle;

namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 一次装载尝试的结果：结果三态、失败原因、入口实例与本次装载的 ALC 和生命周期机。
    /// </summary>
    /// <param name="PluginId">插件 id（清单 id；清单不可用时回落包目录名）。</param>
    /// <param name="Status">装载结果三态。</param>
    /// <param name="FailureReason">拒绝或隔离的可读原因；成功时为 null。</param>
    /// <param name="Plugin">入口实例；仅活动态非 null（失败与拒绝都不保留插件对象）。</param>
    /// <param name="LoadContext">本次装载创建的 ALC；拒绝路径为 null，隔离时非 null 以便卸载回收。</param>
    /// <param name="Lifecycle">本次装载的状态机，保留已完成的转移与隔离原因。</param>
    /// <remarks>
    /// 装载期解析出的入口 <see cref="Type"/> 不进入宿主长生命周期结构：结果只持有
    /// <see cref="IPlugin"/> 实例与 ALC，入口类型每次装载都从新 ALC 重新解析。
    /// </remarks>
    public sealed record PluginLoadResult(
        string PluginId,
        PluginLoadStatus Status,
        string? FailureReason,
        IPlugin? Plugin,
        PluginLoadContext? LoadContext,
        PluginLifecycleStateMachine Lifecycle);
}
