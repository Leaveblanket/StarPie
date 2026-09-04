using System;
using System.Diagnostics;
using System.Linq;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// Routing coverage for the action executor (T15): action-type routing, system
/// preset mapping, launch/folder start-info construction, and hotkey chord
/// parsing. All decisions are pure functions on <see cref="ActionRouting"/> —
/// no process/key-injection side effects are touched.
/// </summary>
public sealed class ActionRoutingTests
{
    // --- Action type routing ----------------------------------------------

    [Theory]
    [InlineData("Launch", ActionRoute.Launch)]
    [InlineData("Folder", ActionRoute.Folder)]
    [InlineData("OpenFolder", ActionRoute.Folder)]
    [InlineData("Hotkey", ActionRoute.Hotkey)]
    [InlineData("System", ActionRoute.System)]
    public void ResolveRoute_KnownTypes_RouteToTheirExecutor(string type, ActionRoute expected)
    {
        Assert.Equal(expected, ActionRouting.ResolveRoute(type));
    }

    [Fact]
    public void ResolveRoute_TrimsSurroundingWhitespace()
    {
        Assert.Equal(ActionRoute.Launch, ActionRouting.ResolveRoute("  Launch  "));
        Assert.Equal(ActionRoute.System, ActionRouting.ResolveRoute("\tSystem\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("launch")]      // routing is case-sensitive, as the original switch
    [InlineData("SYSTEM")]
    [InlineData("SomethingElse")]
    public void ResolveRoute_UnknownType_RoutesToUnknown(string type)
    {
        Assert.Equal(ActionRoute.Unknown, ActionRouting.ResolveRoute(type));
    }

    // --- System preset mapping ---------------------------------------------

    [Fact]
    public void ResolveSystemCommand_PresetsCaseInsensitiveAndTrimmed()
    {
        Assert.Equal(ActionRouting.SystemCommand.LockWorkstation.Instance,
            ActionRouting.ResolveSystemCommand("  LOCK  "));
        Assert.Equal(ActionRouting.SystemCommand.SendKey.VolumeUp,
            ActionRouting.ResolveSystemCommand("VolumeUp"));
    }

    [Fact]
    public void ResolveSystemCommand_EmptyPreset_IsSilentNoop()
    {
        var command = Assert.IsType<ActionRouting.SystemCommand.Noop>(ActionRouting.ResolveSystemCommand(""));
        Assert.Null(command.UnknownPreset);
        var nullCommand = Assert.IsType<ActionRouting.SystemCommand.Noop>(ActionRouting.ResolveSystemCommand(null));
        Assert.Null(nullCommand.UnknownPreset);
    }

    [Fact]
    public void ResolveSystemCommand_UnknownPreset_IsNoopCarryingTheRawName()
    {
        var command = Assert.IsType<ActionRouting.SystemCommand.Noop>(ActionRouting.ResolveSystemCommand("NoSuchPreset"));
        Assert.Equal("NoSuchPreset", command.UnknownPreset);
    }

    [Fact]
    public void ResolveSystemCommand_WindowManagementPresets_MapToHotkeys()
    {
        AssertHotkey("closewindow", "Alt+F4");
        AssertHotkey("minimize", "Win+Down");
        AssertHotkey("maximize", "Win+Up");
        AssertHotkey("snapleft", "Win+Left");
        AssertHotkey("snapright", "Win+Right");
        AssertHotkey("taskview", "Win+Tab");
        AssertHotkey("prevdesktop", "Win+Ctrl+Left");
        AssertHotkey("nextdesktop", "Win+Ctrl+Right");
        AssertHotkey("showdesktop", "Win+D");
        AssertHotkey("fullscreen", "F11");
        AssertHotkey("screenshot", "Win+Shift+S");
        AssertHotkey("rundialog", "Win+R");
        AssertHotkey("windowssearch", "Win+S");
        AssertHotkey("clipboardhistory", "Win+V");
        AssertHotkey("newtab", "Ctrl+T");
        AssertHotkey("closetab", "Ctrl+W");
        AssertHotkey("reopentab", "Ctrl+Shift+T");
        AssertHotkey("refresh", "F5");
        AssertHotkey("hardrefresh", "Ctrl+F5");
        AssertHotkey("zoomin", "Ctrl+Plus");
        AssertHotkey("zoomout", "Ctrl+Minus");
        AssertHotkey("zoomreset", "Ctrl+0");
    }

    [Fact]
    public void ResolveSystemCommand_ProcessPresets_MapToProcessStartWithFallbacks()
    {
        Assert.Equal(new ActionRouting.SystemCommand.StartProcess("taskmgr.exe", "", "Ctrl+Shift+Esc", Silent: false),
            ActionRouting.ResolveSystemCommand("taskmanager"));
        Assert.Equal(new ActionRouting.SystemCommand.StartProcess("explorer.exe", "", "Win+E", Silent: false),
            ActionRouting.ResolveSystemCommand("explorer"));
        Assert.Equal(new ActionRouting.SystemCommand.StartProcess("ms-settings:", "", "Win+I", Silent: false),
            ActionRouting.ResolveSystemCommand("settings"));
        Assert.Equal(new ActionRouting.SystemCommand.StartProcess("calc.exe", "", "Win+R", Silent: false),
            ActionRouting.ResolveSystemCommand("calculator"));
    }

    [Fact]
    public void ResolveSystemCommand_PowerPresets_MapToProcessStartWithSilentFallback()
    {
        Assert.Equal(new ActionRouting.SystemCommand.StartProcess("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0", null, Silent: true),
            ActionRouting.ResolveSystemCommand("sleep"));
        Assert.Equal(new ActionRouting.SystemCommand.StartProcess("shutdown.exe", "/r /t 0", null, Silent: true),
            ActionRouting.ResolveSystemCommand("restart"));
        Assert.Equal(new ActionRouting.SystemCommand.StartProcess("shutdown.exe", "/s /t 0", null, Silent: true),
            ActionRouting.ResolveSystemCommand("shutdown"));
    }

    [Fact]
    public void ResolveSystemCommand_MediaAndVolumePresets_MapToSingleKeys()
    {
        Assert.Equal(ActionRouting.SystemCommand.SendKey.VolumeUp, ActionRouting.ResolveSystemCommand("volumeup"));
        Assert.Equal(ActionRouting.SystemCommand.SendKey.VolumeDown, ActionRouting.ResolveSystemCommand("volumedown"));
        Assert.Equal(ActionRouting.SystemCommand.SendKey.VolumeMute, ActionRouting.ResolveSystemCommand("volumemute"));
        AssertKey("playpause", 0xB3);
        AssertKey("nexttrack", 0xB0);
        AssertKey("prevtrack", 0xB1);
        AssertKey("stopmedia", 0xB2);
    }

    [Fact]
    public void ResolveSystemCommand_LockPreset_LocksWorkstation()
    {
        Assert.Equal(ActionRouting.SystemCommand.LockWorkstation.Instance,
            ActionRouting.ResolveSystemCommand("lock"));
    }

    private static void AssertHotkey(string preset, string expectedHotkey)
        => Assert.Equal(new ActionRouting.SystemCommand.SendHotkey(expectedHotkey),
            ActionRouting.ResolveSystemCommand(preset));

    private static void AssertKey(string preset, ushort expectedVk)
        => Assert.Equal(new ActionRouting.SystemCommand.SendKey(expectedVk),
            ActionRouting.ResolveSystemCommand(preset));

    // --- Launch / Folder start-info construction ---------------------------

    [Fact]
    public void BuildLaunchStartInfo_PassesPathAndArgumentsThrough()
    {
        var startInfo = ActionRouting.BuildLaunchStartInfo(@"C:\Tools\app.exe", "--flag value");

        Assert.Equal(@"C:\Tools\app.exe", startInfo.FileName);
        Assert.Equal("--flag value", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildLaunchStartInfo_NullArguments_BecomeEmptyString()
    {
        var startInfo = ActionRouting.BuildLaunchStartInfo("app.exe", null);

        Assert.Equal(string.Empty, startInfo.Arguments);
    }

    [Fact]
    public void BuildLaunchStartInfo_DoesNotSetWorkingDirectory()
    {
        // Working-directory semantics of the original executor: WorkingDirectory
        // stays unset, so the launched process inherits the caller's.
        var startInfo = ActionRouting.BuildLaunchStartInfo("app.exe", "");

        Assert.True(string.IsNullOrEmpty(startInfo.WorkingDirectory));
    }

    [Fact]
    public void BuildFolderStartInfo_ExistingDirectory_OpensFolderInExplorer()
    {
        var startInfo = ActionRouting.BuildFolderStartInfo(@"C:\Tools", isDirectory: true, isFile: false);

        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.Equal("\"C:\\Tools\"", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildFolderStartInfo_ExistingFile_SelectsItInExplorer()
    {
        var startInfo = ActionRouting.BuildFolderStartInfo(@"C:\Tools\app.exe", isDirectory: false, isFile: true);

        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.Equal("/select,\"C:\\Tools\\app.exe\"", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildFolderStartInfo_MissingPath_ShellExecutesItDirectly()
    {
        var startInfo = ActionRouting.BuildFolderStartInfo(@"C:\missing.lnk", isDirectory: false, isFile: false);

        Assert.Equal(@"C:\missing.lnk", startInfo.FileName);
        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    // --- Hotkey chord parsing and key-stroke sequences ---------------------

    [Fact]
    public void BuildKeySequence_SimpleChord_ModifierDownKeyDownKeyUpModifierUp()
    {
        var strokes = ActionRouting.BuildKeySequence("Ctrl+C");

        var expected = new[]
        {
            new KeyStroke(0xA2, down: true),   // VK_LCONTROL
            new KeyStroke(0x43, down: true),   // C
            new KeyStroke(0x43, down: false),
            new KeyStroke(0xA2, down: false),
        };
        Assert.Equal(expected, strokes);
    }

    [Fact]
    public void BuildKeySequence_ModifierOrder_FollowsChordOrderAndReleasesInReverse()
    {
        var strokes = ActionRouting.BuildKeySequence("Ctrl+Shift+S").ToList();

        Assert.Equal(new ushort[] { 0xA2, 0xA0, 0x53, 0x53, 0xA0, 0xA2 },
            strokes.Select(s => s.VirtualKey));
        Assert.Equal(new[] { true, true, true, false, false, false },
            strokes.Select(s => s.KeyDown));
    }

    [Fact]
    public void BuildKeySequence_DuplicateModifiers_AreSentOnce()
    {
        var strokes = ActionRouting.BuildKeySequence("Ctrl+Ctrl+C");

        Assert.Equal(4, strokes.Count);
        Assert.Equal(1, strokes.Count(s => s.VirtualKey == 0xA2 && s.KeyDown));
    }

    [Fact]
    public void BuildKeySequence_NamedAliases_AcceptCaseInsensitiveSpellings()
    {
        // "control" → Ctrl, uppercase letters map like their lowercase forms.
        Assert.Equal(ActionRouting.BuildKeySequence("Ctrl+C"), ActionRouting.BuildKeySequence("Control+C"));
    }

    [Fact]
    public void BuildKeySequence_ArrowAndMediaKeys_CarryTheExtendedKeyFlag()
    {
        // Win (0x5B) 落在 0x5B-0x5C 段、Left (0x25) 落在 0x21-0x2F 段——迁移前
        // CreateKeyInput 的扩展键规则下两者都带 EXTENDEDKEY 标志。
        var strokes = ActionRouting.BuildKeySequence("Win+Left");
        Assert.All(strokes, s => Assert.True(s.Extended));

        var muteDown = ActionRouting.BuildKeySequence("VolumeMute").First();
        Assert.Equal(0xAD, muteDown.VirtualKey);
        Assert.True(muteDown.Extended);
    }

    [Fact]
    public void BuildKeySequence_ModifierAliases_MapToTheSameLeftHandKeys()
    {
        // 迁移前 ParseHotkey 接受这些别名（用户配置可能存在此类写法），映射到同一左手指。
        Assert.Equal(
            ActionRouting.BuildKeySequence("Ctrl+Shift+Alt+Win+A"),
            ActionRouting.BuildKeySequence("control+lshift+ralt+windows+a"));
    }

    [Fact]
    public void BuildKeySequence_FunctionKeys_MapToTheirVirtualKeys()
    {
        var strokes = ActionRouting.BuildKeySequence("F11");

        // 单主键弦 = 按下 + 抬起两条。
        Assert.Equal(2, strokes.Count);
        Assert.Equal(new ushort[] { 0x7A, 0x7A }, strokes.Select(s => s.VirtualKey).ToArray());
        Assert.Equal(new[] { true, false }, strokes.Select(s => s.KeyDown).ToArray());
    }

    [Fact]
    public void BuildKeySequence_UnparseableChord_ProducesEmptySequence()
    {
        Assert.Empty(ActionRouting.BuildKeySequence("NotAKey"));
        Assert.Empty(ActionRouting.BuildKeySequence("+"));
    }

    [Fact]
    public void BuildKeySequence_ModifierOnlyChord_PressesAndReleasesIt()
    {
        var strokes = ActionRouting.BuildKeySequence("Ctrl").ToList();

        Assert.Equal(2, strokes.Count);
        Assert.True(strokes[0].KeyDown);
        Assert.False(strokes[1].KeyDown);
    }

    // --- Single media key ---------------------------------------------------

    [Fact]
    public void BuildSingleKeyStrokes_PressAndReleaseWithExtendedFlag()
    {
        var strokes = ActionRouting.BuildSingleKeyStrokes(0xAF).ToList(); // VK_VOLUME_UP

        Assert.Equal(2, strokes.Count);
        Assert.True(strokes[0].KeyDown);
        Assert.False(strokes[1].KeyDown);
        Assert.All(strokes, s => Assert.True(s.Extended));
    }

    // --- Debug guard ---------------------------------------------------------

    [Fact]
    public void ActionRoute_HasNoValueBeyondTheKnownRoutes()
    {
        // Guards against accidentally widening the enum without covering the new route.
        Assert.Equal(5, Enum.GetValues<ActionRoute>().Length);
    }
}
