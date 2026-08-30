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

    public int ProgramPickerCallCount { get; private set; }

    /// <summary>每次 ShowIconPicker 收到的 currentIconKey 实参。</summary>
    public List<string?> IconPickerCalls { get; } = new();

    /// <summary>每次 ShowFolderDialog 收到的 initialDirectory 实参。</summary>
    public List<string?> FolderDialogInitialDirectories { get; } = new();

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
        => throw new NotSupportedException("本测试场景不涉及文件打开对话框。");

    public FilePickResult? ShowSaveFileDialog(string filter, string? fileName = null, string? title = null)
        => throw new NotSupportedException("本测试场景不涉及文件保存对话框。");

    public FilePickResult? ShowFolderDialog(string? initialDirectory = null, string? title = null)
    {
        FolderDialogInitialDirectories.Add(initialDirectory);
        return FolderToPick;
    }
}
