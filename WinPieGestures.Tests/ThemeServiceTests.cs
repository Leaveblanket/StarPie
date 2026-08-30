using System;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// Theme-service state coverage (T09): "follow system" resolution through the
/// injected dark-mode probe, named-theme passthrough, the CurrentEffectiveTheme
/// lifecycle, and null-element safety. Brush application itself is view-layer and
/// covered by the Python end-to-end suite, not here.
/// </summary>
public sealed class ThemeServiceTests
{
    [Fact]
    public void CurrentEffectiveTheme_DefaultsToLight_BeforeAnyApply()
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
    public void ApplyTheme_NullElement_IsSafeAndKeepsStateUnchanged()
    {
        var service = new ThemeService(() => true);

        service.ApplyTheme(null, "MidnightNavy");

        Assert.Equal("Light", service.CurrentEffectiveTheme);
    }
}
