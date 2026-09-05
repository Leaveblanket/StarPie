using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 程序选择器 ViewModel 的行为覆盖 (T06)：扫描编排（注入假扫描委托）、搜索过滤接线、
/// 选择结果与手动浏览编排（mock 对话框服务）。T20 起完成经
/// <see cref="ProgramPickerViewModel.IsCompleted"/> 可观察状态驱动，无效选择提示经 IDialogService。
/// </summary>
public sealed class ProgramPickerViewModelTests
{
    private static readonly LocalizationService Localization = new();

    private static ProgramEntry Entry(string name, string path)
        => new(name, path, path, IconSource: null);

    private static ProgramPickerViewModel Create(
        Func<IReadOnlyList<ProgramEntry>>? scan = null,
        TestDialogService? dialogs = null)
        => new(scan ?? (() => new List<ProgramEntry>()), dialogs ?? new TestDialogService(), Localization);

    // --- 扫描编排 ---------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_PopulatesDisplayedPrograms_AndHidesStatus()
    {
        var vm = Create(scan: () => new List<ProgramEntry>
        {
            Entry("记事本", @"C:\Windows\notepad.exe"),
            Entry("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe")
        });

        await vm.LoadAsync();

        Assert.Equal(2, vm.DisplayedPrograms.Count);
        Assert.Equal("记事本", vm.DisplayedPrograms[0].Name); // 扫描侧已排序，VM 保序
        Assert.False(vm.IsStatusVisible);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_ScanThrows_ShowsErrorStatus()
    {
        var vm = Create(scan: () => throw new InvalidOperationException("boom"));

        await vm.LoadAsync();

        Assert.True(vm.HasError);
        Assert.True(vm.IsStatusVisible);
        Assert.Contains("boom", vm.StatusText);
        Assert.Empty(vm.DisplayedPrograms);
    }

    // --- 搜索过滤 -----------------------------------------------------------------

    [Fact]
    public async Task SearchText_FiltersDisplayedPrograms()
    {
        var vm = Create(scan: () => new List<ProgramEntry>
        {
            Entry("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            Entry("VS Code", @"C:\Programs\Microsoft VS Code\Code.exe")
        });
        await vm.LoadAsync();

        vm.SearchText = "chrome";

        var entry = Assert.Single(vm.DisplayedPrograms);
        Assert.Equal("Google Chrome", entry.Name);

        vm.SearchText = "";

        Assert.Equal(2, vm.DisplayedPrograms.Count);
    }

    // --- 选择结果 -----------------------------------------------------------------

    [Fact]
    public async Task BuildResult_WithoutSelection_ReturnsNull()
    {
        var vm = Create(scan: () => new List<ProgramEntry> { Entry("A", @"C:\a.exe") });
        await vm.LoadAsync();

        Assert.Null(vm.BuildResult());
    }

    [Fact]
    public async Task BuildResult_WithSelection_CarriesNameAndPath()
    {
        var vm = Create(scan: () => new List<ProgramEntry> { Entry("VS Code", @"C:\Apps\code.exe") });
        await vm.LoadAsync();
        vm.SelectedProgram = vm.DisplayedPrograms.Single();

        var result = vm.BuildResult();

        Assert.NotNull(result);
        Assert.Equal("VS Code", result!.Name);
        Assert.Equal(@"C:\Apps\code.exe", result.Path);
    }

    [Fact]
    public void Confirm_WithoutSelection_ShowsNoticeAndDoesNotComplete()
    {
        var dialogs = new TestDialogService();
        var vm = Create(dialogs: dialogs);

        vm.ConfirmCommand.Execute(null);

        // 未选中：经对话框服务提示"请选择"，窗口保持打开、不产生结果
        var call = Assert.Single(dialogs.InfoCalls);
        Assert.Contains("请选择", call.Message);
        Assert.False(vm.IsCompleted);
        Assert.Null(vm.Result);
        Assert.Null(vm.BuildResult());
    }

    [Fact]
    public async Task Confirm_WithSelection_CompletesWithResult()
    {
        var vm = Create(scan: () => new List<ProgramEntry> { Entry("A", @"C:\a.exe") });
        await vm.LoadAsync();
        vm.SelectedProgram = vm.DisplayedPrograms.Single();

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.Equal(@"C:\a.exe", vm.Result!.Path);
    }

    // --- 手动浏览（mock 对话框服务） -------------------------------------------------

    [Fact]
    public void BrowseManually_PickedExe_CompletesWithNameFromFileName()
    {
        var dialogs = new TestDialogService { OpenFileToPick = new FilePickResult(@"C:\Tools\mytool.exe") };
        var vm = Create(dialogs: dialogs);

        vm.BrowseManuallyCommand.Execute(null);

        var openCall = Assert.Single(dialogs.OpenFileDialogCalls);
        Assert.Contains("*.exe", openCall.Filter);
        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.Equal("mytool", vm.Result!.Name);
        Assert.Equal(@"C:\Tools\mytool.exe", vm.Result.Path);
    }

    [Fact]
    public void BrowseManually_PickerCancelled_StaysOpenWithoutCompleting()
    {
        var dialogs = new TestDialogService { OpenFileToPick = null };
        var vm = Create(dialogs: dialogs);

        vm.BrowseManuallyCommand.Execute(null);

        Assert.Single(dialogs.OpenFileDialogCalls);
        Assert.False(vm.IsCompleted);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void BrowseManually_UnresolvableLnk_FallsBackToLnkPathItself()
    {
        // 不存在的 .lnk 解析失败（ShortcutResolver 早退于 File.Exists，测试中不触 COM），
        // 沿用旧行为：路径回落为所选 .lnk 本身，显示名取文件名。
        var dialogs = new TestDialogService { OpenFileToPick = new FilePickResult(@"C:\fake\missing.lnk") };
        var vm = Create(dialogs: dialogs);

        vm.BrowseManuallyCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.Equal("missing", vm.Result!.Name);
        Assert.Equal(@"C:\fake\missing.lnk", vm.Result.Path);
    }
}
