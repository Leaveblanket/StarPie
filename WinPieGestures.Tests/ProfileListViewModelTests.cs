using System;
using System.Collections.Generic;
using System.Linq;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 配置方案分区列表侧 ViewModel 的行为覆盖 (T11, ADR-0001)：方案列表与选中态、
/// 扇区数切换、方向槽位集合（方位角标签、缺省动作补齐、扇区数规范化）以及
/// 槽位名称编辑——全部锁定迁移前 SettingsWindow code-behind 的外部行为。
/// 直接 new 被测对象，不触碰 ConfigManager 静态态。
/// </summary>
public sealed class ProfileListViewModelTests
{
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

    // --- 构造与列表展示 -------------------------------------------------------------

    [Fact]
    public void Constructor_WrapsAllSourceProfilesAndStartsUnselected()
    {
        var source = new List<WheelProfile> { MakeProfile("Global"), MakeProfile("chrome.exe", 4) };

        var vm = new ProfileListViewModel(source);

        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("Global", vm.Profiles[0].ProcessName);
        Assert.Equal("chrome.exe", vm.Profiles[1].ProcessName);
        Assert.Null(vm.SelectedProfile);
        Assert.Null(vm.SelectedSectorCount);
        Assert.Empty(vm.Slots);
    }

    // --- 方案选择 -------------------------------------------------------------------

    [Fact]
    public void SelectProfile_Null_ReturnsFalseAndKeepsState()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() });

        Assert.False(vm.SelectProfile(null));
        Assert.Null(vm.SelectedProfile);
        Assert.Empty(vm.Slots);
    }

    [Fact]
    public void SelectProfile_RebuildsSlotsWithDirectionLabelsAndLiveActionReferences()
    {
        var profile = MakeProfile("Global", 8);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile });

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
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() });
        object? notified = null;
        vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.SelectedProfile)) notified = s; };

        vm.SelectProfile(vm.Profiles[0]);

        Assert.NotNull(notified);
    }

    // --- 方向槽位集合重建（迁移前 RefreshSlots 行为） ---------------------------------

    [Fact]
    public void RebuildSlots_NormalizesInvalidSectorCountTo8SlotsWithoutWritingModelBack()
    {
        var profile = MakeProfile("Global", 6, actionCount: 0);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile });
        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(8, vm.Slots.Count);
        Assert.Equal(6, profile.SectorCount); // 迁移前即不回写模型：仅展示层规范化
    }

    [Fact]
    public void RebuildSlots_12KeyProfile_FillsMissingActionsFromDefaultPresets()
    {
        var profile = MakeProfile("Global", 12, actionCount: 0);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile });

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
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile });

        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(4, vm.Slots.Count);
        Assert.Equal(new[] { "复制 (Copy)", "显示桌面 (Desktop)", "粘贴 (Paste)", "关闭窗口 (Close)" },
            profile.Actions.Select(a => a.Name));
    }

    [Fact]
    public void RebuildSlots_8KeyProfile_FillsPlaceholderNames()
    {
        var profile = MakeProfile("Global", 8, actionCount: 0);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile });

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
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile });

        vm.SelectProfile(vm.Profiles[0]);

        Assert.Equal(4, vm.Slots.Count);
        Assert.Equal(6, profile.Actions.Count); // 多余动作不裁剪，与迁移前一致
    }

    [Fact]
    public void RebuildSlots_WithoutAnyProfile_LeavesSlotsEmpty()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile>());

        vm.RebuildSlots();

        Assert.Empty(vm.Slots);
        Assert.Null(vm.SelectedProfile);
    }

    // --- 扇区数切换 -------------------------------------------------------------------

    [Fact]
    public void ApplySectorCount_WritesModelAndRebuildsSlots()
    {
        var profile = MakeProfile("Global", 8);
        var vm = new ProfileListViewModel(new List<WheelProfile> { profile });
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
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile("Global", 4), MakeProfile("chrome.exe", 4) });

        Assert.True(vm.ApplySectorCount(8));

        Assert.Same(vm.Profiles[0], vm.SelectedProfile); // 兜底第一方案，不改列表可视选中
        Assert.Equal(8, vm.SelectedSectorCount);
        Assert.Equal(8, vm.Slots.Count);
    }

    [Fact]
    public void ApplySectorCount_EmptyList_ReturnsFalse()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile>());

        Assert.False(vm.ApplySectorCount(8));
        Assert.Null(vm.SelectedProfile);
    }

    // --- 方案条目增删与展示刷新 ---------------------------------------------------------

    [Fact]
    public void AddProfile_AppendsToSourceListAndDisplayCollection()
    {
        var source = new List<WheelProfile> { MakeProfile() };
        var vm = new ProfileListViewModel(source);
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
        var vm = new ProfileListViewModel(source);
        var target = vm.Profiles[1];
        vm.SelectProfile(target);

        vm.RemoveProfile(target);

        Assert.DoesNotContain(target.Model, source);
        Assert.Single(vm.Profiles);
    }

    [Fact]
    public void RefreshDisplay_RaisesProcessNameChangeAfterModelRename()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile("old.exe") });
        var item = vm.Profiles[0];
        var notified = new List<string?>();
        item.PropertyChanged += (s, e) => notified.Add(e.PropertyName);

        item.Model.ProcessName = "new.exe";
        item.RefreshDisplay();

        Assert.Equal("new.exe", item.ProcessName);
        Assert.Contains(nameof(item.ProcessName), notified);
    }

    [Fact]
    public void Reload_RebuildsCollectionAndClearsSelectionAndSlots()
    {
        var vm = new ProfileListViewModel(new List<WheelProfile> { MakeProfile() });
        vm.SelectProfile(vm.Profiles[0]);

        var newList = new List<WheelProfile> { MakeProfile("imported.exe", 4) };
        vm.Reload(newList);

        Assert.Null(vm.SelectedProfile);
        Assert.Empty(vm.Slots);
        Assert.Single(vm.Profiles);
        Assert.Equal("imported.exe", vm.Profiles[0].ProcessName);
    }

    // --- 槽位名称编辑（迁移前行为锁定：直写模型、无验证） -------------------------------

    [Fact]
    public void SlotName_Set_WritesThroughToActionAndRaisesChange()
    {
        var action = new ActionItem { Type = "Hotkey", Name = "旧名" };
        var slot = new SlotViewModel("右 (E / 0°)", action);
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
        var slot = new SlotViewModel("右 (E / 0°)", action);
        var raised = false;
        slot.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(slot.Name)) raised = true; };

        slot.Name = "同名";

        Assert.False(raised);
        Assert.Equal("同名", action.Name);
    }

    [Fact]
    public void SlotName_Get_NullActionName_ReturnsEmpty()
    {
        var slot = new SlotViewModel("下 (S / 90°)", new ActionItem { Name = null! });

        Assert.Equal("", slot.Name);
    }

    [Fact]
    public void SlotConstructor_NullAction_CreatesDefaultHotkeyAction()
    {
        var slot = new SlotViewModel("左 (W / 180°)", null!);

        Assert.Equal("左 (W / 180°)", slot.DirectionLabel);
        Assert.Equal("Hotkey", slot.Action.Type);
        Assert.Equal("快捷动作", slot.Action.Name);
        Assert.Equal("", slot.Action.Parameter);
    }

    [Fact]
    public void SlotPassthroughProperties_WriteThroughToAction()
    {
        var action = new ActionItem();
        var slot = new SlotViewModel("上 (N / 270°)", action);

        slot.Parameter = "Ctrl+Shift+Esc";
        slot.Arguments = "--minimized";
        slot.IconKey = "TaskManager";

        Assert.Equal("Ctrl+Shift+Esc", action.Parameter);
        Assert.Equal("--minimized", action.Arguments);
        Assert.Equal("TaskManager", action.IconKey);
        Assert.Equal("TaskManager", slot.IconDisplayText);
    }
}
