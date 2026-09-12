namespace StarPie.PluginRuntime.State
{
    /// <summary>
    /// 插件的隔离状态：进入隔离即写宿主状态，本次进程不再调用该插件，重启也不自动重试。
    /// </summary>
    public sealed record PluginQuarantineState(string Reason, DateTimeOffset Since);
}
