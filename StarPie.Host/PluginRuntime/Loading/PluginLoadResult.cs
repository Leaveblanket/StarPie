using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.PluginRuntime.Lifecycle;

namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 一次装载尝试的结果：结果三态、失败原因、入口实例与本次装载的 ALC 和生命周期机。
    /// </summary>
    /// <remarks>
    /// 装载期解析出的入口 <see cref="Type"/> 不进入宿主长生命周期结构：结果只持有
    /// <see cref="IPlugin"/> 实例、ALC 与作用域，入口类型每次装载都从新 ALC 重新解析。
    /// 回收判定要求判定时刻不存在插件对象的强引用，因此插件对象的所有权只有一条路径：
    /// 卸载前经 <c>PluginUnloadRequest.FromLoaded</c> 交接，交接即清空本结果持有的入口实例、
    /// ALC 与作用域（<see cref="ReleaseForUnload"/>）——调用方无从继续经装载结果持有插件对象。
    /// </remarks>
    public sealed record PluginLoadResult
    {
        /// <summary>构造装载结果。</summary>
        /// <param name="pluginId">插件 id（清单 id；清单不可用时回落包目录名）。</param>
        /// <param name="status">装载结果三态。</param>
        /// <param name="failureReason">拒绝或隔离的可读原因；成功时为 null。</param>
        /// <param name="plugin">入口实例；仅活动态非 null（失败与拒绝都不保留插件对象）。</param>
        /// <param name="loadContext">本次装载创建的 ALC；拒绝路径为 null，隔离时非 null 以便卸载回收。</param>
        /// <param name="lifecycle">本次装载的状态机，保留已完成的转移与隔离原因。</param>
        /// <param name="scope">
        /// 本次装载的服务作用域；仅活动态非 null。卸载管线按"先释放作用域、再 Unload ALC"的顺序收口。
        /// </param>
        public PluginLoadResult(
            string pluginId,
            PluginLoadStatus status,
            string? failureReason,
            IPlugin? plugin,
            PluginLoadContext? loadContext,
            PluginLifecycleStateMachine lifecycle,
            PluginServiceScope? scope)
        {
            PluginId = pluginId;
            Status = status;
            FailureReason = failureReason;
            Plugin = plugin;
            LoadContext = loadContext;
            Lifecycle = lifecycle;
            Scope = scope;
        }

        /// <summary>插件 id（清单 id；清单不可用时回落包目录名）。</summary>
        public string PluginId { get; }

        /// <summary>装载结果三态。</summary>
        public PluginLoadStatus Status { get; }

        /// <summary>拒绝或隔离的可读原因；成功时为 null。</summary>
        public string? FailureReason { get; }

        /// <summary>入口实例；仅活动态非 null。交给卸载请求后为 null（见 <see cref="ReleaseForUnload"/>）。</summary>
        public IPlugin? Plugin { get; private set; }

        /// <summary>本次装载创建的 ALC。交给卸载请求后为 null（见 <see cref="ReleaseForUnload"/>）。</summary>
        public PluginLoadContext? LoadContext { get; private set; }

        /// <summary>本次装载的状态机，保留已完成的转移与隔离原因。</summary>
        public PluginLifecycleStateMachine Lifecycle { get; }

        /// <summary>
        /// 本次装载的服务作用域；仅活动态非 null。交给卸载请求后为 null（见 <see cref="ReleaseForUnload"/>）。
        /// </summary>
        public PluginServiceScope? Scope { get; private set; }

        /// <summary>
        /// 交出插件对象的所有权后清空本结果的三条强引用；由 <c>PluginUnloadRequest.FromLoaded</c> 调用——
        /// 这是"装载结果 → 卸载请求"交接的机械保障，调用方不应自行调用。
        /// </summary>
        internal void ReleaseForUnload()
        {
            Plugin = null;
            LoadContext = null;
            Scope = null;
        }
    }
}
