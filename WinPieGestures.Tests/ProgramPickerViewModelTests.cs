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
    /// <summary>对话框服务假实现：只落地 VM 依赖的 ShowOpenFileDialog/ShowInfo，其余不该被调用。</summary>
    private sealed class FakeDialogService : IDialogService
    {
        public FilePickResult? OpenFileDialogResult;
        public string? LastFilter;
        public int OpenFileCalls;
        public List<(string Title, string Message)> InfoCalls { get; } = new();

        public FilePickResult? ShowOpenFileDialog(string filter, string? title = null)
        {
            OpenFileCalls++;
            LastFilter = filter;
            return OpenFileDialogResult;
        }

        public FilePickResult? ShowSaveFileDialog(string filter, string? fileName = null, string? title = null) => throw new NotSupportedException();
        public FilePickResult? ShowFolderDialog(string? initialDirectory = null, string? title = null) => throw new NotSupportedException();
        public bool Confirm(string title, string message) => throw new NotSupportedException();
        public void ShowInfo(string title, string message) => InfoCalls.Add((title, message));

        public ProgramPickResult? ShowProgramPicker() => throw new NotSupportedException();
        public InputDialogResult? ShowInputDialog(string title, string prompt, string defaultText = "", Func<string, (bool IsValid, string ErrorMessage)>? validator = null) => throw new NotSupportedException();
        public IconPickResult? ShowIconPicker(string? currentIconKey) => throw new NotSupportedException();
        public ColorPickResult? ShowColorPicker(string initialHex) => throw new NotSupportedException();
        public EyedropResult? ShowEyedropper() => throw new NotSupportedException();
    }

    private static ProgramEntry Entry(string name, string path)
        => new(name, path, path, IconSource: null);

    private static ProgramPickerViewModel Create(
        Func<IReadOnlyList<ProgramEntry>>? scan = null,
        FakeDialogService? dialogs = null)
        => new(scan ?? (() => new List<ProgramEntry>()), dialogs ?? new FakeDialogService());

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
        var dialogs = new FakeDialogService();
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
        var dialogs = new FakeDialogService { OpenFileDialogResult = new FilePickResult(@"C:\Tools\mytool.exe") };
        var vm = Create(dialogs: dialogs);

        vm.BrowseManuallyCommand.Execute(null);

        Assert.Equal(1, dialogs.OpenFileCalls);
        Assert.Contains("*.exe", dialogs.LastFilter);
        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.Equal("mytool", vm.Result!.Name);
        Assert.Equal(@"C:\Tools\mytool.exe", vm.Result.Path);
    }

    [Fact]
    public void BrowseManually_PickerCancelled_StaysOpenWithoutCompleting()
    {
        var dialogs = new FakeDialogService { OpenFileDialogResult = null };
        var vm = Create(dialogs: dialogs);

        vm.BrowseManuallyCommand.Execute(null);

        Assert.Equal(1, dialogs.OpenFileCalls);
        Assert.False(vm.IsCompleted);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void BrowseManually_UnresolvableLnk_FallsBackToLnkPathItself()
    {
        // 不存在的 .lnk 解析失败（IconHelper 早退于 File.Exists，测试中不触 COM），
        // 沿用旧行为：路径回落为所选 .lnk 本身，显示名取文件名。
        var dialogs = new FakeDialogService { OpenFileDialogResult = new FilePickResult(@"C:\fake\missing.lnk") };
        var vm = Create(dialogs: dialogs);

        vm.BrowseManuallyCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.Equal("missing", vm.Result!.Name);
        Assert.Equal(@"C:\fake\missing.lnk", vm.Result.Path);
    }
}
