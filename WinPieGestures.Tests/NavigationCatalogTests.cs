using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarPie.Tests;

/// <summary>
/// 导航目录与全局槽位表收口：0–4 槽位、NavTab0..4 正典、
/// 注册条目按槽位排序与缺失/重复/未知槽位拦截。只测外部行为——注册结果与校验异常，
/// 不测实现细节（直接 new，不经容器）。
/// </summary>
public sealed class NavigationCatalogTests
{
    private sealed class TriggerViewModel : ObservableObject { }
    private sealed class AppearanceViewModel : ObservableObject { }
    private sealed class GesturesViewModel : ObservableObject { }
    private sealed class AdvancedViewModel : ObservableObject { }
    private sealed class AboutViewModel : ObservableObject { }

    /// <summary>故意打乱注册顺序：目录条目必须仍按槽位 0–4 返回。</summary>
    private static NavigationCatalog CreateFullCatalog()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<GesturesViewModel>(
            NavigationSlot.Gestures, NavigationSlots.GetAutomationId(NavigationSlot.Gestures), "TabGestures", "G");
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabTrigger", "T");
        catalog.RegisterPage<AboutViewModel>(
            NavigationSlot.About, NavigationSlots.GetAutomationId(NavigationSlot.About), "TabAbout", "A");
        catalog.RegisterPage<AppearanceViewModel>(
            NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "TabAppearance", "Ap");
        catalog.RegisterPage<AdvancedViewModel>(
            NavigationSlot.Advanced, NavigationSlots.GetAutomationId(NavigationSlot.Advanced), "TabAdvanced", "Ad");
        return catalog;
    }

    [Fact]
    public void NavigationSlots_CanonicalAutomationIds_AreNavTab0To4()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, NavigationSlots.All.Select(s => (int)s));
        Assert.Equal(new[] { "NavTab0", "NavTab1", "NavTab2", "NavTab3", "NavTab4" },
            NavigationSlots.All.Select(NavigationSlots.GetAutomationId));
    }

    [Fact]
    public void RegisterAllSlots_EntriesSortedBySlot_WithCanonicalData()
    {
        var catalog = CreateFullCatalog();

        catalog.Validate(); // 五槽齐全：不应抛

        Assert.Equal(new[]
        {
            NavigationSlot.Trigger, NavigationSlot.Appearance, NavigationSlot.Gestures,
            NavigationSlot.Advanced, NavigationSlot.About
        }, catalog.Entries.Select(e => e.Slot));
        Assert.Equal(new[] { "NavTab0", "NavTab1", "NavTab2", "NavTab3", "NavTab4" },
            catalog.Entries.Select(e => e.AutomationId));
        Assert.Equal(new[]
        {
            "TabTrigger", "TabAppearance", "TabGestures", "TabAdvanced", "TabAbout"
        }, catalog.Entries.Select(e => e.TitleKey));
        Assert.Equal(new[]
        {
            typeof(TriggerViewModel), typeof(AppearanceViewModel), typeof(GesturesViewModel),
            typeof(AdvancedViewModel), typeof(AboutViewModel)
        }, catalog.Entries.Select(e => e.ViewModelType));
    }

    [Fact]
    public void Validate_MissingSlot_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabTrigger", "");
        catalog.RegisterPage<GesturesViewModel>(
            NavigationSlot.Gestures, NavigationSlots.GetAutomationId(NavigationSlot.Gestures), "TabGestures", "");
        catalog.RegisterPage<AdvancedViewModel>(
            NavigationSlot.Advanced, NavigationSlots.GetAutomationId(NavigationSlot.Advanced), "TabAdvanced", "");
        catalog.RegisterPage<AboutViewModel>(
            NavigationSlot.About, NavigationSlots.GetAutomationId(NavigationSlot.About), "TabAbout", "");

        var ex = Assert.Throws<InvalidOperationException>(() => catalog.Validate());

        Assert.Contains(nameof(NavigationSlot.Appearance), ex.Message);
    }

    [Fact]
    public void Register_DuplicateSlot_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabTrigger", "");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            catalog.RegisterPage<AppearanceViewModel>(
                NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "TabAppearance", ""));

        Assert.Contains(nameof(NavigationSlot.Trigger), ex.Message);
    }

    [Fact]
    public void Register_DuplicateAutomationId_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabTrigger", "");

        Assert.Throws<InvalidOperationException>(() =>
            catalog.RegisterPage<AppearanceViewModel>(
                NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabAppearance", ""));
    }

    [Fact]
    public void Register_UnknownSlot_Throws()
    {
        var catalog = new NavigationCatalog();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            catalog.RegisterPage<TriggerViewModel>((NavigationSlot)7, "NavTab7", "TabTrigger", ""));
    }

    [Fact]
    public void GetEntry_RegisteredSlot_ReturnsCatalogEntry()
    {
        var catalog = CreateFullCatalog();

        var entry = catalog.GetEntry(NavigationSlot.Gestures);

        Assert.Equal(NavigationSlot.Gestures, entry.Slot);
        Assert.Equal("NavTab2", entry.AutomationId);
        Assert.Equal(typeof(GesturesViewModel), entry.ViewModelType);
    }

    [Fact]
    public void GetEntry_UnregisteredSlot_Throws()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabTrigger", "");

        Assert.Throws<InvalidOperationException>(() => catalog.GetEntry(NavigationSlot.About));
    }
}
