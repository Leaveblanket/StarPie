using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarPie.Tests;

/// <summary>
/// 导航目录的插件页契约：插件页追加在固定槽位之后、按稳定标识生成 AutomationId、
/// 摘除后目录不再含该页；固定页注册面保持原样。
/// </summary>
public sealed class NavigationCatalogPluginPageTests
{
    private const string PluginId = "com.example.ui";
    private const string Identifier = "NavPlugin_com.example.ui_main";
    private const string AutomationId = "NavPlugin_com.example.ui_main";

    private sealed class PluginPageViewModel : ObservableObject { }

    private static NavigationCatalog CreateCatalogWithFixedPages()
    {
        var catalog = new NavigationCatalog();
        foreach (NavigationSlot slot in NavigationSlots.All)
        {
            catalog.RegisterPage<PluginPageViewModel>(
                slot, NavigationSlots.GetAutomationId(slot), "Page" + slot, string.Empty);
        }

        return catalog;
    }

    [Fact]
    public void 注册插件页_追加在固定槽位之后()
    {
        NavigationCatalog catalog = CreateCatalogWithFixedPages();

        catalog.RegisterPluginPage(
            PluginId, Identifier, AutomationId, "PluginPageTitle", "M0 0",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        Assert.Equal(
            new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4", AutomationId },
            catalog.Entries.Select(entry => entry.AutomationId));
    }

    [Fact]
    public void 摘除插件页_目录不再含该页且固定页不动()
    {
        NavigationCatalog catalog = CreateCatalogWithFixedPages();
        catalog.RegisterPluginPage(
            PluginId, Identifier, AutomationId, "PluginPageTitle", "M0 0",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        catalog.RemovePluginPage(AutomationId);

        Assert.DoesNotContain(catalog.Entries, entry => entry.AutomationId == AutomationId);
        Assert.Equal(
            new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4" },
            catalog.Entries.Select(entry => entry.AutomationId));
    }

    [Fact]
    public void 插件页注册项_带插件归属与标识且无固定槽位()
    {
        NavigationCatalog catalog = CreateCatalogWithFixedPages();

        NavigationPageRegistration entry = catalog.RegisterPluginPage(
            PluginId, Identifier, AutomationId, "PluginPageTitle", "M0 0",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        Assert.Equal(PluginId, entry.PluginId);
        Assert.Equal(AutomationId, entry.Identifier);
        Assert.Null(entry.Slot);
        Assert.Contains(entry, catalog.Entries);
    }

    [Fact]
    public void 注册插件页_AutomationId重复即拒绝()
    {
        NavigationCatalog catalog = CreateCatalogWithFixedPages();
        catalog.RegisterPluginPage(
            PluginId, Identifier, AutomationId, "PluginPageTitle", "M0 0",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        Assert.Throws<InvalidOperationException>(() => catalog.RegisterPluginPage(
            PluginId, "NavPlugin_com.example.ui_second", AutomationId, "PluginPageTitle", "M0 0",
            typeof(PluginPageViewModel), () => new PluginPageViewModel()));
    }

    [Fact]
    public void 两个插件页并存_按注册顺序排在固定页之后()
    {
        NavigationCatalog catalog = CreateCatalogWithFixedPages();
        catalog.RegisterPluginPage(
            "com.example.a", "NavPlugin_com.example.a_main", "NavPlugin_com.example.a_main", "PageA", "",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());
        catalog.RegisterPluginPage(
            "com.example.b", "NavPlugin_com.example.b_main", "NavPlugin_com.example.b_main", "PageB", "",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        Assert.Equal(
            new[]
            {
                "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4",
                "NavPlugin_com.example.a_main", "NavPlugin_com.example.b_main",
            },
            catalog.Entries.Select(entry => entry.AutomationId));
    }
}
