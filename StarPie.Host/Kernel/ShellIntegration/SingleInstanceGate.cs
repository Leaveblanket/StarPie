namespace StarPie.Kernel.ShellIntegration
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
    /// 三态中的 <see cref="SingleInstanceGateDecision.ExitAndNotifyElevationFailed"/> 不由本函数产生：
    /// 它是"已请求让位、但没有等到让位"这一运行期失败的出口（失败判据不得取自触发命令的退出码）。
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
    }
}
