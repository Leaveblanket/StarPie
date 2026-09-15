using StarPie.Kernel.ShellIntegration;

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
    public void AdminTaskName_KeepsReleaseNamePrefix_DevInstanceOnlyAddsSuffix()
    {
        // dev 实例只加后缀，绝不与正式版共用任务（否则 dev 构建会覆盖正式版的自启形态）。
        Assert.StartsWith("StarPie_AdminAutoStart", AutostartRegistry.AdminTaskName);
    }
}
