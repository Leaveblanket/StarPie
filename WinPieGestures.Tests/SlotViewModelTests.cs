using System;
using System.Collections.Generic;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 槽位动作编辑闭环的行为覆盖 (T12, ADR-0001/0004)：图标设置、程序选择、文件夹选择的
/// 对话框编排与写回规则，以及类型切换副作用——全部锁定迁移前 SettingsWindow code-behind
/// 的外部行为（live-apply：写回直改模型）。对话框经 <see cref="TestDialogService"/> 替身，
/// 只测外部行为，mock 直接 new。
/// </summary>
public sealed class SlotViewModelTests
{
    private static SlotViewModel MakeSlot(ActionItem? action = null)
        => new("右 (E / 0°)", action ?? new ActionItem(), new TestDialogService());

    // --- 图标设置（迁移前 PickIcon_Click） ---------------------------------------------

    [Fact]
    public void PickIcon_WhenPicked_WritesIconKeyAndReturnsTrue()
    {
        var dialogs = new TestDialogService { IconToPick = new IconPickResult("TaskManager") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem(), dialogs);

        var picked = slot.PickIcon();

        Assert.True(picked);
        Assert.Equal("TaskManager", slot.Action.IconKey);
    }

    [Fact]
    public void PickIcon_PassesCurrentIconKeyToPicker()
    {
        var dialogs = new TestDialogService();
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { IconKey = "Copy" }, dialogs);

        slot.PickIcon();

        var call = Assert.Single(dialogs.IconPickerCalls);
        Assert.Equal("Copy", call);
    }

    [Fact]
    public void PickIcon_WhenCancelled_ReturnsFalseAndKeepsIconKey()
    {
        var dialogs = new TestDialogService { IconToPick = null };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { IconKey = "Copy" }, dialogs);

        var picked = slot.PickIcon();

        Assert.False(picked);
        Assert.Equal("Copy", slot.Action.IconKey);
    }

    [Fact]
    public void PickIcon_ClearResult_WritesEmptyIconKey()
    {
        var dialogs = new TestDialogService { IconToPick = new IconPickResult(null) };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { IconKey = "Copy" }, dialogs);

        var picked = slot.PickIcon();

        Assert.True(picked);
        Assert.Equal("", slot.Action.IconKey);
    }

    // --- 程序选择（迁移前 Browse_Click） -------------------------------------------------

    [Fact]
    public void BrowseProgram_WhenPicked_WritesParameterToFillsDefaultName()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("记事本", @"C:\Windows\notepad.exe") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Type = "Launch" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal(@"C:\Windows\notepad.exe", slot.Action.Parameter);
        Assert.Equal("记事本", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_EmptyPickerName_FallsBackToFileNameWithoutExtension()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("", @"C:\Tools\foo.bar.exe") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem(), dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal("foo.bar", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_OverwritesPlaceholderNames()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("计算器", @"C:\Windows\System32\calc.exe") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Name = "动作 2" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal("计算器", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_KeepsCustomizedName()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("计算器", @"C:\Windows\System32\calc.exe") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Name = "我的程序" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal(@"C:\Windows\System32\calc.exe", slot.Action.Parameter);
        Assert.Equal("我的程序", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_WhenCancelled_NothingChanges()
    {
        var dialogs = new TestDialogService { ProgramToPick = null };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Name = "动作 1", Parameter = "old" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal("old", slot.Action.Parameter);
        Assert.Equal("动作 1", slot.Action.Name);
        Assert.Equal(1, dialogs.ProgramPickerCallCount);
    }

    // --- 文件夹选择（迁移前 BrowseFolder_Click） -----------------------------------------

    [Fact]
    public void BrowseFolder_WhenPicked_WritesParameterFillsNameIconAndRaisesEditApplied()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Users\me\Documents") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Type = "Folder", Name = "快捷动作 1" }, dialogs);
        var committed = 0;
        slot.EditApplied += () => committed++;

        slot.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\Users\me\Documents", slot.Action.Parameter);
        Assert.Equal("Documents", slot.Action.Name);
        Assert.Equal("Folder", slot.Action.IconKey);
        Assert.Equal(1, committed);
    }

    [Fact]
    public void BrowseFolder_KeepsCustomizedNameAndIcon()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Work") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Name = "工作目录", IconKey = "Explorer" }, dialogs);

        slot.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\Work", slot.Action.Parameter);
        Assert.Equal("工作目录", slot.Action.Name);
        Assert.Equal("Explorer", slot.Action.IconKey);
    }

    [Fact]
    public void BrowseFolder_WhenCancelled_NothingChangesAndNoEditApplied()
    {
        var dialogs = new TestDialogService { FolderToPick = null };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Name = "动作 1", Parameter = "old" }, dialogs);
        var committed = 0;
        slot.EditApplied += () => committed++;

        slot.BrowseFolderCommand.Execute(null);

        Assert.Equal("old", slot.Action.Parameter);
        Assert.Equal("动作 1", slot.Action.Name);
        Assert.Equal(0, committed);
    }

    [Fact]
    public void BrowseFolder_PassesCurrentParameterAsInitialDirectory()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Work") };
        var slot = new SlotViewModel("右 (E / 0°)", new ActionItem { Parameter = @"C:\Users" }, dialogs);

        slot.BrowseFolderCommand.Execute(null);

        var initial = Assert.Single(dialogs.FolderDialogInitialDirectories);
        Assert.Equal(@"C:\Users", initial);
    }

    // --- 类型切换（迁移前绑定直写 + 副作用） ---------------------------------------------

    [Fact]
    public void Type_SetToFolder_AppliesFolderIconAndDefaultNameWhenMissing()
    {
        var slot = MakeSlot(new ActionItem { Type = "Hotkey", Name = "快捷动作 1" });

        slot.Type = "Folder";

        Assert.Equal("Folder", slot.Action.Type);
        Assert.Equal("Folder", slot.Action.IconKey);
        Assert.Equal(I18n.T("ActionTypeFolderShort"), slot.Action.Name);
    }

    [Fact]
    public void Type_Set_RaisesTypeFlagNotifications()
    {
        var slot = MakeSlot(new ActionItem { Type = "Hotkey" });
        var notified = new List<string?>();
        slot.PropertyChanged += (s, e) => notified.Add(e.PropertyName);

        slot.Type = "Launch";

        Assert.True(slot.IsLaunchType);
        Assert.False(slot.IsHotkeyType);
        Assert.Contains(nameof(slot.Type), notified);
        Assert.Contains(nameof(slot.IsHotkeyType), notified);
        Assert.Contains(nameof(slot.IsLaunchType), notified);
        Assert.Contains(nameof(slot.IsSystemType), notified);
    }

    [Fact]
    public void SelectedSystemPreset_Set_WritesParameterAndAppliesDefaultNameIcon()
    {
        var slot = MakeSlot(new ActionItem { Type = "System", Name = "快捷动作", Parameter = "Lock" });

        slot.SelectedSystemPreset = "TaskManager";

        Assert.Equal("TaskManager", slot.Action.Parameter);
        Assert.Equal("任务管理器", slot.Action.Name);
        Assert.Equal("TaskManager", slot.Action.IconKey);
    }
}
