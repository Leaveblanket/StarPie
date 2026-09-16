using StarPie.ShellIntegration;

namespace StarPie.Tests;

/// <summary>
/// 提权自启任务的 schtasks 参数形状锁定（ADR-0041）。这几项各有一个不可丢的语义，
/// 而写错的失败方式是静默的（开机不自启、或每次都弹 UAC），故逐项断言：
/// <c>/sc onlogon</c> 决定触发时机、<c>/rl highest</c> 决定提权由任务计划程序服务完成（不弹 UAC）、
/// <c>/delay 0000:00</c> 消掉任务计划程序默认的登录延迟、<c>/f</c> 允许覆盖以刷新可执行文件路径。
/// </summary>
public sealed class AutostartRegistryTests
{
    private const string ExePath = @"C:\Tools\StarPie\StarPie.exe";
    private const string TaskName = "StarPie_AdminAutoStart";

    [Fact]
    public void BuildAdminTaskCreateArguments_CarriesTriggerRunLevelDelayAndOverwrite()
    {
        string arguments = AutostartRegistry.BuildAdminTaskCreateArguments(ExePath, TaskName);

        Assert.Contains($"/create /tn \"{TaskName}\"", arguments);
        Assert.Contains("/sc onlogon", arguments);
        Assert.Contains("/rl highest", arguments);
        Assert.Contains("/delay 0000:00", arguments);
        Assert.Contains("/f", arguments);
    }

    [Fact]
    public void BuildAdminTaskCreateArguments_QuotesExecutablePathAndPassesNoAppArguments()
    {
        // 路径连空格必须整体加引号；任务命令行不带额外参数——与注册表自启形态只差权限级别。
        string arguments = AutostartRegistry.BuildAdminTaskCreateArguments(ExePath, TaskName);

        Assert.Contains($"/tr \"\\\"{ExePath}\\\"\"", arguments);
        Assert.DoesNotContain("--", arguments);
    }

    [Fact]
    public void BuildAdminTaskDeleteArguments_TargetsTheTaskAndSkipsConfirmation()
    {
        // /f 必需：否则 schtasks 会等一个交互确认，后台调用会挂住。
        Assert.Equal("/delete /tn \"StarPie_AdminAutoStart\" /f", AutostartRegistry.BuildAdminTaskDeleteArguments(TaskName));
    }

    [Fact]
    public void BuildAdminTaskQueryArguments_TargetsTheTask()
    {
        Assert.Equal("/query /tn \"StarPie_AdminAutoStart\"", AutostartRegistry.BuildAdminTaskQueryArguments(TaskName));
    }

    [Fact]
    public void BuildAdminTaskRunArguments_TargetsTheTaskWithoutChangingItsRunLevel()
    {
        // 权限级别是任务自身的属性（/rl highest），触发方只请求运行——即时提权复用的就是这颗任务。
        // 带 /tn 指名任务；不带 runas 类选项（触发不需要提权，故不弹 UAC）。
        Assert.Equal("/run /tn \"StarPie_AdminAutoStart\"", AutostartRegistry.BuildAdminTaskRunArguments(TaskName));
    }

    [Fact]
    public void AdminTaskName_KeepsReleaseNamePrefix_DevInstanceOnlyAddsSuffix()
    {
        // dev 实例只加后缀，绝不与正式版共用任务（否则 dev 构建会覆盖正式版的自启形态）。
        Assert.StartsWith("StarPie_AdminAutoStart", AutostartRegistry.AdminTaskName);
    }

    // --- 两条权限路线的落位互斥 ---------------------------------------------

    [Theory]
    [InlineData(false, false, false, false)] // 总开关关：两种形态都不落位
    [InlineData(true, false, true, false)]   // 普通权限自启：只写注册表键
    [InlineData(true, true, false, true)]    // 管理员权限静默自启：只留计划任务，注册表键必须缺位
    public void ResolvePlacement_KeepsExactlyOneRoute(
        bool enable, bool asAdmin, bool expectedWriteRunKey, bool expectedWantAdminTask)
    {
        // 提权形态下注册表键必须缺位：两条自启路径同在登录时触发，同时落位会让非提权实例
        // 与提权实例抢单实例互斥体，非提权那个赢了就等于提权自启白开。
        var placement = AutostartRegistry.ResolvePlacement(enable, asAdmin);

        Assert.Equal(expectedWriteRunKey, placement.WriteRunKey);
        Assert.Equal(expectedWantAdminTask, placement.WantAdminTask);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ResolvePlacement_NeverPlacesBothRoutes(bool enable, bool asAdmin)
    {
        var placement = AutostartRegistry.ResolvePlacement(enable, asAdmin);

        Assert.False(placement.WriteRunKey && placement.WantAdminTask);
    }

    // --- 「立即提权」入口的可见性与可点性 -----------------------------------------

    [Theory]
    [InlineData(false, true, true, true)]    // 非提权 + 任务在：入口出现且可点
    [InlineData(false, false, true, false)]  // 非提权 + 任务不在：入口出现但不可点（要说明原因）
    [InlineData(true, true, false, true)]    // 提权态：入口不出现（对提权实例没有意义）
    [InlineData(true, false, false, false)]
    public void ResolveAdminRestartEntry_HidesWhenElevatedAndDisablesWithoutTheTask(
        bool elevated, bool adminTaskExists, bool expectedVisible, bool expectedEnabled)
    {
        // 入口就是提权自启那颗任务的即时触发，故两条口径都从它直接推出：
        // 提权态下不出现；任务不在时不可点——没有可复用的任务就没有这条路，
        // 也不做"临时提权、用完删任务"的第三种形态。
        (bool visible, bool enabled) = AutostartRegistry.ResolveAdminRestartEntry(elevated, adminTaskExists);

        Assert.Equal(expectedVisible, visible);
        Assert.Equal(expectedEnabled, enabled);
    }
}
