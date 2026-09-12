namespace StarPie.PluginRuntime.Lifecycle
{
    /// <summary>一次已完成的生命周期转移（诊断与测试的观察面）。</summary>
    public sealed record PluginLifecycleTransition(PluginLifecycleState From, PluginLifecycleState To);
}
