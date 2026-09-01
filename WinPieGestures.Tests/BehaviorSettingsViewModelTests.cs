using System;
using System.Collections.Generic;
using System.Linq;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 手势行为分区 ViewModel 的行为覆盖 (T13, ADR-0001)：触发阈值、场景隔离（全屏禁用、
/// 修饰键旁路）、外圈逃逸取消与进程排除黑名单——全部锁定迁移前 SettingsWindow code-behind
/// 的外部行为（live-apply 写回运行态配置、防抖/立即落盘事件、黑名单归一化规则）。
/// 直接 new 被测对象，不触碰任何静态配置状态。
/// </summary>
public sealed class BehaviorSettingsViewModelTests
{
    private static AppConfig MakeConfig() => new()
    {
        DragThreshold = 25.0,
        DisableOnFullScreen = true,
        DisableOnCtrl = false,
        DisableOnShift = false,
        DisableOnAlt = false,
        EnableOuterEscapeCancel = true,
        OuterEscapeDistance = 186.0,
        BlacklistedProcesses = new List<string> { "mstsc.exe", "paint.exe" }
    };

    private static TestDialogService Dialogs() => new();

    // --- 构造与重挂 -----------------------------------------------------------------

    [Fact]
    public void Constructor_LoadsStateFromConfigAndListsBlacklist()
    {
        var config = MakeConfig();

        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);

        Assert.Equal(25.0, vm.DragThreshold);
        Assert.True(vm.DisableOnFullScreen);
        Assert.False(vm.DisableOnCtrl);
        Assert.False(vm.DisableOnShift);
        Assert.False(vm.DisableOnAlt);
        Assert.True(vm.EnableOuterEscapeCancel);
        Assert.Equal(186.0, vm.OuterEscapeDistance);
        Assert.Equal(new[] { "mstsc.exe", "paint.exe" }, vm.BlacklistProcesses);
        Assert.Equal(string.Empty, vm.NewBlacklistProcess);
        Assert.Null(vm.SelectedBlacklistProcess);
    }

    [Fact]
    public void Reload_RebindsToNewConfigInstance()
    {
        var vm = new BehaviorSettingsViewModel(MakeConfig(), Dialogs(), TestHub.NewMessenger());
        var imported = MakeConfig();
        imported.DragThreshold = 40.0;
        imported.DisableOnFullScreen = false;
        imported.DisableOnAlt = true;
        imported.EnableOuterEscapeCancel = false;
        imported.OuterEscapeDistance = 250.0;
        imported.BlacklistedProcesses = new List<string> { "game.exe" };

        vm.Reload(imported);

        Assert.Equal(40.0, vm.DragThreshold);
        Assert.False(vm.DisableOnFullScreen);
        Assert.True(vm.DisableOnAlt);
        Assert.False(vm.EnableOuterEscapeCancel);
        Assert.Equal(250.0, vm.OuterEscapeDistance);
        Assert.Equal(new[] { "game.exe" }, vm.BlacklistProcesses);
        // 重挂不回写、不发落盘事件
        Assert.Equal(40.0, imported.DragThreshold);
    }

    // --- live-apply：阈值（防抖落盘） -------------------------------------------------

    [Fact]
    public void DragThreshold_WritesConfigLiveAndRequestsDebouncedSave()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);
        vm.DragThreshold = 42.0;

        Assert.Equal(42.0, config.DragThreshold);
        Assert.Equal(1, save.Debounced);
        // 阈值走防抖通道，不触发立即落盘（对应迁移前 ScheduleAutoSave）
        Assert.Equal(0, save.Immediate);
    }

    // --- live-apply：场景隔离（立即落盘） ---------------------------------------------

    [Fact]
    public void SceneIsolationSwitches_WriteConfigLiveAndRequestImmediateSave()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);

        vm.DisableOnFullScreen = false;
        vm.DisableOnCtrl = true;
        vm.DisableOnShift = true;
        vm.DisableOnAlt = true;

        Assert.False(config.DisableOnFullScreen);
        Assert.True(config.DisableOnCtrl);
        Assert.True(config.DisableOnShift);
        Assert.True(config.DisableOnAlt);
        Assert.Equal(4, save.Immediate);
    }

    // --- live-apply：外圈逃逸 --------------------------------------------------------

    [Fact]
    public void OuterEscapeToggle_WritesConfigLiveAndRequestsImmediateSave()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);

        vm.EnableOuterEscapeCancel = false;

        Assert.False(config.EnableOuterEscapeCancel);
        Assert.Equal(1, save.Immediate);
    }

    [Fact]
    public void OuterEscapeDistance_RoundsBeforeWritingConfig()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);

        vm.OuterEscapeDistance = 190.6;

        // 属性保留滑条原始值，配置写入取整值（对应迁移前 Math.Round）
        Assert.Equal(190.6, vm.OuterEscapeDistance);
        Assert.Equal(191.0, config.OuterEscapeDistance);
        Assert.Equal(1, save.Immediate);
    }

    // --- 黑名单：输入框添加 -----------------------------------------------------------

    [Fact]
    public void AddFromInput_NormalizesAndAddsProcess()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);
        vm.NewBlacklistProcess = "  MyGame ";
        var added = new List<string>();
        vm.BlacklistEntryAdded += p => added.Add(p);

        vm.AddBlacklistFromInputCommand.Execute(null);

        // 归一化：trim + 小写 + 补 .exe
        Assert.Contains("mygame.exe", vm.BlacklistProcesses);
        Assert.Contains("mygame.exe", config.BlacklistedProcesses!);
        // 成功添加后清空输入并请求落盘
        Assert.Equal(string.Empty, vm.NewBlacklistProcess);
        Assert.Equal(1, save.Immediate);
        Assert.Equal(new[] { "mygame.exe" }, added);
        Assert.Equal("mygame.exe", vm.SelectedBlacklistProcess);
    }

    [Fact]
    public void AddFromInput_AppendsExeExtensionWhenMissing()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);
        vm.NewBlacklistProcess = "SOLIDWORKS";

        vm.AddBlacklistFromInputCommand.Execute(null);

        Assert.Equal(new[] { "mstsc.exe", "paint.exe", "solidworks.exe" }, vm.BlacklistProcesses);
    }

    [Fact]
    public void AddFromInput_Duplicate_OnlySelectsAndDoesNotSaveOrClear()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);
        vm.NewBlacklistProcess = "MSTSC";
        var added = new List<string>();
        vm.BlacklistEntryAdded += p => added.Add(p);

        vm.AddBlacklistFromInputCommand.Execute(null);

        // 与迁移前一致：重复项仅选中并滚动，不清输入框、不落盘、不重复入列
        Assert.Single(vm.BlacklistProcesses.Where(p => p == "mstsc.exe"));
        Assert.Equal("mstsc.exe", vm.SelectedBlacklistProcess);
        // 输入框保留用户原始输入（迁移前重复分支不触碰 TextBox）
        Assert.Equal("MSTSC", vm.NewBlacklistProcess);
        Assert.Equal(0, save.Immediate);
        Assert.Equal(new[] { "mstsc.exe" }, added);
    }

    [Fact]
    public void AddFromInput_Empty_FallsBackToProgramPicker()
    {
        var dialogs = Dialogs();
        var vm = new BehaviorSettingsViewModel(MakeConfig(), dialogs, TestHub.NewMessenger());
        dialogs.ProgramToPick = new ProgramPickResult("Some App", @"C:\Tools\MyTool.EXE");
        vm.NewBlacklistProcess = "";

        vm.AddBlacklistFromInputCommand.Execute(null);

        // 与迁移前一致：空输入直接打开程序选择器，取文件名小写入列
        Assert.Equal(1, dialogs.ProgramPickerCallCount);
        Assert.Contains("mytool.exe", vm.BlacklistProcesses);
    }

    // --- 黑名单：程序选择器 -----------------------------------------------------------

    [Fact]
    public void Browse_PicksProgramAndAddsFileNameLowercased()
    {
        var config = MakeConfig();
        var dialogs = Dialogs();
        dialogs.ProgramToPick = new ProgramPickResult("Some App", @"C:\Program Files\App\MyApp.exe");
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, dialogs, messenger);

        vm.BrowseBlacklistCommand.Execute(null);

        Assert.Contains("myapp.exe", vm.BlacklistProcesses);
        Assert.Contains("myapp.exe", config.BlacklistedProcesses!);
        Assert.Equal(string.Empty, vm.NewBlacklistProcess);
        Assert.Equal("myapp.exe", vm.SelectedBlacklistProcess);
    }

    [Fact]
    public void Browse_Cancelled_MakesNoChanges()
    {
        var config = MakeConfig();
        var dialogs = Dialogs();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, dialogs, messenger);
        var before = vm.BlacklistProcesses.ToList();

        vm.BrowseBlacklistCommand.Execute(null);

        Assert.Equal(1, dialogs.ProgramPickerCallCount);
        Assert.Equal(before, vm.BlacklistProcesses);
        Assert.Equal(0, save.Immediate);
    }

    // --- 黑名单：移除 -----------------------------------------------------------------

    [Fact]
    public void Delete_RemovesSelectedProcessFromListAndConfig()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);
        vm.SelectedBlacklistProcess = "paint.exe";

        vm.DeleteBlacklistProcessCommand.Execute(null);

        Assert.DoesNotContain("paint.exe", vm.BlacklistProcesses);
        Assert.DoesNotContain("paint.exe", config.BlacklistedProcesses!);
        Assert.Contains("mstsc.exe", vm.BlacklistProcesses);
        Assert.Equal(1, save.Immediate);
    }

    [Fact]
    public void Delete_WithoutSelection_FallsBackToLastItem()
    {
        var config = MakeConfig();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);

        vm.DeleteBlacklistProcessCommand.Execute(null);

        // 与迁移前一致：未选中时兜底移除最后一项
        Assert.DoesNotContain("paint.exe", vm.BlacklistProcesses);
        Assert.DoesNotContain("paint.exe", config.BlacklistedProcesses!);
        Assert.Equal(1, save.Immediate);
    }

    [Fact]
    public void Delete_EmptyList_MakesNoChanges()
    {
        var config = MakeConfig();
        config.BlacklistedProcesses = new List<string>();
        var (messenger, save) = SaveSpy.Create();
        var vm = new BehaviorSettingsViewModel(config, Dialogs(), messenger);

        vm.DeleteBlacklistProcessCommand.Execute(null);

        Assert.Empty(vm.BlacklistProcesses);
        Assert.Equal(0, save.Immediate);
    }
}
