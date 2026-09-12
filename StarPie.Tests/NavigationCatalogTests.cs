using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarPie.Tests;

/// <summary>
/// 导航目录与全局槽位表收口：0–3 槽位、NavPage0..3 正典、
/// 注册条目按槽位排序与缺失/重复/未知槽位拦截。只测外部行为——注册结果与校验异常，
/// 不测实现细节（直接 new，不经容器）。
/// </summary>
public sealed class NavigationCatalogTests
{
    private sealed class TriggerViewModel : ObservableObject { }
    private sealed class AppearanceViewModel : ObservableObject { }
    private sealed class GesturesViewModel : ObservableObject { }
    private sealed class AdvancedViewModel : ObservableObject { }
    private sealed class PluginsViewModel : ObservableObject { }

    /// <summary>故意打乱注册顺序：目录条目必须仍按槽位 0–4 返回。</summary>
    private static NavigationCatalog CreateFullCatalog()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<GesturesViewModel>(
            NavigationSlot.Gestures, NavigationSlots.GetAutomationId(NavigationSlot.Gestures), "PageGestures", "G");
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "T");
        catalog.RegisterPage<AppearanceViewModel>(
            NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "PageAppearance", "Ap");
        catalog.RegisterPage<AdvancedViewModel>(
            NavigationSlot.Advanced, NavigationSlots.GetAutomationId(NavigationSlot.Advanced), "PageAdvanced", "Ad");
        catalog.RegisterPage<PluginsViewModel>(
            NavigationSlot.Plugins, NavigationSlots.GetAutomationId(NavigationSlot.Plugins), "PagePlugins", "P");
        return catalog;
    }

    [Fact]
    public void NavigationSlots_CanonicalAutomationIds_AreNavPage0To4()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, NavigationSlots.All.Select(s => (int)s));
        Assert.Equal(new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4" },
            NavigationSlots.All.Select(NavigationSlots.GetAutomationId));
    }

    [Fact]
    public void RegisterAllSlots_EntriesSortedBySlot_WithCanonicalData()
    {
        var catalog = CreateFullCatalog();

        catalog.Validate(); // 全槽齐全：不应抛

        Assert.Equal(new[]
        {
            NavigationSlot.Trigger, NavigationSlot.Appearance, NavigationSlot.Gestures,
            NavigationSlot.Advanced, NavigationSlot.Plugins
        }, catalog.Entries.Select(e => e.Slot));
        Assert.Equal(new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4" },
            catalog.Entries.Select(e => e.AutomationId));
        Assert.Equal(new[]
        {
            "PageTrigger", "PageAppearance", "PageGestures", "PageAdvanced", "PagePlugins"
        }, catalog.Entries.Select(e => e.TitleKey));
        Assert.Equal(new[]
        {
            typeof(TriggerViewModel), typeof(AppearanceViewModel), typeof(GesturesViewModel),
            typeof(AdvancedViewModel), typeof(PluginsViewModel)
        }, catalog.Entries.Select(e => e.ViewModelType));
    }

    [Fact]
    public void Validate_MissingSlot_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");
        catalog.RegisterPage<GesturesViewModel>(
            NavigationSlot.Gestures, NavigationSlots.GetAutomationId(NavigationSlot.Gestures), "PageGestures", "");
        catalog.RegisterPage<AdvancedViewModel>(
            NavigationSlot.Advanced, NavigationSlots.GetAutomationId(NavigationSlot.Advanced), "PageAdvanced", "");

        var ex = Assert.Throws<InvalidOperationException>(() => catalog.Validate());

        Assert.Contains(nameof(NavigationSlot.Appearance), ex.Message);
    }

    [Fact]
    public void Register_DuplicateSlot_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            catalog.RegisterPage<AppearanceViewModel>(
                NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "PageAppearance", ""));

        Assert.Contains(nameof(NavigationSlot.Trigger), ex.Message);
    }

    [Fact]
    public void Register_DuplicateAutomationId_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");

        Assert.Throws<InvalidOperationException>(() =>
            catalog.RegisterPage<AppearanceViewModel>(
                NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageAppearance", ""));
    }

    [Fact]
    public void Register_UnknownSlot_Throws()
    {
        var catalog = new NavigationCatalog();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            catalog.RegisterPage<TriggerViewModel>((NavigationSlot)7, "NavPage7", "PageTrigger", ""));
    }

    [Fact]
    public void GetEntry_RegisteredSlot_ReturnsCatalogEntry()
    {
        var catalog = CreateFullCatalog();

        var entry = catalog.GetEntry(NavigationSlot.Gestures);

        Assert.Equal(NavigationSlot.Gestures, entry.Slot);
        Assert.Equal("NavPage2", entry.AutomationId);
        Assert.Equal(typeof(GesturesViewModel), entry.ViewModelType);
    }

    [Fact]
    public void GetEntry_UnregisteredSlot_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");

        Assert.Throws<InvalidOperationException>(() => catalog.GetEntry(NavigationSlot.Advanced));
    }
}
