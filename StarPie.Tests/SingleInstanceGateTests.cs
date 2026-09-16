using StarPie.ShellIntegration;

namespace StarPie.Tests;

/// <summary>
/// 单实例闸门处置决策的真值表（#169）：输入是新实例与既有实例的权限态，输出是闸门的处置方式。
/// 判定严格单向——只有"新实例提权 + 既有实例非提权"请求让位；其余三格一律置前并退出。
/// </summary>
public sealed class SingleInstanceGateTests
{
    [Theory]
    [InlineData(true, false, SingleInstanceGateDecision.RequestHandover)]   // 唯一产生权限收益的一格
    [InlineData(true, true, SingleInstanceGateDecision.ForegroundAndExit)]  // 同级别相遇：接管无收益却要付交接代价
    [InlineData(false, true, SingleInstanceGateDecision.ForegroundAndExit)] // 反向绝不让位：否则一次误双击就撤销提权态
    [InlineData(false, false, SingleInstanceGateDecision.ForegroundAndExit)]
    public void Resolve_IsStrictlyOneWay(
        bool newInstanceElevated, bool existingInstanceElevated, SingleInstanceGateDecision expected)
    {
        Assert.Equal(expected, SingleInstanceGate.Resolve(newInstanceElevated, existingInstanceElevated));
    }

    [Fact]
    public void DecisionDomain_IsExactlyThreeStates()
    {
        // 值域三态即闸门的全部出口：置前退出、请求让位接管、退出并告知提权未生效。
        // 第三态不由 Resolve 产生（它是"已请求让位但没等到"的运行期失败出口），但它是值域的一部分：
        // 闸门的调用点必须显式列出它，不得靠"其余都当置前退出"的兜底把它吞掉。
        Assert.Equal(
            new[]
            {
                SingleInstanceGateDecision.ForegroundAndExit,
                SingleInstanceGateDecision.RequestHandover,
                SingleInstanceGateDecision.ExitAndNotifyElevationFailed,
            },
            Enum.GetValues<SingleInstanceGateDecision>());
    }

    // --- 接收端：已有实例只在非提权态受理让位（严格单向的另一面） ---------------------

    [Theory]
    [InlineData(false, true)]  // 非提权实例让位：接管的目的就是提升权限
    [InlineData(true, false)]  // 提权实例永不让位：受理让位会让"降权"成为可能
    public void AcceptsHandoverRequest_OnlyWhenNotElevated(bool existingElevated, bool expected)
    {
        Assert.Equal(expected, SingleInstanceGate.AcceptsHandoverRequest(existingElevated));
    }

    // --- 接管尝试的结果并入决策：等不到让位即转"退出并告知未生效" ---------------------

    [Theory]
    [InlineData(SingleInstanceGateDecision.RequestHandover, true, SingleInstanceGateDecision.RequestHandover)]
    [InlineData(SingleInstanceGateDecision.RequestHandover, false, SingleInstanceGateDecision.ExitAndNotifyElevationFailed)]
    [InlineData(SingleInstanceGateDecision.ForegroundAndExit, true, SingleInstanceGateDecision.ForegroundAndExit)]
    [InlineData(SingleInstanceGateDecision.ForegroundAndExit, false, SingleInstanceGateDecision.ForegroundAndExit)]
    [InlineData(SingleInstanceGateDecision.ExitAndNotifyElevationFailed, false, SingleInstanceGateDecision.ExitAndNotifyElevationFailed)]
    public void ApplyHandoverOutcome_OnlyTurnsAFailedHandoverIntoTheFailureExit(
        SingleInstanceGateDecision decision, bool takeoverSucceeded, SingleInstanceGateDecision expected)
    {
        // 请求让位未成（请求送不出去，或等不到已有实例释放单实例互斥体）→ 本次提权作废，
        // 新实例按时限退出并告知；其余决策不受接管尝试的结果影响。
        Assert.Equal(expected, SingleInstanceGate.ApplyHandoverOutcome(decision, takeoverSucceeded));
    }
}
