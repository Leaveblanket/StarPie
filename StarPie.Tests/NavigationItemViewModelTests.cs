using CommunityToolkit.Mvvm.ComponentModel;
using StarPie.Kernel.Localization;
using StarPie.PluginHosting.Extensions;
using StarPie.ViewModels.Navigation;

namespace StarPie.Tests;

/// <summary>
/// 导航项标题的来源：给了显示名即字面量（语言切换后重取取值不变），
/// 否则按宿主文案键取词；语言切换的重取由主框架 VM 引同一解析规则完成。
/// </summary>
public sealed class NavigationItemViewModelTests
{
    private sealed class TargetPage : ObservableObject { }

    private static NavigationItemViewModel Create(
        string titleKey,
        string? displayName,
        ILocalizationService localization)
        => new(
            "NavPage0",
            titleKey,
            displayName,
            "M0 0",
            typeof(TargetPage),
            () => { },
            localization);

    [Fact]
    public void 未给显示名_标题按宿主文案键取词()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("zh-CN");

        NavigationItemViewModel item = Create("ThemeLight", null, localization);

        Assert.Null(item.DisplayName);
        Assert.Equal(localization.GetString("ThemeLight"), item.Title);
    }

    [Fact]
    public void 给了显示名_标题为字面量且语言切换后重取仍不变()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("zh-CN");
        NavigationItemViewModel item = Create("ThemeLight", "示例页", localization);

        Assert.Equal("示例页", item.Title);

        localization.SetLanguage("ja");

        Assert.Equal("示例页", PluginSurfaceTitle.Resolve(item.DisplayName, item.TitleKey, localization));
    }
}
