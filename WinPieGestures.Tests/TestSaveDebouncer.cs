using System;

namespace WinPieGestures.Tests;

/// <summary>
/// ISaveDebouncer 的测试替身（工程约定：mock 直接 new，不使用 mocking 框架）：
/// 不真实计时，记录调度参数并保留挂起动作，由测试用例手动 <see cref="TickPending"/>
/// 模拟防抖到期，或断言 <see cref="PendingCount"/> 验证折叠/取消语义。
/// </summary>
public sealed class TestSaveDebouncer : ISaveDebouncer
{
    private Action? _pending;

    /// <summary>累计 Schedule 调用次数。</summary>
    public int ScheduleCalls { get; private set; }

    /// <summary>最近一次 Schedule 收到的延迟。</summary>
    public TimeSpan? LastDelay { get; private set; }

    /// <summary>当前挂起动作数（防抖语义下为 0 或 1）。</summary>
    public int PendingCount => _pending == null ? 0 : 1;

    public void Schedule(Action action, TimeSpan delay)
    {
        ScheduleCalls++;
        LastDelay = delay;
        _pending = action;
    }

    public void CancelPending() => _pending = null;

    /// <summary>手动触发挂起动作（模拟防抖计时到期）；无挂起时为空操作。</summary>
    public void TickPending()
    {
        var action = _pending;
        _pending = null;
        action?.Invoke();
    }
}
