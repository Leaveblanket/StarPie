using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 动作执行服务的外部行为 (T15)：路由决策驱动的系统调用经注入假体捕获——启动的
/// ProcessStartInfo、发送的键序、锁屏与错误通知。真 Win32 调用（SendInput/
/// LockWorkStation/MessageBox）不在测试范围，由 Python e2e 与手动冒烟兜底。
/// </summary>
public sealed class ActionExecutorServiceTests
{
    private class FakeSeams
    {
        public List<ProcessStartInfo> Started { get; } = new();
        public List<IReadOnlyList<KeyStroke>> SentKeyStrokes { get; } = new();
        public int LockCalls;
        public List<string> ActionErrors { get; } = new();
        public List<string> FolderErrors { get; } = new();
        public bool FailNextStart;
        public Func<string, bool> DirectoryExists { get; set; } = _ => false;
        public Func<string, bool> FileExists { get; set; } = _ => false;

        public ActionExecutorService BuildService()
        {
            return new ActionExecutorService(
                startProcess: startInfo =>
                {
                    if (FailNextStart) { FailNextStart = false; throw new System.ComponentModel.Win32Exception(2); }
                    Started.Add(startInfo);
                },
                directoryExists: DirectoryExists,
                fileExists: FileExists,
                lockWorkStation: () => LockCalls++,
                sendKeyStrokes: strokes => SentKeyStrokes.Add(strokes),
                showActionError: message => ActionErrors.Add(message),
                showFolderError: message => FolderErrors.Add(message));
        }
    }

    private static ActionItem Action(string type, string parameter, string? arguments = null)
        => new() { Type = type, Parameter = parameter, Name = "测试动作", Arguments = arguments ?? "" };

    // --- Launch ---------------------------------------------------------------

    [Fact]
    public void Execute_Launch_StartsShellExecuteWithArguments()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("Launch", @"C:\Tools\app.exe", "--flag value"));

        var startInfo = Assert.Single(seams.Started);
        Assert.Equal(@"C:\Tools\app.exe", startInfo.FileName);
        Assert.Equal("--flag value", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void Execute_LaunchEmptyPath_SilentNoStart()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("Launch", ""));

        Assert.Empty(seams.Started);
        Assert.Empty(seams.ActionErrors);
    }

    // --- Hotkey ---------------------------------------------------------------

    [Fact]
    public void Execute_Hotkey_SendsCompiledStrokeSequence()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("Hotkey", "Ctrl+C"));

        var strokes = Assert.Single(seams.SentKeyStrokes);
        Assert.Equal(
            new[] { (0xA2, true), (0x43, true), (0x43, false), (0xA2, false) },
            strokes.Select(s => ((int)s.VirtualKey, s.KeyDown)).ToArray());
    }

    // --- System presets ---------------------------------------------------------

    [Fact]
    public void Execute_SystemShowDesktop_SendsWinD()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("System", "ShowDesktop"));

        var strokes = Assert.Single(seams.SentKeyStrokes);
        Assert.Equal(new ushort[] { 0x5B, 0x44, 0x44, 0x5B }, strokes.Select(s => s.VirtualKey).ToArray());
        Assert.Empty(seams.Started);
    }

    [Fact]
    public void Execute_SystemTaskManager_TriesProcessThenFallsBackToHotkey()
    {
        var seams = new FakeSeams();
        seams.FailNextStart = true;
        var service = seams.BuildService();

        service.Execute(Action("System", "taskmanager"));

        var strokes = Assert.Single(seams.SentKeyStrokes); // Ctrl+Shift+Esc 降级
        Assert.Equal(new ushort[] { 0xA2, 0xA0, 0x1B, 0x1B, 0xA0, 0xA2 }, strokes.Select(s => s.VirtualKey).ToArray()); // Ctrl+Shift+Esc
        Assert.Empty(seams.ActionErrors); // 非静默类降级不报错
    }

    [Fact]
    public void Execute_SystemSleep_StartFailureIsSilent()
    {
        var seams = new FakeSeams();
        seams.FailNextStart = true;
        var service = seams.BuildService();

        service.Execute(Action("System", "sleep"));

        Assert.Empty(seams.SentKeyStrokes);
        Assert.Empty(seams.ActionErrors);
    }

    [Fact]
    public void Execute_SystemLock_CallsLockWorkStationOnly()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("System", "lock"));

        Assert.Equal(1, seams.LockCalls);
        Assert.Empty(seams.Started);
        Assert.Empty(seams.SentKeyStrokes);
    }

    [Fact]
    public void Execute_SystemVolumeUp_SendsSingleExtendedKey()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("System", "volumeup"));

        var strokes = Assert.Single(seams.SentKeyStrokes);
        Assert.Equal(2, strokes.Count);
        Assert.All(strokes, s => Assert.True(s.Extended));
    }

    [Fact]
    public void Execute_SystemUnknownPreset_SilentNoop()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("System", "NoSuchPreset"));

        Assert.Empty(seams.Started);
        Assert.Empty(seams.SentKeyStrokes);
        Assert.Equal(0, seams.LockCalls);
    }

    // --- Folder -----------------------------------------------------------------

    [Fact]
    public void Execute_Folder_ExistingDirectory_OpensInExplorer()
    {
        var seams = new FakeSeams { DirectoryExists = _ => true };
        var service = seams.BuildService();

        service.Execute(Action("Folder", @"C:\Tools"));

        var startInfo = Assert.Single(seams.Started);
        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.Equal("\"C:\\Tools\"", startInfo.Arguments);
    }

    [Fact]
    public void Execute_Folder_ExistingFile_SelectsInExplorer()
    {
        var seams = new FakeSeams { FileExists = p => p.EndsWith(".lnk") };
        var service = seams.BuildService();

        service.Execute(Action("OpenFolder", @"C:\Tools\app.lnk"));

        var startInfo = Assert.Single(seams.Started);
        Assert.Equal("/select,\"C:\\Tools\\app.lnk\"", startInfo.Arguments);
    }

    [Fact]
    public void Execute_Folder_StartFailure_ShowsFolderErrorWithOriginalPath()
    {
        var seams = new FakeSeams();
        seams.FailNextStart = true;
        var service = seams.BuildService();

        service.Execute(Action("Folder", @"C:\Tools"));

        Assert.Single(seams.FolderErrors);
        Assert.Contains(@"C:\Tools", seams.FolderErrors[0]);
    }

    // --- 未知类型与空动作 -----------------------------------------------------------

    [Fact]
    public void Execute_UnknownType_SilentNoop()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(Action("SomethingElse", "x"));

        Assert.Empty(seams.Started);
        Assert.Empty(seams.SentKeyStrokes);
        Assert.Empty(seams.ActionErrors);
    }

    [Fact]
    public void Execute_NullAction_SilentNoop()
    {
        var seams = new FakeSeams();
        var service = seams.BuildService();

        service.Execute(null!);

        Assert.Empty(seams.Started);
        Assert.Empty(seams.SentKeyStrokes);
    }
}
