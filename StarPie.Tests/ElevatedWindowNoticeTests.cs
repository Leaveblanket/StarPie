using StarPie.Kernel.ShellIntegration;

namespace StarPie.Tests;

/// <summary>
/// 高权限窗口一次性告知的判据（ADR-0040 决策 6 / #165）：三条限定——一次性、提权态不报、
/// 探测未知不报。全部是 <see cref="ElevatedWindowNotice"/> 上的纯函数，不触碰系统调用。
/// </summary>
public sealed class ElevatedWindowNoticeTests
{
    [Fact]
    public void ShouldWatch_OnlyWhenNotElevatedAndNotYetReported()
    {
        Assert.True(ElevatedWindowNotice.ShouldWatch(isElevated: false, alreadyReported: false));
        Assert.False(ElevatedWindowNotice.ShouldWatch(isElevated: true, alreadyReported: false));
        Assert.False(ElevatedWindowNotice.ShouldWatch(isElevated: false, alreadyReported: true));
        Assert.False(ElevatedWindowNotice.ShouldWatch(isElevated: true, alreadyReported: true));
    }

    [Theory]
    [InlineData(true)]  // 前台确为更高完整性级别
    public void ShouldReport_NotElevatedNotReportedAndHigherIntegrity_Reports(bool higher)
    {
        Assert.True(ElevatedWindowNotice.ShouldReport(
            isElevated: false, alreadyReported: false, foregroundIsHigherIntegrity: higher));
    }

    [Fact]
    public void ShouldReport_ForegroundNotHigherOrUnknown_DoesNotReport()
    {
        // 低于/等于本进程：无内容可报。
        Assert.False(ElevatedWindowNotice.ShouldReport(
            isElevated: false, alreadyReported: false, foregroundIsHigherIntegrity: false));

        // 探测未知（无前台窗口/权限不足/进程已退出）：未知不是"低于"，但按未知提示就是凭空造噪音。
        Assert.False(ElevatedWindowNotice.ShouldReport(
            isElevated: false, alreadyReported: false, foregroundIsHigherIntegrity: null));
    }

    [Fact]
    public void ShouldReport_ElevatedOrAlreadyReported_DoesNotReport()
    {
        // 提权态：高权限窗口内手势本就可唤起，无可提示内容。
        Assert.False(ElevatedWindowNotice.ShouldReport(
            isElevated: true, alreadyReported: false, foregroundIsHigherIntegrity: true));

        // 每个安装只报一次：已提示过即不再报，哪怕前台仍是高权限窗口。
        Assert.False(ElevatedWindowNotice.ShouldReport(
            isElevated: false, alreadyReported: true, foregroundIsHigherIntegrity: true));
    }
}
