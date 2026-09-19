using CommunityToolkit.Mvvm.ComponentModel;
using StarPie.Host.Localization;
using StarPie.Ui.ViewModels.Navigation;

namespace StarPie.Tests;

/// <summary>
/// 导航项标题的来源：给了显示名即字面量，否则按宿主文案键取词。
/// </summary>
/// <remarks>
/// 语言切换下的重取不变由 <see cref="PluginSurfaceTitleTests"/> 与
/// <see cref="NavigationViewModelTests.LanguageChanged_RefreshesItemTitles"/> 承担。
/// </remarks>
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
    public void 给了显示名_构造期标题即字面量()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("zh-CN");
        NavigationItemViewModel item = Create("ThemeLight", "示例页", localization);

        Assert.Equal("示例页", item.Title);
    }
}
