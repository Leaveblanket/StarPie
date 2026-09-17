using System;
using System.Collections.Generic;
using SharpHook.Data;
using SharpHook.Testing;

namespace StarPie.Tests;

/// <summary>
/// 输入栈捕获侧（ADR-0052）的核心行为：抑制决策、触发键过滤、暂停放行，以及回放回路
/// （补发点击 → 注入事件回到捕获侧 → 被回放窗口吃掉）。
/// </summary>
/// <remarks>
/// 钩子与注入都用 <see cref="TestGlobalHook"/>：同一实例既是 <c>IGlobalHook</c> 又是
/// <c>IEventSimulator</c>，注入的事件会同步回到捕获侧——与生产回路同形。
/// 事件掩码统一标成模拟事件：生产里注入来源（本进程回放与 e2e 的注入）都带这个标记。
/// 触发键按住期间的移动在生产里是 MouseDragged（libuiohook 语义），替身没有该模拟入口，
/// 该订阅由 e2e 的拖拽用例覆盖。
/// </remarks>
public sealed class InputStackTests : IDisposable
{
    private readonly FakeConfigService _config = new();
    private readonly FakeWindowContext _windowContext = new();
    private readonly FakeWheelFactory _wheelFactory = new();
    private readonly TestActionExecutor _executor = new();
    private readonly TestGlobalHook _hook = new();
    private readonly GestureEngine _engine;
    private readonly MouseInputHook _stack;

    public InputStackTests()
    {
        _engine = new GestureEngine(_config, _windowContext, _wheelFactory);
        _hook.EventMask = _ => EventMask.SimulatedEvent;
        // 替身默认在每次松开后再补一笔 MouseClicked（生产里 libuiohook 亦然）；本栈不订阅它，
        // 关掉后模拟事件计数只反映按下与抬起，断言不必迁就噪声。
        _hook.RaiseMouseClicked = false;
        _stack = new MouseInputHook(
            _hook,
            _engine,
            _executor,
            postToUiThread: action => action(),
            simulatorFactory: () => _hook,
            cursorProbe: () => null,
            watchdogPeriod: TimeSpan.FromHours(1));
        _stack.Start();
    }

    public void Dispose() => _stack.Dispose();

    private void Press(MouseButton button = MouseButton.Button2, short x = 100, short y = 100)
        => _hook.SimulateMousePress(x, y, button);

    private void Release(MouseButton button = MouseButton.Button2, short x = 100, short y = 100)
        => _hook.SimulateMouseRelease(x, y, button);

    private void MoveTo(short x, short y) => _hook.SimulateMouseMovement(x, y);

    // --- 抑制决策 -------------------------------------------------

    [Fact]
    public void Press_TriggerButton_TakenByGesture_IsSuppressed()
    {
        Press();

        Assert.Equal(GestureState.WaitingThreshold, _engine.State);
        Assert.Single(_hook.SuppressedEvents);
    }

    [Fact]
    public void Press_NonTriggerButton_PassesThrough()
    {
        Press(MouseButton.Button1);

        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void Press_BlacklistedProcess_PassesThrough()
    {
        _config.Current.BlacklistedProcesses = new List<string> { "explorer.exe" };

        Press();

        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void Paused_TriggerButton_PassesThrough()
    {
        _stack.IsPaused = true;

        Press();

        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void TriggerButton_IsParameterised_MiddleButtonTriggersInsteadOfRight()
    {
        var hook = new TestGlobalHook();
        hook.EventMask = _ => EventMask.SimulatedEvent;
        var engine = new GestureEngine(_config, _windowContext, _wheelFactory);
        using var stack = new MouseInputHook(
            hook,
            engine,
            _executor,
            postToUiThread: action => action(),
            triggerButton: MouseButton.Button3,
            simulatorFactory: () => hook,
            cursorProbe: () => null,
            watchdogPeriod: TimeSpan.FromHours(1));
        stack.Start();

        hook.SimulateMousePress(MouseButton.Button2);
        Assert.Empty(hook.SuppressedEvents);

        hook.SimulateMousePress(MouseButton.Button3);
        Assert.Single(hook.SuppressedEvents);
        Assert.Equal(GestureState.WaitingThreshold, engine.State);
    }

    // --- 回放回路 -------------------------------------------------

    [Fact]
    public void Click_BelowThreshold_ReplaysTriggerClick_AndSwallowsTheReplay()
    {
        Press();

        Release();

        // 注入面收到补发的一对按下/抬起；它们回到捕获侧后被回放窗口吃掉，
        // 没有再喂给引擎——所以抑制计数仍是「按下 + 抬起」两笔，也没有弹出轮盘。
        Assert.Equal(4, _hook.SimulatedEvents.Count);
        Assert.Equal(2, _hook.SuppressedEvents.Count);
        Assert.Empty(_wheelFactory.Created);
        Assert.Equal(GestureState.Idle, _engine.State);
    }

    [Fact]
    public void Click_BelowThreshold_ReplayWindowCloses_SoLaterInjectionsAreGestures()
    {
        Press();
        Release();
        Assert.Equal(2, _hook.SuppressedEvents.Count);

        // 窗口只有两笔配额：后续注入（e2e 走的就是这条路径）仍按手势输入处理。
        Press();

        Assert.Equal(3, _hook.SuppressedEvents.Count);
        Assert.Equal(GestureState.WaitingThreshold, _engine.State);
    }

    // --- 手势回路 -------------------------------------------------

    [Fact]
    public void Drag_BeyondThreshold_ShowsWheel_AndReleaseExecutesSelectedAction()
    {
        _config.AddProfile("Global", sectorCount: 4, actionCount: 4);

        Press();
        MoveTo(210, 100); // 越过 25px 阈值，角度 0° → 扇区 0

        var (center, profile) = Assert.Single(_wheelFactory.Created);
        Assert.Equal(new GesturePoint(100, 100), center);
        Assert.Equal("Global", profile.ProcessName);

        FakeWheel wheel = Assert.Single(_wheelFactory.Wheels);
        Assert.Contains("Show", wheel.Calls);
        Assert.Contains("Highlight:0", wheel.Calls);

        Release(x: 210);

        Assert.Contains("Close", wheel.Calls);
        Assert.Equal(2, _hook.SuppressedEvents.Count);
        ActionItem executed = Assert.Single(_executor.Executed);
        Assert.Equal("动作0", executed.Name);
    }
}

/// <summary>
/// 看门狗的失效判定（ADR-0052）：光标动过却零事件 = 钩子被系统静默移除、需要就地重注册。
/// 直接驱动 <see cref="HookWatchdog.CheckOnce"/>，不依赖真实时钟与真实钩子。
/// </summary>
public sealed class HookWatchdogTests
{
    [Fact]
    public void CursorMoved_WithoutAnyEvents_Recovers()
    {
        var probe = new CursorProbe(new GesturePoint(0, 0), new GesturePoint(10, 10));
        int recovered = 0;
        using var watchdog = NewWatchdog(probe.Read, () => recovered++);
        watchdog.Start();

        watchdog.CheckOnce();

        Assert.Equal(1, recovered);
    }

    [Fact]
    public void CursorMoved_WithEvents_DoesNotRecover()
    {
        var probe = new CursorProbe(new GesturePoint(0, 0), new GesturePoint(10, 10));
        int recovered = 0;
        using var watchdog = NewWatchdog(probe.Read, () => recovered++);
        watchdog.Start();
        watchdog.CountEvent();

        watchdog.CheckOnce();

        Assert.Equal(0, recovered);
    }

    [Fact]
    public void CursorStill_ResetsCount_SoStaleCountsCannotMaskDeath()
    {
        var probe = new CursorProbe(new GesturePoint(0, 0), new GesturePoint(0, 0), new GesturePoint(5, 5));
        int recovered = 0;
        using var watchdog = NewWatchdog(probe.Read, () => recovered++);
        watchdog.Start();
        watchdog.CountEvent();

        watchdog.CheckOnce(); // 光标没动 → 清零计数，不判定
        Assert.Equal(0, recovered);

        watchdog.CheckOnce(); // 光标动过且期间零事件 → 判定失效
        Assert.Equal(1, recovered);
    }

    [Fact]
    public void CursorUnavailable_DoesNotJudge()
    {
        var probe = new CursorProbe(new GesturePoint(0, 0), null);
        int recovered = 0;
        using var watchdog = NewWatchdog(probe.Read, () => recovered++);
        watchdog.Start();

        watchdog.CheckOnce();

        Assert.Equal(0, recovered);
    }

    [Fact]
    public void BeforeStart_DoesNotProbe()
    {
        var probe = new CursorProbe(new GesturePoint(0, 0));
        int recovered = 0;
        using var watchdog = NewWatchdog(probe.Read, () => recovered++);

        watchdog.CheckOnce();

        Assert.Equal(0, probe.Calls);
        Assert.Equal(0, recovered);
    }

    [Fact]
    public void AfterStop_DoesNotProbe()
    {
        var probe = new CursorProbe(new GesturePoint(0, 0), new GesturePoint(9, 9));
        int recovered = 0;
        using var watchdog = NewWatchdog(probe.Read, () => recovered++);
        watchdog.Start();
        watchdog.Stop();
        int probesAfterStop = probe.Calls;

        watchdog.CheckOnce();

        Assert.Equal(probesAfterStop, probe.Calls);
        Assert.Equal(0, recovered);
    }

    private static HookWatchdog NewWatchdog(Func<GesturePoint?> probe, Action recover)
        => new(TimeSpan.FromHours(1), probe, recover);

    /// <summary>按序吐出预置光标位置的探针（取尽后返回 null），并记录被调用次数。</summary>
    private sealed class CursorProbe
    {
        private readonly Queue<GesturePoint?> _positions = new();

        public CursorProbe(params GesturePoint?[] positions)
        {
            foreach (GesturePoint? position in positions)
            {
                _positions.Enqueue(position);
            }
        }

        public int Calls { get; private set; }

        public GesturePoint? Read()
        {
            Calls++;
            return _positions.Count > 0 ? _positions.Dequeue() : null;
        }
    }
}
