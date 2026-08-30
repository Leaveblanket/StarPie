using System;
using System.Collections.Generic;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// State-transition coverage for the gesture engine (T04): threshold trigger,
/// direction select, outer escape, center-deadzone cancel, foreground-profile
/// match, full-screen isolation, modifier-key isolation, blacklist isolation,
/// and release outcomes (execute / replay click / pass through).
/// </summary>
public sealed class GestureEngineTests
{
    private readonly FakeConfigService _config = new();
    private readonly FakeWindowContext _windowContext = new();
    private readonly FakeWheelFactory _wheelFactory = new();
    private readonly GestureEngine _engine;

    public GestureEngineTests()
    {
        _engine = new GestureEngine(_config, _windowContext, _wheelFactory);
    }

    private static GesturePoint P(double x, double y) => new(x, y);

    private WheelProfile AddProfile(string processName, int sectorCount, int actionCount)
        => _config.AddProfile(processName, sectorCount, actionCount);

    // --- Threshold trigger -------------------------------------------------

    [Fact]
    public void Move_BelowThreshold_DoesNotActivateWheel()
    {
        Assert.True(_engine.OnTriggerDown(P(100, 100)));
        Assert.Equal(GestureState.WaitingThreshold, _engine.State);

        _engine.OnTriggerMove(P(124, 100)); // 24 < 25 default threshold

        Assert.Empty(_wheelFactory.Created);
        Assert.Equal(GestureState.WaitingThreshold, _engine.State);
    }

    [Fact]
    public void Move_AtThreshold_ActivatesWheelAtStartPoint()
    {
        var profile = AddProfile("Global", sectorCount: 8, actionCount: 8);

        Assert.True(_engine.OnTriggerDown(P(100, 100)));
        _engine.OnTriggerMove(P(125, 100)); // exactly the 25px threshold

        var (center, createdProfile) = Assert.Single(_wheelFactory.Created);
        Assert.Equal(100, center.X);
        Assert.Equal(100, center.Y);
        Assert.Same(profile, createdProfile);
        var wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Equal(new[] { "Show", "Escape:False", "Highlight:0" }, wheel.Calls);
        Assert.Equal(GestureState.Active, _engine.State);
    }

    [Fact]
    public void Move_ThresholdReadLive_MidGestureThresholdChangeApplies()
    {
        AddProfile("Global", sectorCount: 8, actionCount: 8);
        _engine.OnTriggerDown(P(100, 100));

        _config.Current.DragThreshold = 10.0;
        _engine.OnTriggerMove(P(115, 100)); // below old 25, above new 10

        Assert.Single(_wheelFactory.Created);
    }

    // --- Direction selection ----------------------------------------------

    [Fact]
    public void Move_Active_SelectsSectorByAngle()
    {
        AddProfile("Global", sectorCount: 8, actionCount: 8);
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100)); // activate, sector 0

        // 60px moves at screen-clockwise angles (Y grows downward).
        _engine.OnTriggerMove(P(160.0, 100.0)); // 0°   -> 0 (already selected)
        _engine.OnTriggerMove(P(100.0, 160.0)); // 90°  -> 2
        _engine.OnTriggerMove(P(40.0, 100.0));  // 180° -> 4
        _engine.OnTriggerMove(P(100.0, 40.0));  // 270° -> 6
        _engine.OnTriggerMove(P(142.43, 142.43)); // 45°  -> 1
        _engine.OnTriggerMove(P(159.1, 89.6));    // 350° -> round(7.78) % 8 = 0

        var wheel = Assert.Single(_wheelFactory.Wheels);
        // Every in-range move re-asserts the escape state before highlighting.
        Assert.Equal(
            new[]
            {
                "Show", "Escape:False", "Highlight:0",
                "Escape:False", "Highlight:0",
                "Escape:False", "Highlight:2",
                "Escape:False", "Highlight:4",
                "Escape:False", "Highlight:6",
                "Escape:False", "Highlight:1",
                "Escape:False", "Highlight:0", // 350° wraps to sector 0
            },
            wheel.Calls);
    }

    // --- Center deadzone cancel --------------------------------------------

    [Fact]
    public void Move_BackIntoCenterDeadzone_ClearsSelection_AndReleaseCancels()
    {
        var profile = AddProfile("Global", sectorCount: 8, actionCount: 8);
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100)); // activate, sector 0

        _engine.OnTriggerMove(P(112, 100)); // 12 < 25 * 0.6 deadzone

        var wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Contains("Highlight:-1", wheel.Calls);
        Assert.Contains("Escape:False", wheel.Calls);

        var result = _engine.OnTriggerUp(P(112, 100));

        Assert.True(result.Handled);
        Assert.False(result.ShouldReplayClick);
        Assert.Null(result.ActionToExecute);
        Assert.Equal(new[] { "Show", "Escape:False", "Highlight:0", "Highlight:-1", "Escape:False", "Close" }, wheel.Calls);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    // --- Outer escape -------------------------------------------------------

    [Fact]
    public void Move_BeyondEscapeDistance_ShowsEscapeState_AndReleaseCancels()
    {
        AddProfile("Global", sectorCount: 8, actionCount: 8); // OuterEscapeDistance defaults to 186
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100)); // activate

        _engine.OnTriggerMove(P(290, 100)); // 190 > 186

        var wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Equal(
            new[] { "Show", "Escape:False", "Highlight:0", "Highlight:-1", "Escape:True" },
            wheel.Calls);

        var result = _engine.OnTriggerUp(P(290, 100));
        Assert.True(result.Handled);
        Assert.Null(result.ActionToExecute);
        Assert.Equal("Close", wheel.Calls[^1]);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void Move_EscapeDisabled_KeepsSectorSelectionAndExecutesOnRelease()
    {
        _config.Current.EnableOuterEscapeCancel = false;
        var profile = AddProfile("Global", sectorCount: 8, actionCount: 8);
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100)); // activate

        _engine.OnTriggerMove(P(350, 100)); // 250 > 186, but escape disabled

        var wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Contains("Highlight:0", wheel.Calls);
        Assert.DoesNotContain("Escape:True", wheel.Calls);

        var result = _engine.OnTriggerUp(P(350, 100));
        Assert.Same(profile.Actions[0], result.ActionToExecute);
    }

    [Fact]
    public void Move_EscapeDistanceFallsBackToWheelRadiusTimes15_WhenUnset()
    {
        _config.Current.OuterEscapeDistance = 0; // fall back to WheelRadius (138) * 1.5 = 207
        AddProfile("Global", sectorCount: 8, actionCount: 8);
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100)); // activate

        _engine.OnTriggerMove(P(295, 100)); // 195 < 207: still a normal selection
        _engine.OnTriggerMove(P(310, 100)); // 210 > 207: escape

        var wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Contains("Highlight:0", wheel.Calls);
        Assert.Contains("Escape:True", wheel.Calls);
    }

    // --- Foreground process profile match -----------------------------------

    [Fact]
    public void Activate_UsesProfileOfForegroundProcess()
    {
        var chrome = AddProfile("chrome.exe", sectorCount: 4, actionCount: 4);
        AddProfile("Global", sectorCount: 8, actionCount: 8);
        _windowContext.ProcessName = "chrome.exe";

        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(150, 100)); // 50 > 25
        _engine.OnTriggerMove(P(100, 150)); // 90° with sector size 90 -> sector 1

        var (_, createdProfile) = Assert.Single(_wheelFactory.Created);
        Assert.Same(chrome, createdProfile);
        var wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Equal(new[] { "Show", "Escape:False", "Highlight:0", "Escape:False", "Highlight:1" }, wheel.Calls);
    }

    [Fact]
    public void Activate_UnknownProcess_FallsBackToGlobalProfile()
    {
        var global = _config.GetGlobalProfile();
        _windowContext.ProcessName = "notepad.exe";

        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(150, 100));

        var (_, createdProfile) = Assert.Single(_wheelFactory.Created);
        Assert.Same(global, createdProfile);
    }

    // --- Isolation: blacklist ------------------------------------------------

    [Fact]
    public void TriggerDown_BlacklistedProcess_PassesThrough()
    {
        AddProfile("Global", sectorCount: 8, actionCount: 8);
        _config.Current.BlacklistedProcesses = new List<string> { "mstsc.exe", "paint.exe" };
        _windowContext.ProcessName = "MSTSC.EXE"; // case-insensitive match

        Assert.False(_engine.OnTriggerDown(P(100, 100)));
        Assert.Equal(GestureState.Idle, _engine.State);
        Assert.Empty(_wheelFactory.Created);
    }

    [Fact]
    public void TriggerDown_BlacklistNull_DoesNotIsolate()
    {
        // Legacy config.json can deserialize this list as null; the engine guards for it.
        _config.Current.BlacklistedProcesses = null!;
        _windowContext.ProcessName = "anything.exe";

        Assert.True(_engine.OnTriggerDown(P(100, 100)));
        Assert.Equal(GestureState.WaitingThreshold, _engine.State);
    }

    // --- Isolation: full screen ----------------------------------------------

    [Fact]
    public void TriggerDown_FullScreenEnabledAndFullscreen_PassesThrough()
    {
        _config.Current.DisableOnFullScreen = true;
        _windowContext.FullScreen = true;

        Assert.False(_engine.OnTriggerDown(P(100, 100)));
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void TriggerDown_FullScreenDisabled_WaitsForThreshold()
    {
        _config.Current.DisableOnFullScreen = false;
        _windowContext.FullScreen = true;

        Assert.True(_engine.OnTriggerDown(P(100, 100)));
        Assert.Equal(GestureState.WaitingThreshold, _engine.State);
    }

    // --- Isolation: modifier keys ---------------------------------------------

    [Fact]
    public void TriggerDown_DisableOnCtrl_AndCtrlHeld_PassesThrough()
    {
        _config.Current.DisableOnCtrl = true;
        _windowContext.Modifiers = GestureModifierKeys.Control;

        Assert.False(_engine.OnTriggerDown(P(100, 100)));
    }

    [Fact]
    public void TriggerDown_DisableOnShift_AndShiftHeld_PassesThrough()
    {
        _config.Current.DisableOnShift = true;
        _windowContext.Modifiers = GestureModifierKeys.Shift;

        Assert.False(_engine.OnTriggerDown(P(100, 100)));
    }

    [Fact]
    public void TriggerDown_DisableOnAlt_AndAltHeld_PassesThrough()
    {
        _config.Current.DisableOnAlt = true;
        _windowContext.Modifiers = GestureModifierKeys.Alt;

        Assert.False(_engine.OnTriggerDown(P(100, 100)));
    }

    [Fact]
    public void TriggerDown_ModifierFlagOff_WaitsForThreshold()
    {
        _config.Current.DisableOnCtrl = false;
        _windowContext.Modifiers = GestureModifierKeys.Control;

        Assert.True(_engine.OnTriggerDown(P(100, 100)));
    }

    // --- Release outcomes ------------------------------------------------------

    [Fact]
    public void Release_BeforeThreshold_ReplaysClick()
    {
        AddProfile("Global", sectorCount: 8, actionCount: 8);
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(110, 100));

        var result = _engine.OnTriggerUp(P(110, 100));

        Assert.True(result.Handled);
        Assert.True(result.ShouldReplayClick);
        Assert.Null(result.ActionToExecute);
        Assert.Empty(_wheelFactory.Created);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void Release_AfterSelection_ExecutesSelectedAction()
    {
        var profile = AddProfile("Global", sectorCount: 8, actionCount: 8);
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100)); // activate
        _engine.OnTriggerMove(P(100, 160)); // 90° -> sector 2

        var result = _engine.OnTriggerUp(P(100, 160));

        Assert.True(result.Handled);
        Assert.False(result.ShouldReplayClick);
        Assert.Same(profile.Actions[2], result.ActionToExecute);
        var wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Equal("Close", wheel.Calls[^1]);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void Release_SelectedActionWithEmptyType_Cancels()
    {
        var profile = AddProfile("Global", sectorCount: 8, actionCount: 8);
        profile.Actions[2] = new ActionItem { Type = "", Name = "空动作" };
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100));
        _engine.OnTriggerMove(P(100, 160)); // sector 2

        var result = _engine.OnTriggerUp(P(100, 160));

        Assert.True(result.Handled);
        Assert.Null(result.ActionToExecute);
    }

    [Fact]
    public void Release_SectorWithoutAction_Cancels()
    {
        AddProfile("Global", sectorCount: 12, actionCount: 3); // sectors 3..11 have no action
        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(125, 100));
        _engine.OnTriggerMove(P(100, 150)); // 90° with sector size 30 -> sector 3 (no action)

        var result = _engine.OnTriggerUp(P(160, 100));

        Assert.True(result.Handled);
        Assert.Null(result.ActionToExecute);
    }

    [Fact]
    public void Release_WhileIdle_PassesThrough()
    {
        var result = _engine.OnTriggerUp(P(100, 100));

        Assert.False(result.Handled);
        Assert.False(result.ShouldReplayClick);
        Assert.Null(result.ActionToExecute);
    }

    // --- Wheel lifecycle ---------------------------------------------------------

    [Fact]
    public void SecondGesture_CreatesFreshWheel_AndClosesPrevious()
    {
        AddProfile("Global", sectorCount: 8, actionCount: 8);

        _engine.OnTriggerDown(P(100, 100));
        _engine.OnTriggerMove(P(150, 100));
        _engine.OnTriggerUp(P(150, 100));

        _engine.OnTriggerDown(P(300, 300));
        _engine.OnTriggerMove(P(350, 300));
        _engine.OnTriggerUp(P(350, 300));

        Assert.Equal(2, _wheelFactory.Wheels.Count);
        Assert.NotSame(_wheelFactory.Wheels[0], _wheelFactory.Wheels[1]);
        Assert.Equal(1, _wheelFactory.Wheels[0].Calls.Count(c => c == "Close"));
        Assert.Equal(1, _wheelFactory.Wheels[1].Calls.Count(c => c == "Close"));
    }

    [Fact]
    public void Move_WhileIdle_IsIgnored()
    {
        _engine.OnTriggerMove(P(500, 500));

        Assert.Empty(_wheelFactory.Created);
        Assert.Equal(GestureState.Idle, _engine.State);
    }
}

internal sealed class FakeConfigService : IConfigService
{
    private readonly Dictionary<string, WheelProfile> _profilesByProcess = new(StringComparer.OrdinalIgnoreCase);
    private WheelProfile? _global;

    public AppConfig Current { get; set; } = new();

    /// <summary>Registers the canned profile the lookup returns for a process name.</summary>
    public WheelProfile AddProfile(string processName, int sectorCount, int actionCount)
    {
        var profile = new WheelProfile
        {
            ProcessName = processName,
            SectorCount = sectorCount,
            Actions = new List<ActionItem>(),
        };
        for (int i = 0; i < actionCount; i++)
        {
            profile.Actions.Add(new ActionItem { Type = "Hotkey", Name = $"动作{i}", Parameter = "Ctrl+C" });
        }
        _profilesByProcess[processName] = profile;
        if (processName == "Global")
        {
            _global = profile;
        }
        return profile;
    }

    public void Load() { }

    public void Save() { }

    public WheelProfile GetProfileForProcess(string processName)
    {
        if (!string.IsNullOrEmpty(processName) && _profilesByProcess.TryGetValue(processName, out var profile))
        {
            return profile;
        }
        return GetGlobalProfile();
    }

    public WheelProfile GetGlobalProfile() => _global ??= AddProfile("Global", sectorCount: 8, actionCount: 0);
}

internal sealed class FakeWindowContext : IWindowContext
{
    public string ProcessName { get; set; } = "explorer.exe";
    public bool FullScreen { get; set; }
    public GestureModifierKeys Modifiers { get; set; } = GestureModifierKeys.None;

    public string GetForegroundProcessName() => ProcessName;

    public bool IsForegroundFullScreen() => FullScreen;

    public GestureModifierKeys GetActiveModifierKeys() => Modifiers;
}

internal sealed class FakeWheelFactory : IWheelFactory
{
    public List<(GesturePoint Center, WheelProfile Profile)> Created { get; } = new();
    public List<FakeWheel> Wheels { get; } = new();

    public IWheelViewModel Create(GesturePoint center, WheelProfile profile)
    {
        Created.Add((center, profile));
        var wheel = new FakeWheel();
        Wheels.Add(wheel);
        return wheel;
    }
}

internal sealed class FakeWheel : IWheelViewModel
{
    /// <summary>Ordered interaction log: "Show", "Highlight:{i}", "Escape:{bool}", "Close".</summary>
    public List<string> Calls { get; } = new();

    public void Show() => Calls.Add("Show");

    public void HighlightSector(int sectorIndex) => Calls.Add($"Highlight:{sectorIndex}");

    public void SetOuterEscapeState(bool isEscaped) => Calls.Add($"Escape:{isEscaped}");

    public void Close() => Calls.Add("Close");
}
