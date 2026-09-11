using System;
using System.Collections.Generic;

namespace StarPie.Tests;

/// <summary>
/// 内核主题引擎状态覆盖：经注入深色探针的"跟随系统"解析、命名主题透传、
/// CurrentEffectiveTheme 生命周期、SetTheme 单一入口与换肤端口调用。
/// 窗口 DWM 应用与系统深浅色监听属 Ui 侧适配（见 ThemeServiceTests），
/// 画刷换入属视图层，由 Python 端到端套件覆盖。
/// </summary>
public sealed class ThemeEngineTests
{
    [Fact]
    public void CurrentEffectiveTheme_DefaultsToLight_BeforeAnySetTheme()
    {
        var engine = new ThemeEngine(() => true);

        Assert.Equal("Light", engine.CurrentEffectiveTheme);
        Assert.Equal("System", engine.RequestedTheme);
    }

    [Fact]
    public void ResolveEffectiveTheme_System_FollowsInjectedProbe()
    {
        var darkEngine = new ThemeEngine(() => true);
        var lightEngine = new ThemeEngine(() => false);

        Assert.Equal("Dark", darkEngine.ResolveEffectiveTheme("System"));
        Assert.Equal("Light", lightEngine.ResolveEffectiveTheme("System"));
        Assert.Equal("Dark", darkEngine.ResolveEffectiveTheme("system")); // 兼容旧配置小写值
    }

    [Fact]
    public void ResolveEffectiveTheme_Empty_FollowsProbe()
    {
        var engine = new ThemeEngine(() => true);

        Assert.Equal("Dark", engine.ResolveEffectiveTheme(""));
        Assert.Equal("Dark", engine.ResolveEffectiveTheme(null!));
    }

    [Fact]
    public void ResolveEffectiveTheme_NamedTheme_PassesThroughUnchanged()
    {
        var engine = new ThemeEngine(() => false);

        Assert.Equal("MidnightNavy", engine.ResolveEffectiveTheme("MidnightNavy"));
        Assert.Equal("RoyalViolet", engine.ResolveEffectiveTheme("RoyalViolet"));
        Assert.Equal("TitaniumGray", engine.ResolveEffectiveTheme("TitaniumGray"));
    }

    [Fact]
    public void ResolveEffectiveTheme_WithoutProbe_ResolvesToKnownTheme()
    {
        // 生产默认探针实时读 Windows 设置：无论深浅，解析结果只可能是两个固定名之一。
        var engine = new ThemeEngine();

        Assert.Contains(engine.ResolveEffectiveTheme("System"), new[] { "Dark", "Light" });
    }

    [Fact]
    public void IsWindowsInDarkTheme_DelegatesToProbe()
    {
        var engine = new ThemeEngine(() => true);

        Assert.True(engine.IsWindowsInDarkTheme());
    }

    [Fact]
    public void SetTheme_ResolvesStateAndShortCircuitsOnSameTheme()
    {
        var engine = new ThemeEngine(() => true);
        var applied = new TestThemeApplier();
        engine.AttachApplier(applied);

        engine.SetTheme("MidnightNavy");
        Assert.Equal("MidnightNavy", engine.CurrentEffectiveTheme);
        Assert.Single(applied.Themes);

        engine.SetTheme("MidnightNavy"); // 同主题 no-op：不重复换入/广播
        Assert.Single(applied.Themes);

        engine.SetTheme("System"); // probe dark → Dark
        Assert.Equal("Dark", engine.CurrentEffectiveTheme);
        Assert.Equal(2, applied.Themes.Count);
    }

    [Fact]
    public void SetTheme_FirstApply_ExecutesEvenWhenEffectiveThemeMatchesInitialLight()
    {
        var engine = new ThemeEngine(() => false);
        var applied = new TestThemeApplier();
        engine.AttachApplier(applied);

        engine.SetTheme("Light"); // 首次应用：状态虽同默认仍执行换入，保证调色板入槽
        Assert.Equal("Light", engine.CurrentEffectiveTheme);
        Assert.Single(applied.Themes);

        engine.SetTheme("Light");
        Assert.Single(applied.Themes);
    }

    [Fact]
    public void SetTheme_WithoutApplier_StillTracksState()
    {
        var engine = new ThemeEngine(() => true);

        engine.SetTheme("System");

        Assert.Equal("Dark", engine.CurrentEffectiveTheme);
        Assert.Equal("System", engine.RequestedTheme);
    }

    [Fact]
    public void RefreshSystemTheme_WhenFollowingSystem_ReResolvesOnProbeChange()
    {
        bool dark = false;
        var engine = new ThemeEngine(() => dark);
        var applied = new TestThemeApplier();
        engine.AttachApplier(applied);

        engine.SetTheme("System"); // probe light
        Assert.Equal("Light", engine.CurrentEffectiveTheme);
        Assert.Single(applied.Themes);

        dark = true; // 模拟 Windows 深浅色切换
        engine.RefreshSystemTheme();
        Assert.Equal("Dark", engine.CurrentEffectiveTheme);
        Assert.Equal(2, applied.Themes.Count);

        engine.RefreshSystemTheme(); // 系统未再变化 → no-op
        Assert.Equal(2, applied.Themes.Count);
    }

    [Fact]
    public void RefreshSystemTheme_WhenFixedTheme_DoesNothing()
    {
        bool dark = false;
        var engine = new ThemeEngine(() => dark);
        var applied = new TestThemeApplier();
        engine.AttachApplier(applied);

        engine.SetTheme("MidnightNavy");
        Assert.Single(applied.Themes);

        dark = true;
        engine.RefreshSystemTheme(); // 固定主题不跟随系统
        Assert.Equal("MidnightNavy", engine.CurrentEffectiveTheme);
        Assert.Single(applied.Themes);
    }
}
