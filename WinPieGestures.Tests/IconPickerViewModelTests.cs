using System;
using System.Collections.Generic;
using System.Linq;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 图标选择器 ViewModel 的行为覆盖 (T08)：过滤规则（自定义按显示名/键、内置按显示名/分类/键）、
/// 初始选中恢复、选择/清空、确认结果、导入编排（mock 对话框服务）与删除编排（注入委托）。
/// T20 起完成经 <see cref="IconPickerViewModel.IsCompleted"/> 可观察状态驱动，导入失败提示经 IDialogService。
/// </summary>
public sealed class IconPickerViewModelTests
{
    private static readonly LocalizationService Localization = new();

    private static IconHelper.CustomIconItem Custom(string key, string displayName, bool isSvg = false)
        => new() { Key = key, DisplayName = displayName, FilePath = @"C:\icons\" + key + (isSvg ? ".svg" : ".png"), SvgData = isSvg ? "M0,0L1,1" : "" };

    private static VectorIconItem Vector(string key, string displayName, string category = "测试分类")
        => new() { Key = key, Category = category, DisplayName = displayName, SvgData = "M0,0L1,1" };

    private static IconPickerViewModel Create(
        List<IconHelper.CustomIconItem>? customs = null,
        List<VectorIconItem>? vectors = null,
        TestDialogService? dialogs = null,
        string? initialKey = null,
        Func<string, bool>? deleteIcon = null,
        Func<string, IconHelper.CustomIconItem?>? importIcon = null)
        => new(
            () => customs ?? new List<IconHelper.CustomIconItem>(),
            () => vectors ?? new List<VectorIconItem>(),
            dialogs ?? new TestDialogService(),
            Localization,
            initialKey,
            deleteIcon,
            importIcon);

    // --- 过滤 -----------------------------------------------------------------

    [Fact]
    public void ApplyFilter_Empty_ShowsCustomsFirstThenVectors()
    {
        var vm = Create(
            customs: new List<IconHelper.CustomIconItem> { Custom("custom:star", "star") },
            vectors: new List<VectorIconItem> { Vector("Copy", "复制 (Copy)") });
        vm.ApplyFilter(null);

        Assert.Equal(2, vm.DisplayedIcons.Count);
        Assert.True(vm.DisplayedIcons[0].IsCustom); // 自定义图标在前
        Assert.Equal("Copy", vm.DisplayedIcons[1].Key);
    }

    [Fact]
    public void SearchText_FiltersCustomsByDisplayNameOrKey_CaseInsensitive()
    {
        var vm = Create(
            customs: new List<IconHelper.CustomIconItem> { Custom("custom:star", "MyStar"), Custom("custom:moon", "月亮") },
            vectors: new List<VectorIconItem> { Vector("Copy", "复制 (Copy)") });

        vm.SearchText = "STAR"; // 只命中自定义的显示名（忽略大小写）

        var entry = Assert.Single(vm.DisplayedIcons);
        Assert.Equal("custom:star", entry.Key);

        vm.SearchText = "custom:moon"; // 命中自定义的键

        Assert.Equal("custom:moon", Assert.Single(vm.DisplayedIcons).Key);
    }

    [Fact]
    public void SearchText_FiltersVectorsByDisplayNameCategoryOrKey()
    {
        var vm = Create(
            vectors: new List<VectorIconItem>
            {
                Vector("Copy", "复制 (Copy)", "编辑与剪贴板"),
                Vector("Shutdown", "关闭电脑 (Shutdown)", "多媒体与系统"),
                Vector("Folder", "打开文件夹 (Folder)", "生产力工具")
            });

        vm.SearchText = "剪贴板"; // 分类命中

        Assert.Equal("Copy", Assert.Single(vm.DisplayedIcons).Key);

        vm.SearchText = "shutdown"; // 显示名命中（忽略大小写）

        Assert.Equal("Shutdown", Assert.Single(vm.DisplayedIcons).Key);

        vm.SearchText = "folder"; // 键命中

        Assert.Equal("Folder", Assert.Single(vm.DisplayedIcons).Key);
    }

    [Fact]
    public void SearchText_IsTrimmedBeforeFiltering()
    {
        var vm = Create(vectors: new List<VectorIconItem> { Vector("Copy", "复制 (Copy)") });

        vm.SearchText = "  copy  ";

        Assert.Equal("Copy", Assert.Single(vm.DisplayedIcons).Key);
    }

    // --- 初始选中恢复 -------------------------------------------------------------

    [Fact]
    public void InitialKey_MatchingVector_RestoresSelectionLabel()
    {
        var vm = Create(vectors: new List<VectorIconItem> { Vector("Copy", "复制 (Copy)") }, initialKey: "Copy");
        vm.ApplyFilter(null);

        Assert.Equal("Copy", vm.SelectedIconKey);
        Assert.Equal("复制 (Copy)", vm.SelectedIconDisplayName);
    }

    [Fact]
    public void InitialKey_MatchingCustom_RestoresLabelWithCustomSuffix()
    {
        var vm = Create(
            customs: new List<IconHelper.CustomIconItem> { Custom("custom:star", "star") },
            initialKey: "custom:star");
        vm.ApplyFilter(null);

        Assert.Equal("star (自定义)", vm.SelectedIconDisplayName);
    }

    [Fact]
    public void InitialKey_Empty_LabelIsLocalizedNone()
    {
        var vm = Create(initialKey: null);
        vm.ApplyFilter(null);

        Assert.Null(vm.SelectedIconKey);
        Assert.Equal(Localization.GetString("IconPickerNone"), vm.SelectedIconDisplayName);
    }

    // --- 选择 / 清空 / 确认 ---------------------------------------------------------

    [Fact]
    public void Select_UpdatesKeyAndLabel()
    {
        var vm = Create(vectors: new List<VectorIconItem> { Vector("Copy", "复制 (Copy)") });
        vm.ApplyFilter(null);

        vm.Select(vm.DisplayedIcons.Single());

        Assert.Equal("Copy", vm.SelectedIconKey);
        Assert.Equal("复制 (Copy)", vm.SelectedIconDisplayName);
    }

    [Fact]
    public void ClearIcon_SetsEmptyKeyAndNoneLabel()
    {
        var vm = Create(vectors: new List<VectorIconItem> { Vector("Copy", "复制 (Copy)") }, initialKey: "Copy");
        vm.ApplyFilter(null);

        vm.ClearIconCommand.Execute(null);

        Assert.Equal("", vm.SelectedIconKey);
        Assert.Equal("(无图标)", vm.SelectedIconDisplayName);
    }

    [Fact]
    public void Confirm_CompletesWithCurrentKey()
    {
        var vm = Create(vectors: new List<VectorIconItem> { Vector("Copy", "复制 (Copy)") }, initialKey: "Copy");
        vm.ApplyFilter(null);

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.Equal("Copy", vm.BuildResult().IconKey);
    }

    [Fact]
    public void Confirm_WithoutSelection_ResultIconKeyIsNull()
    {
        // 迁移前行为：未做任何选择时确认，结果携带 null 键（调用方以 ?? "" 收敛）。
        var vm = Create();

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.Null(vm.BuildResult().IconKey);
    }

    // --- 导入（mock 对话框服务） ------------------------------------------------------

    [Fact]
    public void Import_PickedFile_SelectsImportedKeyAndRebuildsList()
    {
        var customs = new List<IconHelper.CustomIconItem> { Custom("custom:old", "old") };
        var dialogs = new TestDialogService { OpenFileToPick = new FilePickResult(@"C:\icons\new.svg") };
        var vm = Create(
            customs: customs,
            dialogs: dialogs,
            // 真实 IconHelper 导入后刷新缓存，这里同样把新图标并入来源列表。
            importIcon: _ => { var item = Custom("custom:new", "new", isSvg: true); customs.Add(item); return item; });
        vm.ApplyFilter(null);

        vm.ImportIconCommand.Execute(null);

        var openCall = Assert.Single(dialogs.OpenFileDialogCalls);
        Assert.Contains("*.svg", openCall.Filter);
        Assert.Equal("custom:new", vm.SelectedIconKey);
        Assert.Equal("new (自定义)", vm.SelectedIconDisplayName);
        Assert.Contains(vm.DisplayedIcons, e => e.Key == "custom:new");
    }

    [Fact]
    public void Import_Cancelled_StaysOpenWithoutChange()
    {
        var dialogs = new TestDialogService { OpenFileToPick = null };
        var importCalls = 0;
        var vm = Create(dialogs: dialogs, importIcon: _ => { importCalls++; return null; });
        vm.ApplyFilter(null);

        vm.ImportIconCommand.Execute(null);

        Assert.Single(dialogs.OpenFileDialogCalls);
        Assert.Equal(0, importCalls); // 取消后不应尝试导入
        Assert.Null(vm.SelectedIconKey);
    }

    [Fact]
    public void Import_Throws_ShowsInfo()
    {
        var dialogs = new TestDialogService { OpenFileToPick = new FilePickResult(@"C:\icons\broken.svg") };
        var vm = Create(dialogs: dialogs, importIcon: _ => throw new InvalidOperationException("disk full"));
        vm.ApplyFilter(null);

        vm.ImportIconCommand.Execute(null);

        var call = Assert.Single(dialogs.InfoCalls);
        Assert.Contains("disk full", call.Message);
        Assert.False(vm.IsCompleted);
    }

    // --- 删除（注入委托） -------------------------------------------------------------

    [Fact]
    public void DeleteCustomIcon_Success_RebuildsList()
    {
        var customs = new List<IconHelper.CustomIconItem> { Custom("custom:star", "star") };
        var deleted = new List<string>();
        var vm = Create(
            customs: customs,
            deleteIcon: key => { deleted.Add(key); return customs.Remove(customs.Single(i => i.Key == key)); });
        vm.ApplyFilter(null);
        Assert.Single(vm.DisplayedIcons);

        vm.DeleteCustomIconActionCommand.Execute("custom:star");

        Assert.Equal(["custom:star"], deleted);
        Assert.Empty(vm.DisplayedIcons); // 重建后自定义图标已不在来源里
    }

    [Fact]
    public void DeleteCustomIcon_Failure_ListUnchanged()
    {
        var vm = Create(
            customs: new List<IconHelper.CustomIconItem> { Custom("custom:star", "star") },
            deleteIcon: _ => false);
        vm.ApplyFilter(null);

        vm.DeleteCustomIconActionCommand.Execute("custom:star");

        Assert.Single(vm.DisplayedIcons);
    }
}
