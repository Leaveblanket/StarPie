using System;
using StarPie;

namespace StarPie.Tests;

/// <summary>
/// 主题服务状态覆盖：经注入深色探针的"跟随系统"解析、命名主题透传、CurrentEffectiveTheme
/// 生命周期、SetTheme 单一入口/调色板应用回调与 null 窗口安全。画刷应用属视图层，
/// 由 Python 端到端套件覆盖，不在此处。
/// </summary>
public sealed class ThemeServiceTests
{
    [Fact]
    public void CurrentEffectiveTheme_DefaultsToLight_BeforeAnySetTheme()
    {
        var service = new ThemeService(() => true);

        Assert.Equal("Light", service.CurrentEffectiveTheme);
    }

    [Fact]
    public void ResolveEffectiveTheme_System_FollowsInjectedProbe()
    {
        var darkService = new ThemeService(() => true);
        var lightService = new ThemeService(() => false);

        Assert.Equal("Dark", darkService.ResolveEffectiveTheme("System"));
        Assert.Equal("Light", lightService.ResolveEffectiveTheme("System"));
        Assert.Equal("Dark", darkService.ResolveEffectiveTheme("system")); // 兼容旧配置小写值
    }

    [Fact]
    public void ResolveEffectiveTheme_Empty_FollowsProbe()
    {
        var service = new ThemeService(() => true);

        Assert.Equal("Dark", service.ResolveEffectiveTheme(""));
        Assert.Equal("Dark", service.ResolveEffectiveTheme(null!));
    }

    [Fact]
    public void ResolveEffectiveTheme_NamedTheme_PassesThroughUnchanged()
    {
        var service = new ThemeService(() => false);

        Assert.Equal("MidnightNavy", service.ResolveEffectiveTheme("MidnightNavy"));
        Assert.Equal("RoyalViolet", service.ResolveEffectiveTheme("RoyalViolet"));
        Assert.Equal("TitaniumGray", service.ResolveEffectiveTheme("TitaniumGray"));
    }

    [Fact]
    public void IsWindowsInDarkTheme_DelegatesToProbe()
    {
        var service = new ThemeService(() => true);

        Assert.True(service.IsWindowsInDarkTheme());
    }

    [Fact]
    public void SetTheme_ResolvesStateAndShortCircuitsOnSameTheme()
    {
        var service = new ThemeService(() => true);
        var applied = new List<string>();
        service.AttachPaletteApplier(applied.Add);

        service.SetTheme("MidnightNavy");
        Assert.Equal("MidnightNavy", service.CurrentEffectiveTheme);
        Assert.Single(applied);

        service.SetTheme("MidnightNavy"); // 同主题 no-op：不重复换入/广播
        Assert.Single(applied);

        service.SetTheme("System"); // probe dark → Dark
        Assert.Equal("Dark", service.CurrentEffectiveTheme);
        Assert.Equal(2, applied.Count);
    }

    [Fact]
    public void SetTheme_FirstApply_ExecutesEvenWhenEffectiveThemeMatchesInitialLight()
    {
        var service = new ThemeService(() => false);
        var applied = new List<string>();
        service.AttachPaletteApplier(applied.Add);

        service.SetTheme("Light"); // 首次应用：状态虽同默认仍执行换入，保证 manager 调色板入槽
        Assert.Equal("Light", service.CurrentEffectiveTheme);
        Assert.Single(applied);

        service.SetTheme("Light");
        Assert.Single(applied);
    }

    [Fact]
    public void ApplyWindowTheme_NullElement_IsSafeAndKeepsStateUnchanged()
    {
        var service = new ThemeService(() => true);
        service.SetTheme("MidnightNavy");

        service.ApplyWindowTheme(null);

        Assert.Equal("MidnightNavy", service.CurrentEffectiveTheme);
    }

    [Fact]
    public void RefreshSystemTheme_WhenFollowingSystem_ReResolvesOnProbeChange()
    {
        bool dark = false;
        var service = new ThemeService(() => dark);
        var applied = new List<string>();
        service.AttachPaletteApplier(applied.Add);

        service.SetTheme("System"); // probe light
        Assert.Equal("Light", service.CurrentEffectiveTheme);
        Assert.Single(applied);

        dark = true; // 模拟 Windows 深浅色切换
        service.RefreshSystemTheme();
        Assert.Equal("Dark", service.CurrentEffectiveTheme);
        Assert.Equal(2, applied.Count);

        service.RefreshSystemTheme(); // 系统未再变化 → no-op
        Assert.Equal(2, applied.Count);
    }

    [Fact]
    public void RefreshSystemTheme_WhenFixedTheme_DoesNothing()
    {
        bool dark = false;
        var service = new ThemeService(() => dark);
        var applied = new List<string>();
        service.AttachPaletteApplier(applied.Add);

        service.SetTheme("MidnightNavy");
        Assert.Single(applied);

        dark = true;
        service.RefreshSystemTheme(); // 固定主题不跟随系统
        Assert.Equal("MidnightNavy", service.CurrentEffectiveTheme);
        Assert.Single(applied);
    }
}
