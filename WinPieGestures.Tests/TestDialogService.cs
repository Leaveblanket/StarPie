using System;
using System.Collections.Generic;

namespace WinPieGestures.Tests;

/// <summary>
/// IDialogService 的测试替身（工程约定：mock 直接 new，不使用 mocking 框架）：
/// 按方法预设返回值并记录调用参数，供对话框编排类 ViewModel 测试断言外部行为。
/// 未预设的方法返回 null；与本票无关的对话框方法抛出 NotSupportedException，
/// 避免测试静默走过不相关的编排路径。
/// </summary>
public sealed class TestDialogService : IDialogService
{
    /// <summary>ShowProgramPicker 的预设返回值。</summary>
    public ProgramPickResult? ProgramToPick { get; set; }

    /// <summary>ShowIconPicker 的预设返回值。</summary>
    public IconPickResult? IconToPick { get; set; }

    /// <summary>ShowFolderDialog 的预设返回值。</summary>
    public FilePickResult? FolderToPick { get; set; }

    /// <summary>ShowOpenFileDialog 的预设返回值。</summary>
    public FilePickResult? OpenFileToPick { get; set; }

    /// <summary>ShowSaveFileDialog 的预设返回值。</summary>
    public FilePickResult? SaveFileToPick { get; set; }

    public int ProgramPickerCallCount { get; private set; }

    /// <summary>每次 ShowIconPicker 收到的 currentIconKey 实参。</summary>
    public List<string?> IconPickerCalls { get; } = new();

    /// <summary>每次 ShowFolderDialog 收到的 initialDirectory 实参。</summary>
    public List<string?> FolderDialogInitialDirectories { get; } = new();

    /// <summary>每次 ShowOpenFileDialog 收到的 (filter, title) 实参。</summary>
    public List<(string Filter, string? Title)> OpenFileDialogCalls { get; } = new();

    /// <summary>每次 ShowSaveFileDialog 收到的 (filter, fileName, title) 实参。</summary>
    public List<(string Filter, string? FileName, string? Title)> SaveFileDialogCalls { get; } = new();

    public ProgramPickResult? ShowProgramPicker()
    {
        ProgramPickerCallCount++;
        return ProgramToPick;
    }

    public InputDialogResult? ShowInputDialog(
        string title,
        string prompt,
        string defaultText = "",
        Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        => throw new NotSupportedException("本测试场景不涉及输入框。");

    public IconPickResult? ShowIconPicker(string? currentIconKey)
    {
        IconPickerCalls.Add(currentIconKey);
        return IconToPick;
    }

    public ColorPickResult? ShowColorPicker(string initialHex)
        => throw new NotSupportedException("本测试场景不涉及颜色选择器。");

    public EyedropResult? ShowEyedropper()
        => throw new NotSupportedException("本测试场景不涉及屏上取色。");

    public FilePickResult? ShowOpenFileDialog(string filter, string? title = null)
    {
        OpenFileDialogCalls.Add((filter, title));
        return OpenFileToPick;
    }

    public FilePickResult? ShowSaveFileDialog(string filter, string? fileName = null, string? title = null)
    {
        SaveFileDialogCalls.Add((filter, fileName, title));
        return SaveFileToPick;
    }

    public FilePickResult? ShowFolderDialog(string? initialDirectory = null, string? title = null)
    {
        FolderDialogInitialDirectories.Add(initialDirectory);
        return FolderToPick;
    }

    /// <summary>Confirm 的预设返回值（true = 用户选"是"）。</summary>
    public bool ConfirmResult { get; set; }

    /// <summary>每次 Confirm 收到的 (title, message) 实参。</summary>
    public List<(string Title, string Message)> ConfirmCalls { get; } = new();

    /// <summary>每次 ShowInfo 收到的 (title, message) 实参。</summary>
    public List<(string Title, string Message)> InfoCalls { get; } = new();

    public bool Confirm(string title, string message)
    {
        ConfirmCalls.Add((title, message));
        return ConfirmResult;
    }

    public void ShowInfo(string title, string message)
    {
        InfoCalls.Add((title, message));
    }
}
