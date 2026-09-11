namespace StarPie.Tests;

/// <summary>
/// Ui 侧主题服务覆盖：状态与解析透传内核 <see cref="ThemeEngine"/>、换肤端口接线、
/// null 窗口安全。引擎状态机自身见 ThemeEngineTests；DWM 应用与系统深浅色监听
/// 属 WPF/WinRT 视图层，由 Python 端到端套件覆盖。
/// </summary>
public sealed class ThemeServiceTests
{
    [Fact]
    public void SetTheme_DelegatesStateToEngine()
    {
        var service = new ThemeService(() => true);

        service.SetTheme("System");

        Assert.Equal("Dark", service.CurrentEffectiveTheme);
        Assert.Equal("System", service.RequestedTheme);
        Assert.Equal("Dark", service.ResolveEffectiveTheme("system"));
        Assert.True(service.IsWindowsInDarkTheme());
    }

    [Fact]
    public void AttachApplier_ReceivesEngineApplications()
    {
        var service = new ThemeService(() => false);
        var applied = new TestThemeApplier();

        service.AttachApplier(applied);
        service.SetTheme("MidnightNavy");

        Assert.Equal(new[] { "MidnightNavy" }, applied.Themes);
    }

    [Fact]
    public void ApplyWindowTheme_NullElement_IsSafeAndKeepsStateUnchanged()
    {
        var service = new ThemeService(() => true);
        service.SetTheme("MidnightNavy");

        service.ApplyWindowTheme(null);

        Assert.Equal("MidnightNavy", service.CurrentEffectiveTheme);
    }
}
