using System;
using System.Collections.Generic;

namespace WinPieGestures.Tests;

/// <summary>
/// IDialogService 的测试替身（工程约定：mock 直接 new，不使用 mocking 框架）：
/// 按方法预设返回值并记录调用参数，供对话框编排类 ViewModel 测试断言外部行为。
/// 未预设返回值的方法返回 null；所有对话框方法都会记录调用，测试按需断言。
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
    {
        InputDialogCalls.Add((title, prompt, defaultText));
        return InputToPick;
    }

    public IconPickResult? ShowIconPicker(string? currentIconKey)
    {
        IconPickerCalls.Add(currentIconKey);
        return IconToPick;
    }

    public ColorPickResult? ShowColorPicker(string initialHex)
    {
        ColorPickerCalls.Add(initialHex);
        return ColorToPick;
    }

    public EyedropResult? ShowEyedropper()
    {
        EyedropCalls++;
        return EyedropToPick;
    }

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

    /// <summary>ShowInputDialog 的预设返回值。</summary>
    public InputDialogResult? InputToPick { get; set; }

    /// <summary>每次 ShowInputDialog 收到的 (title, prompt, defaultText) 实参。</summary>
    public List<(string Title, string Prompt, string DefaultText)> InputDialogCalls { get; } = new();

    /// <summary>ShowInputDialog 调用次数。</summary>
    public int InputCalls => InputDialogCalls.Count;

    /// <summary>最近一次 ShowInputDialog 收到的 title。</summary>
    public string? LastInputTitle => InputDialogCalls.Count == 0 ? null : InputDialogCalls[^1].Title;

    /// <summary>最近一次 ShowInputDialog 收到的 prompt。</summary>
    public string? LastInputPrompt => InputDialogCalls.Count == 0 ? null : InputDialogCalls[^1].Prompt;

    /// <summary>最近一次 ShowInputDialog 收到的 defaultText。</summary>
    public string? LastInputDefaultText => InputDialogCalls.Count == 0 ? null : InputDialogCalls[^1].DefaultText;

    /// <summary>ShowColorPicker 的预设返回值。</summary>
    public ColorPickResult? ColorToPick { get; set; }

    /// <summary>每次 ShowColorPicker 收到的初始色值。</summary>
    public List<string> ColorPickerCalls { get; } = new();

    /// <summary>ShowColorPicker 调用次数。</summary>
    public int ColorCalls => ColorPickerCalls.Count;

    /// <summary>最近一次 ShowColorPicker 收到的初始色值。</summary>
    public string? LastColorPickerInitial => ColorPickerCalls.Count == 0 ? null : ColorPickerCalls[^1];

    /// <summary>ShowEyedropper 的预设返回值。</summary>
    public EyedropResult? EyedropToPick { get; set; }

    /// <summary>ShowEyedropper 调用次数。</summary>
    public int EyedropCalls { get; private set; }

    /// <summary>ShowIconPicker 调用次数（<see cref="IconPickerCalls"/> 的便捷计数）。</summary>
    public int IconCalls => IconPickerCalls.Count;

    /// <summary>最近一次 ShowIconPicker 收到的 currentIconKey。</summary>
    public string? LastIconPickerCurrentKey => IconPickerCalls.Count == 0 ? null : IconPickerCalls[^1];

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
