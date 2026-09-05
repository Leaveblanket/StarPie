using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 配置方案分区列表侧 ViewModel 的行为覆盖 (T11, ADR-0001)：方案列表与选中态、
/// 扇区数切换、方向槽位集合（方位角标签、缺省动作补齐、扇区数规范化）以及
/// 槽位名称编辑——全部锁定迁移前 SettingsWindow code-behind 的外部行为。
/// 直接 new 被测对象，不触碰任何静态配置状态。
/// </summary>
public sealed class ProfileListViewModelTests
{
    private static readonly LocalizationService Localization = new();

    private static WheelProfile MakeProfile(string processName = "Global", int sectorCount = 8, int actionCount = -1)
    {
        var profile = new WheelProfile { ProcessName = processName, SectorCount = sectorCount };
        int count = actionCount >= 0 ? actionCount : sectorCount;
        for (int i = 0; i < count; i++)
        {
            profile.Actions.Add(new ActionItem { Type = "Hotkey", Name = $"动作 {i + 1}", Parameter = $"P{i}" });
        }
        return profile;
    }

    private static TestDialogService Dialogs() => new();

    /// <summary>T27 诊断：LocalizationService 实例 LanguageChanged 字段的委托列表。</summary>
    private static string I18nHandlersDump()
        => ((MulticastDelegate?)typeof(LocalizationService)
            .GetField(nameof(LocalizationService.LanguageChanged), BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(Localization)) is { } handlers
            ? string.Join(" | ", handlers.GetInvocationList().Select(d => d.Method.DeclaringType?.Name + "." + d.Method.Name))
            : "<null>";
    /// <summary>LocalizationService 实例 LanguageChanged 事件当前订阅者数（反射读 backing field；
    /// 事件是 <see cref="Action"/> 无订阅者时为 null）。</summary>
    private static int I18nEventSubscriberCount()
        => ((MulticastDelegate?)typeof(LocalizationService)
            .GetField(nameof(LocalizationService.LanguageChanged), BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(Localization))?.GetInvocationList().Length ?? 0;

    // --- 构造与列表展示 -------------------------------------------------------------

    [Fact]
    public void Constructor_WrapsAllSourceProfiles_SelectsFirstByDefault()
    {
        var source = new List<WheelProfile> { MakeProfile("Global"), MakeProfile("chrome.exe", 4) };

        var vm = new ProfileListViewModel(source, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("Global", vm.Profiles[0].ProcessName);
        Assert.Equal("chrome.exe", vm.Profiles[1].ProcessName);
        // T21：默认选中收编进 VM——有方案即选中首项并重建槽位（页面 View 不再写选中态）。
        Assert.Same(vm.Profiles[0], vm.SelectedProfile);
        Assert.Equal(8, vm.SelectedSectorCount);
        Assert.Equal(8, vm.Slots.Count);
    }

    [Fact]
    public void Constructor_NoProfiles_StartsUnselectedWithEmptySlots()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile>(), Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.Empty(vm.Profiles);
        Assert.Null(vm.SelectedProfile);
        Assert.Null(vm.SelectedSectorCount);
        Assert.Empty(vm.Slots);
    }

    // --- IProfilePreviewSource 只读预览 Profile 来源（#69：M1 对外契约，选中/首项回落语义） ----

    [Fact]
    public void ImplementsIProfilePreviewSource_DefaultSelection_ReturnsSelectedProfileModel()
    {
        var profile = MakeProfile("Global", 8);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        IProfilePreviewSource source = vm;

        // 构造默认选中首项：预览上下文 = 选中方案的模型实例（与轮盘外观预览既有取值链一致）。
        Assert.Same(profile, source.PreviewProfile);
    }

    [Fact]
    public void ImplementsIProfilePreviewSource_SelectionChange_UpdatesPreviewProfile()
    {
        var first = MakeProfile("Global", 4);
        var second = MakeProfile("chrome.exe", 12);
        var vm = new ProfileListViewModel(
            new List<WheelProfile> { first, second },
            Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        IProfilePreviewSource source = vm;

        Assert.Same(first, source.PreviewProfile);

        vm.SelectProfile(vm.Profiles[1]);

        Assert.Same(second, source.PreviewProfile);
    }

    [Fact]
    public void ImplementsIProfilePreviewSource_Reload_ResetsToFirstOfNewList()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile("old.exe") }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        IProfilePreviewSource source = vm;
        var imported = MakeProfile("imported.exe", 4);

        vm.Reload(new List<WheelProfile> { imported });

        // 导入回落语义：预览上下文随新列表首项（与选中回落一致）。
        Assert.Same(imported, source.PreviewProfile);
    }

    [Fact]
    public void ImplementsIProfilePreviewSource_NoProfiles_ReturnsNull()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile>(), Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        IProfilePreviewSource source = vm;

        Assert.Null(source.PreviewProfile);
    }

    // --- 方案选择 -------------------------------------------------------------------

    [Fact]
    public void SelectProfile_Null_ReturnsFalseAndKeepsState()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.False(vm.SelectProfile(null));
        // T21：默认选中已在 VM 内，null 选择不清空当前选中与槽位。
        Assert.Same(vm.Profiles[0], vm.SelectedProfile);
        Assert.Equal(8, vm.Slots.Count);
    }

    [Fact]
    public void SelectProfile_RebuildsSlotsWithDirectionLabelsAndLiveActionReferences()
    {
        var profile = MakeProfile("Global", 8);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.True(vm.SelectProfile(vm.Profiles[0]));

        Assert.Same(vm.Profiles[0], vm.SelectedProfile);
        Assert.Equal(8, vm.Slots.Count);
        Assert.Equal("右 (E / 0°)", vm.Slots[0].DirectionLabel);
        Assert.Equal("右上 (NE / 315°)", vm.Slots[7].DirectionLabel);
        // 槽位包装的是模型动作的同一实例：编辑经 VM 直写模型（live-apply）
        Assert.Same(profile.Actions[3], vm.Slots[3].Action);
        Assert.Equal("动作 4", vm.Slots[3].Name);
    }

    [Fact]
    public void SelectProfile_RaisesSelectedProfileNotification()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile(), MakeProfile("chrome.exe", 4) }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        object? notified = null;
        vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.SelectedProfile)) notified = s; };

        vm.SelectProfile(vm.Profiles[1]); // 默认已选中首项，改选第二项才会触发通知

        Assert.NotNull(notified);
    }

    // --- 方向槽位集合重建（迁移前 RefreshSlots 行为） ---------------------------------

    [Fact]
    public void RebuildSlots_NormalizesInvalidSectorCountTo8SlotsWithoutWritingModelBack()
    {
        var profile = MakeProfile("Global", 6, actionCount: 0);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(8, vm.Slots.Count);
        Assert.Equal(6, profile.SectorCount); // 迁移前即不回写模型：仅展示层规范化
    }

    [Fact]
    public void RebuildSlots_12KeyProfile_FillsMissingActionsFromDefaultPresets()
    {
        var profile = MakeProfile("Global", 12, actionCount: 0);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(12, vm.Slots.Count);
        Assert.Equal(12, profile.Actions.Count);
        Assert.Equal("复制 (Copy)", profile.Actions[0].Name);
        Assert.Equal("Ctrl+C", profile.Actions[0].Parameter);
        Assert.Equal("剪切 (Cut)", profile.Actions[1].Name);
        Assert.Equal("任务管理器 (TaskMgr)", profile.Actions[11].Name);
        Assert.Equal("TaskManager", profile.Actions[11].Parameter);
    }

    [Fact]
    public void RebuildSlots_4KeyProfile_FillsMissingActionsFromDefaultPresets()
    {
        var profile = MakeProfile("Global", 4, actionCount: 0);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(4, vm.Slots.Count);
        Assert.Equal(new[] { "复制 (Copy)", "显示桌面 (Desktop)", "粘贴 (Paste)", "关闭窗口 (Close)" },
            profile.Actions.Select(a => a.Name));
    }

    [Fact]
    public void RebuildSlots_8KeyProfile_FillsPlaceholderNames()
    {
        var profile = MakeProfile("Global", 8, actionCount: 0);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(8, vm.Slots.Count);
        // 与迁移前一致：缺省预设仅用于 4/12 键，8 键全部按序补占位
        Assert.Equal("快捷动作 1", profile.Actions[0].Name);
        Assert.Equal("快捷动作 5", profile.Actions[4].Name);
        Assert.Equal("快捷动作 8", profile.Actions[7].Name);
    }

    [Fact]
    public void RebuildSlots_KeepsActionsBeyondSectorCount()
    {
        var profile = MakeProfile("Global", 4, actionCount: 6);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(4, vm.Slots.Count);
        Assert.Equal(6, profile.Actions.Count); // 多余动作不裁剪，与迁移前一致
    }

    [Fact]
    public void RebuildSlots_WithoutAnyProfile_LeavesSlotsEmpty()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile>(), Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        vm.RebuildSlots();

        Assert.Empty(vm.Slots);
        Assert.Null(vm.SelectedProfile);
    }

    // --- 扇区数切换 -------------------------------------------------------------------

    [Fact]
    public void ApplySectorCount_WritesModelAndRebuildsSlots()
    {
        var profile = MakeProfile("Global", 8);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        vm.SelectProfile(vm.Profiles[0]);

        Assert.True(vm.ApplySectorCount(12));

        Assert.Equal(12, profile.SectorCount);
        Assert.Equal(12, vm.Slots.Count);
        Assert.Equal(12, profile.Actions.Count);
        Assert.Equal("音量减小 (Vol-)", profile.Actions[8].Name); // 迁移前一致：用 12 键预设补齐
    }

    [Fact]
    public void ApplySectorCount_WithoutSelection_FallsBackToFirstProfile()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile("Global", 4), MakeProfile("chrome.exe", 4) }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.True(vm.ApplySectorCount(8));

        Assert.Same(vm.Profiles[0], vm.SelectedProfile); // 兜底第一方案，不改列表可视选中
        Assert.Equal(8, vm.SelectedSectorCount);
        Assert.Equal(8, vm.Slots.Count);
    }

    [Fact]
    public void ApplySectorCount_EmptyList_ReturnsFalse()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile>(), Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.False(vm.ApplySectorCount(8));
        Assert.Null(vm.SelectedProfile);
    }

    // --- 方案条目增删与展示刷新 ---------------------------------------------------------

    [Fact]
    public void AddProfile_AppendsToSourceListAndDisplayCollection()
    {
        var source = new List<WheelProfile> { MakeProfile() };
        var vm = new ProfileListViewModel(source, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        var added = new WheelProfile { ProcessName = "new.exe", SectorCount = 4 };

        var item = vm.AddProfile(added);

        Assert.Contains(added, source);           // 写入运行态配置列表（同一实例）
        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("new.exe", vm.Profiles[1].ProcessName);
        Assert.Same(added, item.Model);
    }

    [Fact]
    public void RemoveProfile_RemovesFromSourceListAndDisplayCollection()
    {
        var source = new List<WheelProfile> { MakeProfile(), MakeProfile("chrome.exe") };
        var vm = new ProfileListViewModel(source, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        var target = vm.Profiles[1];
        vm.SelectProfile(target);

        vm.RemoveProfile(target);

        Assert.DoesNotContain(target.Model, source);
        Assert.Single(vm.Profiles);
    }

    [Fact]
    public void RefreshDisplay_RaisesProcessNameChangeAfterModelRename()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile("old.exe") }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        var item = vm.Profiles[0];
        var notified = new List<string?>();
        item.PropertyChanged += (s, e) => notified.Add(e.PropertyName);

        item.Model.ProcessName = "new.exe";
        item.RefreshDisplay();

        Assert.Equal("new.exe", item.ProcessName);
        Assert.Contains(nameof(item.ProcessName), notified);
    }

    [Fact]
    public void Reload_RebuildsCollectionAndSelectsFirstOfNewList()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        vm.SelectProfile(vm.Profiles[0]);

        var newList = new List<WheelProfile> { MakeProfile("imported.exe", 4) };
        vm.Reload(newList);

        // T21：导入回落收编进 VM——重挂后选中新列表首项并重建槽位。
        Assert.Single(vm.Profiles);
        Assert.Equal("imported.exe", vm.Profiles[0].ProcessName);
        Assert.Same(vm.Profiles[0], vm.SelectedProfile);
        Assert.Equal(4, vm.SelectedSectorCount);
        Assert.Equal(4, vm.Slots.Count);
    }

    [Fact]
    public void Reload_EmptyList_ClearsSelectionAndSlots()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        vm.SelectProfile(vm.Profiles[0]);

        vm.Reload(new List<WheelProfile>());

        Assert.Empty(vm.Profiles);
        Assert.Null(vm.SelectedProfile);
        Assert.Empty(vm.Slots);
    }

    // --- 瞬态 VM 生命周期（T27/ADR-0010：槽位经本 VM Dispose；Dispose 后订阅清零） -------

    [Fact]
    public void RebuildSlots_DisposesOldSlots_SoLanguageHandlersDoNotAccumulate()
    {
        var original = Localization.CurrentLanguage;
        var before = I18nEventSubscriberCount();
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        var firstGeneration = vm.Slots.ToArray();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                vm.SelectProfile(vm.Profiles[0]); // 重复选中触发多次 RebuildSlots
            }

            // 只等于当前存活槽数（8）而非 rebuild 次数（3×8）累积；
            // before 之后：槽位每次重建先成对退订再新建，差值即当前槽的订阅。
            Assert.Equal(before + vm.Slots.Count, I18nEventSubscriberCount());
        }
        finally
        {
            foreach (var slot in firstGeneration) slot.Dispose(); // 防据实失败时泄漏静态订阅
            vm.Dispose();
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void RebuildSlots_OldSlotsBecomeCollectable_AfterDispose()
    {
        var original = Localization.CurrentLanguage;
        var (vm, weak) = CreateRebuiltProfileListWithDeadSlot();
        try
        {
            for (int i = 0; i < 5 && weak.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(weak.IsAlive); // 不再被 I18n 强引用 → 可回收
        }
        finally
        {
            vm.Dispose();
            Localization.SetLanguage(original);
        }
    }

    /// <summary>在独立方法内创建并重建，令旧槽局部引用随方法返回失效（Debug JIT 保活下仍可回收）。</summary>
    private static (ProfileListViewModel Vm, WeakReference WeakSlot) CreateRebuiltProfileListWithDeadSlot()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);
        var weak = new WeakReference(vm.Slots[0]);
        vm.ApplySectorCount(4); // 重建：旧槽被 Dispose（退订静态事件）并从集合移除
        return (vm, weak);
    }
    [Fact]
    public void Dispose_DisposesAllRemainingSlots_AndUnsubscribesTheirLanguageHandlers()
    {
        var original = Localization.CurrentLanguage;
        var before = I18nEventSubscriberCount();
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        try
        {
            vm.Dispose();

            Assert.Empty(vm.Slots);
            Assert.Equal(before, I18nEventSubscriberCount()); // 槽位全部退订，订阅清零回到基线
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var original = Localization.CurrentLanguage;
        var before = I18nEventSubscriberCount();
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        try
        {
            vm.Dispose();
            vm.Dispose(); // guard：重复释放不重复退订

            Assert.Equal(before, I18nEventSubscriberCount());
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void Dispose_AfterRebuild_StillLeavesNoSubscribers()
    {
        var original = Localization.CurrentLanguage;
        var before = I18nEventSubscriberCount();
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile(), MakeProfile("chrome.exe", 4) }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        try
        {
            vm.SelectProfile(vm.Profiles[1]);
            vm.Dispose();

            Assert.Equal(before, I18nEventSubscriberCount());
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    // --- 槽位名称编辑（迁移前行为锁定：直写模型、无验证） -------------------------------

    [Fact]
    public void SlotName_Set_WritesThroughToActionAndRaisesChange()
    {
        var action = new ActionItem { Type = "Hotkey", Name = "旧名" };
        var slot = new SlotViewModel("右 (E / 0°)", action, Dialogs(), new TestActionExecutor(), TestHub.NewMessenger(), Localization);
        var names = new List<string?>();
        slot.PropertyChanged += (s, e) => names.Add(e.PropertyName);

        slot.Name = "  新名  "; // 与迁移前一致：不去空白直写

        Assert.Equal("  新名  ", action.Name);
        Assert.Contains(nameof(slot.Name), names);
    }

    [Fact]
    public void SlotName_SetSameValue_DoesNotRaiseChange()
    {
        var action = new ActionItem { Type = "Hotkey", Name = "同名" };
        var slot = new SlotViewModel("右 (E / 0°)", action, Dialogs(), new TestActionExecutor(), TestHub.NewMessenger(), Localization);
        var raised = false;
        slot.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(slot.Name)) raised = true; };

        slot.Name = "同名";

        Assert.False(raised);
        Assert.Equal("同名", action.Name);
    }

    [Fact]
    public void SlotName_Get_NullActionName_ReturnsEmpty()
    {
        var slot = new SlotViewModel("下 (S / 90°)", new ActionItem { Name = null! }, Dialogs(), new TestActionExecutor(), TestHub.NewMessenger(), Localization);

        Assert.Equal("", slot.Name);
    }

    [Fact]
    public void SlotConstructor_NullAction_CreatesDefaultHotkeyAction()
    {
        var slot = new SlotViewModel("左 (W / 180°)", null!, Dialogs(), new TestActionExecutor(), TestHub.NewMessenger(), Localization);

        Assert.Equal("左 (W / 180°)", slot.DirectionLabel);
        Assert.Equal("Hotkey", slot.Action.Type);
        Assert.Equal("快捷动作", slot.Action.Name);
        Assert.Equal("", slot.Action.Parameter);
    }

    [Fact]
    public void SlotPassthroughProperties_WriteThroughToAction()
    {
        var action = new ActionItem();
        var slot = new SlotViewModel("上 (N / 270°)", action, Dialogs(), new TestActionExecutor(), TestHub.NewMessenger(), Localization);

        slot.Parameter = "Ctrl+Shift+Esc";
        slot.Arguments = "--minimized";
        slot.IconKey = "TaskManager";

        Assert.Equal("Ctrl+Shift+Esc", action.Parameter);
        Assert.Equal("--minimized", action.Arguments);
        Assert.Equal("TaskManager", action.IconKey);
        Assert.Equal("TaskManager", slot.IconDisplayText);
    }

    // --- 槽位编辑提交转发 (T12) ---------------------------------------------------------

    [Fact]
    public void SlotEditApplied_BubblesUpAsSlotEditCommitted()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Work") };
        var (messenger, save) = SaveSpy.Create();
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, dialogs, messenger, new TestActionExecutor(), Localization);
        vm.SelectProfile(vm.Profiles[0]);

        vm.Slots[2].BrowseFolderCommand.Execute(null); // 文件夹选择提交经槽位上报立即落盘请求

        Assert.Equal(1, save.Immediate);
    }

    [Fact]
    public void SlotEditCommitted_AfterRebuild_SubscribesOnlyToCurrentSlots()
    {
        var dialogs = new TestDialogService { FolderToPick = new FilePickResult(@"C:\Work") };
        var (messenger, save) = SaveSpy.Create();
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, dialogs, messenger, new TestActionExecutor(), Localization);
        vm.SelectProfile(vm.Profiles[0]);

        vm.ApplySectorCount(4); // 重建槽位集合（T19 起扇区数应用自身发一次立即落盘请求）
        var before = save.Immediate;
        vm.Slots[0].BrowseFolderCommand.Execute(null);

        Assert.Equal(before + 1, save.Immediate); // 重建后的新槽位仍上报落盘请求
    }

    // --- 名称查重与缺省名（T16 自窗口 code-behind 收编） -----------------------------

    [Fact]
    public void IsProcessNameTaken_MatchesCaseInsensitiveAgainstRuntimeProfiles()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile("Global"), MakeProfile("chrome.exe", 4) }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.True(vm.IsProcessNameTaken("Chrome.EXE"));
        Assert.True(vm.IsProcessNameTaken("global"));
        Assert.False(vm.IsProcessNameTaken("myapp.exe"));
    }

    [Fact]
    public void IsProcessNameTaken_SeesProfilesAddedAtRuntime()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        vm.AddProfile(MakeProfile("newapp.exe"));

        Assert.True(vm.IsProcessNameTaken("NewApp.exe"));
    }

    [Fact]
    public void CreateDefaultCustomProfileName_UsesCurrentProfileCount()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile(), MakeProfile("chrome.exe") }, Dialogs(), TestHub.NewMessenger(), new TestActionExecutor(), Localization);

        Assert.Equal("自定义配置_2", vm.CreateDefaultCustomProfileName());
    }
}
