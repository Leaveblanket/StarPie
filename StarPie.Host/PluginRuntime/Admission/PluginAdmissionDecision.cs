namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 一次准入判定的结果：四态之一 + 可读原因（拒绝时即拒绝理由）。
    /// </summary>
    public sealed record PluginAdmissionDecision(PluginAdmission Status, string Reason);
}
