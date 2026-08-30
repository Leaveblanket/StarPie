using System;
using System.Collections.Generic;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// State coverage for the wheel view-model (T05): sector slots built from the
/// profile, core title/subtitle derivation, the inner-radius safety clamp, and the
/// engine-driven state mutations (show/close, selected sector, outer escape) with
/// the change notifications the window relies on.
/// </summary>
public sealed class WheelViewModelTests
{
    private static WheelProfile Profile(int sectorCount, params ActionItem[] actions)
    {
        var profile = new WheelProfile { ProcessName = "chrome.exe", SectorCount = sectorCount };
        profile.Actions.AddRange(actions);
        return profile;
    }

    private static WheelViewModel Create(WheelProfile profile, AppConfig? config = null)
        => new(new GesturePoint(120, 96), profile, config ?? new AppConfig());

    // --- Construction ---------------------------------------------------------

    [Fact]
    public void Ctor_MapsActionDataIntoSectorSlots()
    {
        var profile = Profile(4,
            new ActionItem { Type = "Launch", Name = "浏览器", Parameter = @"C:\chrome.exe", IconKey = "Chrome", CustomIconSvg = "M0,0L1,1" },
            new ActionItem { Type = "Hotkey", Name = "复制", Parameter = "Ctrl+C" },
            new ActionItem { Type = "System", Name = "锁屏", Parameter = "lock" });

        var vm = Create(profile);

        Assert.Equal(4, vm.Sectors.Count);
        Assert.True(vm.Sectors[0].HasAction);
        Assert.Equal("浏览器", vm.Sectors[0].Name);
        Assert.Equal("Launch", vm.Sectors[0].Type);
        Assert.Equal(@"C:\chrome.exe", vm.Sectors[0].Parameter);
        Assert.Equal("Chrome", vm.Sectors[0].IconKey);
        Assert.Equal("M0,0L1,1", vm.Sectors[0].CustomIconSvg);
        Assert.Equal("复制", vm.Sectors[1].Name);
        Assert.Equal("锁屏", vm.Sectors[2].Name);
    }

    [Fact]
    public void Ctor_SlotsWithoutAction_HaveNoAction_AndCarryViewDefaults()
    {
        var profile = Profile(4, new ActionItem { Type = "Hotkey", Name = "复制" });

        var vm = Create(profile);

        Assert.False(vm.Sectors[1].HasAction);
        Assert.Equal("", vm.Sectors[1].Name);
        Assert.Equal("Hotkey", vm.Sectors[1].Type); // view defaults when no action bound
        Assert.Equal("", vm.Sectors[1].Parameter);
        Assert.Equal("", vm.Sectors[3].IconKey);
    }

    [Fact]
    public void Ctor_GlobalProfile_TitleIsGlobalActions_SubtitleShowsSectorCount()
    {
        var vm = Create(new WheelProfile { ProcessName = "Global", SectorCount = 8 });

        Assert.Equal("全局动作", vm.CoreTitle);
        Assert.Equal("8 键动作", vm.CoreSubtitle);
    }

    [Fact]
    public void Ctor_ProcessProfile_TitleIsProcessName()
    {
        var vm = Create(Profile(4));

        Assert.Equal("chrome.exe", vm.CoreTitle);
        Assert.Equal("4 键动作", vm.CoreSubtitle);
    }

    [Fact]
    public void Ctor_CarriesCenterAndRadiusState()
    {
        var config = new AppConfig { WheelRadius = 130.0, CoreRadius = 46.0 };

        var vm = Create(Profile(8), config);

        Assert.Equal(120, vm.Center.X);
        Assert.Equal(96, vm.Center.Y);
        Assert.Equal(130.0, vm.OuterRadius);
        Assert.Equal(46.0, vm.CoreRadius);
        Assert.Equal(52.0, vm.InnerRadius); // AppConfig default, below outer: kept
    }

    [Fact]
    public void Ctor_InnerRadiusAtOrAboveOuter_IsClampedBelow()
    {
        var config = new AppConfig { WheelRadius = 100.0, InnerRadius = 100.0 };

        var vm = Create(Profile(8), config);

        Assert.Equal(80.0, vm.InnerRadius); // outer - 20
    }

    [Fact]
    public void Ctor_ThemeAndStyleFallBackToDefaults_WhenUnset()
    {
        var config = new AppConfig { Theme = null!, UiStyle = null! };

        var vm = Create(Profile(8), config);

        Assert.Equal("System", vm.Theme);
        Assert.Equal("ClassicRing", vm.UiStyle);
    }

    // --- Engine-driven state mutations ----------------------------------------

    [Fact]
    public void InitialState_Unselected_NoEscape_NotShownNotClosed()
    {
        var vm = Create(Profile(8));

        Assert.Equal(-1, vm.SelectedSectorIndex);
        Assert.False(vm.IsOuterEscaped);
        Assert.False(vm.IsShown);
        Assert.False(vm.IsClosed);
    }

    [Fact]
    public void HighlightSector_SetsSelectedIndex_AndNotifies()
    {
        var vm = Create(Profile(8));
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.HighlightSector(3);

        Assert.Equal(3, vm.SelectedSectorIndex);
        Assert.Equal(new[] { nameof(WheelViewModel.SelectedSectorIndex) }, changes);
    }

    [Fact]
    public void HighlightSector_MinusOne_ClearsSelection()
    {
        var vm = Create(Profile(8));
        vm.HighlightSector(2);

        vm.HighlightSector(-1);

        Assert.Equal(-1, vm.SelectedSectorIndex);
    }

    [Fact]
    public void HighlightSector_SameIndex_ReassertsNotification()
    {
        // The engine calls HighlightSector on every drag move; the view must re-apply
        // the selection (center-cancel feedback included) even when the index repeats,
        // matching the pre-migration window's unconditional re-run.
        var vm = Create(Profile(8));
        vm.HighlightSector(2);
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.HighlightSector(2);

        Assert.Equal(2, vm.SelectedSectorIndex);
        Assert.Equal(new[] { nameof(WheelViewModel.SelectedSectorIndex) }, changes);
    }

    [Fact]
    public void SetOuterEscapeState_SameValue_RaisesNoNotification()
    {
        var vm = Create(Profile(8));
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.SetOuterEscapeState(false);

        Assert.Empty(changes);
    }

    [Fact]
    public void SetOuterEscapeState_Toggles_AndNotifies()
    {
        var vm = Create(Profile(8));
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.SetOuterEscapeState(true);
        Assert.True(vm.IsOuterEscaped);
        Assert.Equal(new[] { nameof(WheelViewModel.IsOuterEscaped) }, changes);

        vm.SetOuterEscapeState(false);
        Assert.False(vm.IsOuterEscaped);
    }

    [Fact]
    public void Show_ThenClose_TransitionsLifecycleState()
    {
        var vm = Create(Profile(8));
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.Show();
        Assert.True(vm.IsShown);

        vm.Close();
        Assert.True(vm.IsClosed);

        Assert.Equal(
            new[] { nameof(WheelViewModel.IsShown), nameof(WheelViewModel.IsClosed) },
            changes);
    }
}
