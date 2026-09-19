using System;
using System.Collections.Generic;
using Microsoft.Extensions.Time.Testing;
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
    private readonly WheelGestureEngine _engine;
    private readonly MouseInputHook _stack;

    // 触发键实时读数的可变后端：改键即时生效无需重建捕获栈（配置面 live-apply 的替身）。
    private MouseButton _triggerButton = MouseButton.Button2;

    public InputStackTests()
    {
        _engine = new WheelGestureEngine(_config, _windowContext, _wheelFactory);
        _hook.EventMask = _ => EventMask.SimulatedEvent;
        // 替身默认在每次松开后再补一笔 MouseClicked（生产里 libuiohook 亦然）；本栈不订阅它，
        // 关掉后模拟事件计数只反映按下与抬起，断言不必迁就噪声。
        _hook.RaiseMouseClicked = false;
        _stack = new MouseInputHook(
            _hook,
            _engine,
            _executor,
            postToUiThread: action => action(),
            triggerButtonProvider: () => _triggerButton,
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
    public void Press_TriggerButton_TakenByWheelGesture_IsSuppressed()
    {
        Press();

        Assert.Equal(WheelGestureState.WaitingThreshold, _engine.State);
        Assert.Single(_hook.SuppressedEvents);
    }

    [Fact]
    public void Press_NonTriggerButton_PassesThrough()
    {
        Press(MouseButton.Button1);

        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.Idle, _engine.State);
    }

    [Fact]
    public void Press_BlacklistedProcess_PassesThrough()
    {
        _config.Current.BlacklistedProcesses = new List<string> { "explorer.exe" };

        Press();

        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.Idle, _engine.State);
    }

    [Fact]
    public void Paused_TriggerButton_PassesThrough()
    {
        _stack.IsPaused = true;

        Press();

        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.Idle, _engine.State);
    }

    [Fact]
    public void TriggerButton_IsParameterised_MiddleButtonTriggersInsteadOfRight()
    {
        _triggerButton = MouseButton.Button3;

        Press(MouseButton.Button2);
        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.Idle, _engine.State);

        Press(MouseButton.Button3);
        Assert.Single(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.WaitingThreshold, _engine.State);
    }

    [Fact]
    public void TriggerButton_FollowsLiveProvider_ChangeTakesEffectWithoutRestart()
    {
        // 初始右键:侧键按下不接管。
        Press(MouseButton.Button4);
        Release(MouseButton.Button4);
        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.Idle, _engine.State);

        // 配置面把触发键换成侧键 1:同一捕获栈,下一个按下事件即按新键接管。
        _triggerButton = MouseButton.Button4;

        Press(MouseButton.Button2);
        Assert.Empty(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.Idle, _engine.State);

        Press(MouseButton.Button4);
        Assert.Single(_hook.SuppressedEvents);
        Assert.Equal(WheelGestureState.WaitingThreshold, _engine.State);
    }

    [Fact]
    public void TriggerButton_ChangedMidPress_ReleaseOfOldButton_StillReplaysItsClick()
    {
        // 右键按下被接管(抑制)后改键:抬起必须仍按按下时的键配对收尾,
        // 补发的点击是当初按下的右键,而不是新配置的侧键。
        Press(MouseButton.Button2);
        Assert.Single(_hook.SuppressedEvents);

        _triggerButton = MouseButton.Button4;
        Release(MouseButton.Button2);

        // 注入面共四笔:测试的按下/抬起 + 补发的一对(全为右键)。补发回波经最近补发键记账
        // 进入回放窗口、不被抑制——点击照常还给下层应用,且不再喂给引擎。
        Assert.Equal(2, _hook.SuppressedEvents.Count);
        Assert.Equal(4, _hook.SimulatedEvents.Count);
        Assert.All(_hook.SimulatedEvents, e => Assert.Equal(MouseButton.Button2, e.Mouse.Button));
        Assert.Empty(_wheelFactory.Created);
        Assert.Equal(WheelGestureState.Idle, _engine.State);

        // 回放窗口已被回波配额耗尽:新触发键的下一个真实按下照常接管,不被窗口误吞。
        Press(MouseButton.Button4);
        Assert.Equal(3, _hook.SuppressedEvents.Count);
        Assert.Equal(WheelGestureState.WaitingThreshold, _engine.State);
    }

    [Fact]
    public void TriggerButton_Changed_NewKeyTap_ReplaysNewKeyClick()
    {
        // 换键后的轻点:补发点击语义随新键工作——补的是侧键 1,不是旧右键。
        _triggerButton = MouseButton.Button4;

        Press(MouseButton.Button4);
        Release(MouseButton.Button4);

        // 注入面共四笔:测试的按下/抬起 + 补发的一对(全为侧键 1,回波进回放窗口不被抑制)。
        Assert.Equal(2, _hook.SuppressedEvents.Count);
        Assert.Equal(4, _hook.SimulatedEvents.Count);
        Assert.All(_hook.SimulatedEvents, e => Assert.Equal(MouseButton.Button4, e.Mouse.Button));
        Assert.Empty(_wheelFactory.Created);
        Assert.Equal(WheelGestureState.Idle, _engine.State);
    }

    [Fact]
    public void TriggerButton_ChangedMidPress_SecondTriggerPress_PassesThrough()
    {
        // 旧键按下在途(未收尾)时按下新触发键:不接管(放行给下层应用),在途交互留给旧键抬起收尾。
        Press(MouseButton.Button2);
        _triggerButton = MouseButton.Button4;

        Press(MouseButton.Button4);
        Assert.Single(_hook.SuppressedEvents); // 只有旧键按下被抑制
        Assert.Equal(WheelGestureState.WaitingThreshold, _engine.State);

        Release(MouseButton.Button4);
        Assert.Single(_hook.SuppressedEvents); // 新键按下/抬起均原样放行

        // 旧键抬起照常配对收尾:补发的是旧右键的点击(回放窗口耗尽其回波对),链路无残留。
        Release(MouseButton.Button2);
        Assert.Equal(2, _hook.SuppressedEvents.Count);
        // 注入面六笔:[旧键按下, 新键按下(放行), 新键抬起(放行), 旧键抬起, 旧键回波按下, 旧键回波抬起]。
        Assert.Equal(6, _hook.SimulatedEvents.Count);
        Assert.Equal(MouseButton.Button2, _hook.SimulatedEvents[4].Mouse.Button);
        Assert.Equal(MouseButton.Button2, _hook.SimulatedEvents[5].Mouse.Button);
        Assert.Equal(WheelGestureState.Idle, _engine.State);

        // 交互收尾后新键照常接管。
        Press(MouseButton.Button4);
        Assert.Equal(3, _hook.SuppressedEvents.Count);
        Assert.Equal(WheelGestureState.WaitingThreshold, _engine.State);
    }

    [Fact]
    public void TriggerButton_Changed_NewKeyDragsWheelAndExecutes()
    {
        _config.AddProfile("Global", sectorCount: 4, actionCount: 4);

        // 先用旧键轻点一次(补发点击对消耗回放窗口),再切键用侧键 1 走完整轮盘手势。
        Press(MouseButton.Button2);
        Release(MouseButton.Button2);
        _triggerButton = MouseButton.Button4;

        Press(MouseButton.Button4);
        MoveTo(210, 100); // 越过 25px 阈值,角度 0° → 扇区 0

        var (center, profile) = Assert.Single(_wheelFactory.Created);
        Assert.Equal(new ScreenPoint(100, 100), center);
        Assert.Equal("Global", profile.ProcessName);

        Release(MouseButton.Button4, x: 210);

        ActionItem executed = Assert.Single(_executor.Executed);
        Assert.Equal("动作0", executed.Name);
        Assert.Equal(WheelGestureState.Idle, _engine.State);
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
        Assert.Equal(WheelGestureState.Idle, _engine.State);
    }

    [Fact]
    public void Click_BelowThreshold_ReplayWindowCloses_SoLaterInjectionsAreWheelGesture()
    {
        Press();
        Release();
        Assert.Equal(2, _hook.SuppressedEvents.Count);

        // 窗口只有两笔配额：后续注入（e2e 走的就是这条路径）仍按轮盘手势输入处理。
        Press();

        Assert.Equal(3, _hook.SuppressedEvents.Count);
        Assert.Equal(WheelGestureState.WaitingThreshold, _engine.State);
    }

    // --- 轮盘手势回路 -------------------------------------------------

    [Fact]
    public void Drag_BeyondThreshold_ShowsWheel_AndReleaseExecutesSelectedAction()
    {
        _config.AddProfile("Global", sectorCount: 4, actionCount: 4);

        Press();
        MoveTo(210, 100); // 越过 25px 阈值，角度 0° → 扇区 0

        var (center, profile) = Assert.Single(_wheelFactory.Created);
        Assert.Equal(new ScreenPoint(100, 100), center);
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
/// 周期探针由注入的 <see cref="FakeTimeProvider"/> 推进时钟驱动（判定入口不开公开面），
/// 不依赖真实时钟与真实钩子。
/// </summary>
public sealed class HookWatchdogTests
{
    /// <summary>探针周期：测试里每次推进假时钟的长度。</summary>
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

    [Fact]
    public void CursorMoved_WithoutAnyEvents_Recovers()
    {
        var probe = new CursorProbe(new ScreenPoint(0, 0), new ScreenPoint(10, 10));
        int recovered = 0;
        var time = new FakeTimeProvider();
        using var watchdog = NewWatchdog(probe.Read, () => recovered++, time);
        watchdog.Start();

        time.Advance(Period);

        Assert.Equal(1, recovered);
    }

    [Fact]
    public void CursorMoved_WithEvents_DoesNotRecover()
    {
        var probe = new CursorProbe(new ScreenPoint(0, 0), new ScreenPoint(10, 10));
        int recovered = 0;
        var time = new FakeTimeProvider();
        using var watchdog = NewWatchdog(probe.Read, () => recovered++, time);
        watchdog.Start();
        watchdog.CountEvent();

        time.Advance(Period);

        Assert.Equal(0, recovered);
    }

    [Fact]
    public void CursorStill_ResetsCount_SoStaleCountsCannotMaskDeath()
    {
        var probe = new CursorProbe(new ScreenPoint(0, 0), new ScreenPoint(0, 0), new ScreenPoint(5, 5));
        int recovered = 0;
        var time = new FakeTimeProvider();
        using var watchdog = NewWatchdog(probe.Read, () => recovered++, time);
        watchdog.Start();
        watchdog.CountEvent();

        time.Advance(Period); // 光标没动 → 清零计数，不判定
        Assert.Equal(0, recovered);

        time.Advance(Period); // 光标动过且期间零事件 → 判定失效
        Assert.Equal(1, recovered);
    }

    [Fact]
    public void CursorUnavailable_DoesNotJudge()
    {
        var probe = new CursorProbe(new ScreenPoint(0, 0), null);
        int recovered = 0;
        var time = new FakeTimeProvider();
        using var watchdog = NewWatchdog(probe.Read, () => recovered++, time);
        watchdog.Start();

        time.Advance(Period);

        // 探测确实发生过、只是无结论：Start 取基线一次 + 周期到点一次。
        Assert.Equal(2, probe.Calls);
        Assert.Equal(0, recovered);
    }

    [Fact]
    public void BeforeStart_DoesNotProbe()
    {
        var probe = new CursorProbe(new ScreenPoint(0, 0));
        int recovered = 0;
        var time = new FakeTimeProvider();
        using var watchdog = NewWatchdog(probe.Read, () => recovered++, time);

        time.Advance(Period); // 未 Start，无定时器可触发

        Assert.Equal(0, probe.Calls);
        Assert.Equal(0, recovered);
    }

    [Fact]
    public void AfterStop_DoesNotProbe()
    {
        var probe = new CursorProbe(new ScreenPoint(0, 0), new ScreenPoint(9, 9));
        int recovered = 0;
        var time = new FakeTimeProvider();
        using var watchdog = NewWatchdog(probe.Read, () => recovered++, time);
        watchdog.Start();
        watchdog.Stop();
        int probesAfterStop = probe.Calls;

        time.Advance(Period); // 停表后时钟再推进也不该触发

        Assert.Equal(probesAfterStop, probe.Calls);
        Assert.Equal(0, recovered);
    }

    private static HookWatchdog NewWatchdog(Func<ScreenPoint?> probe, Action recover, TimeProvider time)
        => new(Period, probe, recover, time);

    /// <summary>按序吐出预置光标位置的探针（取尽后返回 null），并记录被调用次数。</summary>
    private sealed class CursorProbe
    {
        private readonly Queue<ScreenPoint?> _positions = new();

        public CursorProbe(params ScreenPoint?[] positions)
        {
            foreach (ScreenPoint? position in positions)
            {
                _positions.Enqueue(position);
            }
        }

        public int Calls { get; private set; }

        public ScreenPoint? Read()
        {
            Calls++;
            return _positions.Count > 0 ? _positions.Dequeue() : null;
        }
    }
}
