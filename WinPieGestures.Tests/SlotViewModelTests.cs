using System;
using System.Collections.Generic;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 槽位动作编辑闭环的行为覆盖 (T12, ADR-0001/0004)：图标设置、程序选择、文件夹选择的
/// 对话框编排与写回规则，以及类型切换副作用——全部锁定迁移前 SettingsWindow code-behind
/// 的外部行为（live-apply：写回直改模型）。对话框经 <see cref="TestDialogService"/> 替身，
/// 只测外部行为，mock 直接 new。T20 起动作入口走 RelayCommand，文件夹提交落盘经
/// ImmediateSaveRequestedMessage 消息（取代 EditApplied 事件）。
/// </summary>
public sealed class SlotViewModelTests
{
    private static readonly LocalizationService Localization = new();

    /// <summary>LocalizationService 实例 LanguageChanged 事件当前订阅者数（反射读 backing field；
    /// 事件是 <see cref="Action"/> 无订阅者时为 null）。</summary>
    private static int I18nEventSubscriberCount()
        => ((MulticastDelegate?)typeof(LocalizationService)
            .GetField(nameof(LocalizationService.LanguageChanged), BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(Localization))?.GetInvocationList().Length ?? 0;

    private static SlotViewModel MakeSlot(
        ActionItem? action = null,
        TestDialogService? dialogs = null,
        WeakReferenceMessenger? messenger = null)
        => new(
            "右 (E / 0°)",
            action ?? new ActionItem(),
            dialogs ?? new TestDialogService(),
            new TestActionExecutor(),
            messenger ?? TestHub.NewMessenger(),
            Localization);

    // --- 图标设置（迁移前 PickIcon_Click） ---------------------------------------------

    [Fact]
    public void PickIcon_WhenPicked_WritesIconKey()
    {
        var dialogs = new TestDialogService { IconToPick = new IconPickResult("TaskManager") };
        var slot = MakeSlot(dialogs: dialogs);

        slot.PickIconCommand.Execute(null);

        Assert.Equal("TaskManager", slot.Action.IconKey);
    }

    [Fact]
    public void PickIcon_PassesCurrentIconKeyToPicker()
    {
        var dialogs = new TestDialogService();
        var slot = MakeSlot(new ActionItem { IconKey = "Copy" }, dialogs);

        slot.PickIconCommand.Execute(null);

        var call = Assert.Single(dialogs.IconPickerCalls);
        Assert.Equal("Copy", call);
    }

    [Fact]
    public void PickIcon_WhenCancelled_KeepsIconKey()
    {
        var dialogs = new TestDialogService { IconToPick = null };
        var slot = MakeSlot(new ActionItem { IconKey = "Copy" }, dialogs);

        slot.PickIconCommand.Execute(null);

        Assert.Equal("Copy", slot.Action.IconKey);
    }

    [Fact]
    public void PickIcon_ClearResult_WritesEmptyIconKey()
    {
        var dialogs = new TestDialogService { IconToPick = new IconPickResult(null) };
        var slot = MakeSlot(new ActionItem { IconKey = "Copy" }, dialogs);

        slot.PickIconCommand.Execute(null);

        Assert.Equal("", slot.Action.IconKey);
    }

    [Fact]
    public void VectorIconPathData_WithVectorKey_ReturnsSvgFromS1Export()
    {
        // T3c/#67：图标取值经 S1 共享图标资产出口 IconAssets。
        var slot = MakeSlot(new ActionItem { IconKey = "Copy" });

        Assert.Equal(IconAssets.GetSvgPathByKey("Copy"), slot.VectorIconPathData);
    }

    [Fact]
    public void VectorIconPathData_WithUnknownVectorKey_ReturnsNull()
    {
        var slot = MakeSlot(new ActionItem { IconKey = "Not-A-Real-Key" });

        Assert.Null(slot.VectorIconPathData);
        Assert.False(slot.HasVectorIcon);
    }

    [Fact]
    public void VectorIconPathData_PrefersCustomSvgDataOverVectorKey()
    {
        var slot = MakeSlot(new ActionItem { IconKey = "Copy", CustomIconSvg = "M0,0L5,5" });

        Assert.Equal("M0,0L5,5", slot.VectorIconPathData);
        Assert.True(slot.HasVectorIcon);
    }

    // --- 程序选择（迁移前 Browse_Click） -------------------------------------------------

    [Fact]
    public void BrowseProgram_WhenPicked_WritesParameterToFillsDefaultName()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("记事本", @"C:\Windows\notepad.exe") };
        var slot = MakeSlot(new ActionItem { Type = "Launch" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal(@"C:\Windows\notepad.exe", slot.Action.Parameter);
        Assert.Equal("记事本", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_EmptyPickerName_FallsBackToFileNameWithoutExtension()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("", @"C:\Tools\foo.bar.exe") };
        var slot = MakeSlot(dialogs: dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal("foo.bar", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_OverwritesPlaceholderNames()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("计算器", @"C:\Windows\System32\calc.exe") };
        var slot = MakeSlot(new ActionItem { Name = "动作 2" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal("计算器", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_KeepsCustomizedName()
    {
        var dialogs = new TestDialogService { ProgramToPick = new ProgramPickResult("计算器", @"C:\Windows\System32\calc.exe") };
        var slot = MakeSlot(new ActionItem { Name = "我的程序" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal(@"C:\Windows\System32\calc.exe", slot.Action.Parameter);
        Assert.Equal("我的程序", slot.Action.Name);
    }

    [Fact]
    public void BrowseProgram_WhenCancelled_NothingChanges()
    {
        var dialogs = new TestDialogService { ProgramToPick = null };
        var slot = MakeSlot(new ActionItem { Name = "动作 1", Parameter = "old" }, dialogs);

        slot.BrowseProgramCommand.Execute(null);

        Assert.Equal("old", slot.Action.Parameter);
        Assert.Equal("动作 1", slot.Action.Name);
        Assert.Equal(1, dialogs.ProgramPickerCallCount);
    }

    // --- 文件夹选择（迁移前 BrowseFolder_Click） -----------------------------------------

    [Fact]
    public void BrowseFolder_WhenPicked_WritesParameterFillsNameIconAndRequestsSave()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Users\me\Documents") };
        var (messenger, save) = SaveSpy.Create();
        var slot = new SlotViewModel(
            "右 (E / 0°)",
            new ActionItem { Type = "Folder", Name = "快捷动作 1" },
            dialogs,
            new TestActionExecutor(),
            messenger,
            Localization);

        slot.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\Users\me\Documents", slot.Action.Parameter);
        Assert.Equal("Documents", slot.Action.Name);
        Assert.Equal("Folder", slot.Action.IconKey);
        Assert.Equal(1, save.Immediate); // 文件夹提交后请求落盘（取代 EditApplied）
    }

    [Fact]
    public void BrowseFolder_KeepsCustomizedNameAndIcon()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Work") };
        var slot = MakeSlot(new ActionItem { Name = "工作目录", IconKey = "Explorer" }, dialogs);

        slot.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\Work", slot.Action.Parameter);
        Assert.Equal("工作目录", slot.Action.Name);
        Assert.Equal("Explorer", slot.Action.IconKey);
    }

    [Fact]
    public void BrowseFolder_WhenCancelled_NothingChangesAndNoSaveRequest()
    {
        var dialogs = new TestDialogService { FolderToPick = null };
        var (messenger, save) = SaveSpy.Create();
        var slot = new SlotViewModel(
            "右 (E / 0°)",
            new ActionItem { Name = "动作 1", Parameter = "old" },
            dialogs,
            new TestActionExecutor(),
            messenger,
            Localization);

        slot.BrowseFolderCommand.Execute(null);

        Assert.Equal("old", slot.Action.Parameter);
        Assert.Equal("动作 1", slot.Action.Name);
        Assert.Equal(0, save.Immediate);
    }

    [Fact]
    public void BrowseFolder_PassesCurrentParameterAsInitialDirectory()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Work") };
        var slot = MakeSlot(new ActionItem { Parameter = @"C:\Users" }, dialogs);

        slot.BrowseFolderCommand.Execute(null);

        var initial = Assert.Single(dialogs.FolderDialogInitialDirectories);
        Assert.Equal(@"C:\Users", initial);
    }

    // --- 瞬态生命周期（T27/ADR-0010：自订阅 Localization.LanguageChanged 须成对退订） ------------

    [Fact]
    public void LanguageChanged_RefreshesResidentComputedProperties()
    {
        var original = Localization.CurrentLanguage;
        var slot = MakeSlot(new ActionItem { Type = "System", IconKey = "TaskManager" });
        var notified = new List<string?>();
        slot.PropertyChanged += (s, e) => notified.Add(e.PropertyName);
        try
        {
            var target = original == LanguageCode.En ? LanguageCode.Ja : LanguageCode.En;
            Localization.SetLanguage(target);

            Assert.Contains(nameof(slot.ActionTypes), notified);
            Assert.Contains(nameof(slot.TestButtonText), notified);
            Assert.Contains(nameof(slot.IconDisplayText), notified);
        }
        finally
        {
            Localization.SetLanguage(original);
            slot.Dispose();
        }
    }

    [Fact]
    public void Dispose_UnsubscribesLanguageChangedHandler()
    {
        var original = Localization.CurrentLanguage;
        var before = I18nEventSubscriberCount();
        var slot = MakeSlot();
        Assert.Equal(before + 1, I18nEventSubscriberCount()); // 构造即订阅
        slot.Dispose();
        Assert.Equal(before, I18nEventSubscriberCount());     // 退订后恢复

        // 退订后切语不再唤醒已释放槽
        var refreshed = 0;
        slot.PropertyChanged += (s, e) => refreshed++;
        try
        {
            var target = original == LanguageCode.En ? LanguageCode.Ja : LanguageCode.En;
            Localization.SetLanguage(target);
        }
        finally
        {
            Localization.SetLanguage(original);
        }
        Assert.Equal(0, refreshed);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var before = I18nEventSubscriberCount();
        var slot = MakeSlot();

        slot.Dispose();
        slot.Dispose(); // 重复释放不得重复退订（guard）

        Assert.Equal(before, I18nEventSubscriberCount());
    }

    [Fact]
    public void Dispose_IsIdempotent_EvenWhenHandlerWasRemovedExternally()
    {
        // 与实现解耦的幂等语义验证：guard 使第二次 Dispose 不产生额外副作用，
        // 事件计数最终回到基线（等价于"外部早已退订后再次 Dispose 不上抛"）。
        var before = I18nEventSubscriberCount();
        var slot = MakeSlot();

        slot.Dispose();
        Assert.Equal(before, I18nEventSubscriberCount());
        slot.Dispose(); // 幂等：第二次释放被 guard 拦截

        Assert.Equal(before, I18nEventSubscriberCount());
    }

    // --- 类型切换（迁移前绑定直写 + 副作用） ---------------------------------------------

    [Fact]
    public void Type_SetToFolder_AppliesFolderIconAndDefaultNameWhenMissing()
    {
        var slot = MakeSlot(new ActionItem { Type = "Hotkey", Name = "快捷动作 1" });

        slot.Type = "Folder";

        Assert.Equal("Folder", slot.Action.Type);
        Assert.Equal("Folder", slot.Action.IconKey);
        Assert.Equal(Localization.GetString("ActionTypeFolderShort"), slot.Action.Name);
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
