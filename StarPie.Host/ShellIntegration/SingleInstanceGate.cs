namespace StarPie.Host.ShellIntegration
{
    /// <summary>新实例发现已有实例在运行时的处置方式（值域三态）。</summary>
    public enum SingleInstanceGateDecision
    {
        /// <summary>把已运行的实例置前，本实例退出。</summary>
        ForegroundAndExit,

        /// <summary>请求已运行的实例让位，本实例接替成为唯一实例。</summary>
        RequestHandover,

        /// <summary>本实例退出，并经握手信道把"本次提权尝试未生效"告知已运行的实例，
        /// 由它给出用户可见的提示（本实例即将退出，无处呈现）。</summary>
        ExitAndNotifyElevationFailed,
    }

    /// <summary>
    /// 单实例闸门的处置决策（纯函数）：新实例发现已有实例在运行时，按两者的权限态决定怎么办。
    /// </summary>
    /// <remarks>
    /// 判定**严格单向**：接管的目的只有"提升权限"一种，故只有"新实例提权 + 已有实例非提权"请求让位。
    /// 反向绝不让位——非提权新实例遇到提权已有实例时若让位，一次误双击就能把用户的提权状态撤销掉；
    /// 同一提权级别相遇同样置前退出，接管不产生任何权限收益，却要付一次进程交接的代价。
    /// 三态中的 <see cref="SingleInstanceGateDecision.ExitAndNotifyElevationFailed"/> 不由 <see cref="Resolve"/>
    /// 产生：它是"已请求让位、但没有等到让位"这一运行期失败的出口（见 <see cref="ApplyHandoverOutcome"/>），
    /// 而失败判据不得取自触发命令的退出码——就绪只能是"单实例互斥体已可取得"。
    /// 真值表由 <c>SingleInstanceGateTests</c> 逐格锁定。
    /// </remarks>
    public static class SingleInstanceGate
    {
        /// <summary>按新实例与已有实例的权限态给出处置决策。</summary>
        /// <param name="newInstanceElevated">本次启动的实例是否以管理员身份运行。</param>
        /// <param name="existingInstanceElevated">已运行的实例是否以管理员身份运行。</param>
        public static SingleInstanceGateDecision Resolve(bool newInstanceElevated, bool existingInstanceElevated)
            => newInstanceElevated && !existingInstanceElevated
                ? SingleInstanceGateDecision.RequestHandover
                : SingleInstanceGateDecision.ForegroundAndExit;

        /// <summary>
        /// 已有实例是否受理让位请求（接收端的单向规则）：只有非提权实例让位。提权实例受理让位
        /// 会让"降权"成为可能——同一规则的另一面，故与 <see cref="Resolve"/> 同处一个正典。
        /// </summary>
        /// <param name="existingInstanceElevated">已运行的实例是否以管理员身份运行。</param>
        public static bool AcceptsHandoverRequest(bool existingInstanceElevated) => !existingInstanceElevated;

        /// <summary>
        /// 把接管尝试的结果并入处置决策：请求让位未成（请求送不出去，或等不到已有实例释放单实例
        /// 互斥体）即转 <see cref="SingleInstanceGateDecision.ExitAndNotifyElevationFailed"/>，
        /// 其余决策不受尝试结果影响。
        /// </summary>
        /// <param name="decision">按权限态得出的决策。</param>
        /// <param name="takeoverSucceeded">接管尝试是否成功（等到了互斥体释放）。</param>
        public static SingleInstanceGateDecision ApplyHandoverOutcome(
            SingleInstanceGateDecision decision, bool takeoverSucceeded)
            => decision == SingleInstanceGateDecision.RequestHandover && !takeoverSucceeded
                ? SingleInstanceGateDecision.ExitAndNotifyElevationFailed
                : decision;
    }
}
