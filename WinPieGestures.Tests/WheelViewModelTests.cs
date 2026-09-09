using System;
using System.Collections.Generic;
using StarPie;

namespace StarPie.Tests;

/// <summary>
/// 轮盘 ViewModel 状态覆盖：由 Profile 构建扇区槽位、核标题/副标题推导、内半径安全夹紧，
/// 以及引擎驱动的状态变更（显示/关闭、选中扇区、外围逃逸）与窗口依赖的属性变更通知。
/// </summary>
public sealed class WheelViewModelTests
{
    private static readonly LocalizationService Localization = new();

    private static WheelProfile Profile(int sectorCount, params ActionItem[] actions)
    {
        var profile = new WheelProfile { ProcessName = "chrome.exe", SectorCount = sectorCount };
        profile.Actions.AddRange(actions);
        return profile;
    }

    private static WheelViewModel Create(WheelProfile profile, AppConfig? config = null)
        => new(new GesturePoint(120, 96), profile, config ?? new AppConfig(), Localization);

    // --- 构造 ---------------------------------------------------------

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
        Assert.Equal("Hotkey", vm.Sectors[1].Type); // 无动作绑定时使用视图默认值
        Assert.Equal("", vm.Sectors[1].Parameter);
        Assert.Equal("", vm.Sectors[3].IconKey);
    }

    [Fact]
    public void Ctor_GlobalProfile_TitleIsGlobalActions_SubtitleShowsSectorCount()
    {
        var original = Localization.CurrentLanguage;
        try
        {
            Localization.SetLanguage("zh-CN");

            var vm = Create(new WheelProfile { ProcessName = "Global", SectorCount = 8 });

            Assert.Equal("全局动作", vm.CoreTitle);
            Assert.Equal("8 键动作", vm.CoreSubtitle);
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void Ctor_ProcessProfile_TitleIsProcessName()
    {
        var original = Localization.CurrentLanguage;
        try
        {
            Localization.SetLanguage("zh-CN");

            var vm = Create(Profile(4));

            Assert.Equal("chrome.exe", vm.CoreTitle);
            Assert.Equal("4 键动作", vm.CoreSubtitle);
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    [Theory]
    [InlineData("zh-CN", "全局动作", "8 键动作")]
    [InlineData("zh-TW", "全域動作", "8 鍵動作")]
    [InlineData("en", "Global Actions", "8 Actions")]
    [InlineData("ja", "グローバル操作", "8 アクション")]
    public void Ctor_GlobalProfile_CoreCopyFollowsCurrentLanguageAtCreation(
        string language, string expectedTitle, string expectedSubtitle)
    {
        var original = Localization.CurrentLanguage;
        try
        {
            Localization.SetLanguage(language);

            var vm = Create(new WheelProfile { ProcessName = "Global", SectorCount = 8 });

            Assert.Equal(expectedTitle, vm.CoreTitle);
            Assert.Equal(expectedSubtitle, vm.CoreSubtitle);
        }
        finally
        {
            Localization.SetLanguage(original);
        }
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
        Assert.Equal(52.0, vm.InnerRadius); // AppConfig 默认内半径低于外半径：保持
    }

    [Fact]
    public void Ctor_InnerRadiusAtOrAboveOuter_IsClampedBelow()
    {
        var config = new AppConfig { WheelRadius = 100.0, InnerRadius = 100.0 };

        var vm = Create(Profile(8), config);

        Assert.Equal(80.0, vm.InnerRadius); // 外半径 - 20
    }

    [Fact]
    public void Ctor_PaletteAndStyleFallBackToDefaults_WhenUnset()
    {
        var config = new AppConfig { WheelPalette = null!, WheelStyle = null! };

        var vm = Create(Profile(8), config);

        Assert.Equal("System", vm.WheelPalette);
        Assert.Equal("ClassicRing", vm.WheelStyle);
    }

    // --- 引擎驱动状态变更 ----------------------------------------

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
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

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
        // 引擎在每次拖拽移动时都会调用 HighlightSector；即使索引重复，视图也必须重新应用
        // 选中（含中心取消反馈），与窗口的无条件重跑语义一致。
        var vm = Create(Profile(8));
        vm.HighlightSector(2);
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.HighlightSector(2);

        Assert.Equal(2, vm.SelectedSectorIndex);
        Assert.Equal(new[] { nameof(WheelViewModel.SelectedSectorIndex) }, changes);
    }

    [Fact]
    public void SetOuterEscapeState_SameValue_RaisesNoNotification()
    {
        var vm = Create(Profile(8));
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.SetOuterEscapeState(false);

        Assert.Empty(changes);
    }

    [Fact]
    public void SetOuterEscapeState_Toggles_AndNotifies()
    {
        var vm = Create(Profile(8));
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

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
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Show();
        Assert.True(vm.IsShown);

        vm.Close();
        Assert.True(vm.IsClosed);

        Assert.Equal(
            new[] { nameof(WheelViewModel.IsShown), nameof(WheelViewModel.IsClosed) },
            changes);
    }
}
