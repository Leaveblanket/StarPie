namespace StarPie.PluginRuntime.Lifecycle
{
    /// <summary>一次已完成的生命周期转移（诊断与测试的观察面）。</summary>
    /// <param name="From">转移前状态。</param>
    /// <param name="To">转移后状态。</param>
    public sealed record PluginLifecycleTransition(PluginLifecycleState From, PluginLifecycleState To);
}
