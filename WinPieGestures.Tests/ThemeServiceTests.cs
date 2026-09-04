using System;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// Theme-service state coverage (T09/ADR-0013 #47): "follow system" resolution through the
/// injected dark-mode probe, named-theme passthrough, the CurrentEffectiveTheme lifecycle,
/// the SetTheme single-entry/ThemeChanged contract and null-window safety. Brush application
/// itself is view-layer and covered by the Python end-to-end suite, not here.
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
        Assert.Equal("Dark", darkService.ResolveEffectiveTheme("system")); // legacy lowercase config value
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
        int fired = 0;
        service.ThemeChanged += () => fired++;

        service.SetTheme("MidnightNavy");
        Assert.Equal("MidnightNavy", service.CurrentEffectiveTheme);
        Assert.Equal(1, fired);

        service.SetTheme("MidnightNavy"); // 同主题 no-op：不重复换入/广播
        Assert.Equal(1, fired);

        service.SetTheme("System"); // probe dark → Dark
        Assert.Equal("Dark", service.CurrentEffectiveTheme);
        Assert.Equal(2, fired);
    }

    [Fact]
    public void SetTheme_FirstApply_ExecutesEvenWhenEffectiveThemeMatchesInitialLight()
    {
        var service = new ThemeService(() => false);
        int fired = 0;
        service.ThemeChanged += () => fired++;

        service.SetTheme("Light"); // 首次应用：状态虽同默认仍执行换入，保证 manager 调色板入槽
        Assert.Equal("Light", service.CurrentEffectiveTheme);
        Assert.Equal(1, fired);

        service.SetTheme("Light");
        Assert.Equal(1, fired);
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
