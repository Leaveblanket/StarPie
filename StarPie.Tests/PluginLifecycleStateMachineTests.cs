using System;
using System.Collections.Generic;
using System.Linq;
using StarPie.PluginRuntime.Lifecycle;

namespace StarPie.Tests;

/// <summary>
/// 插件生命周期状态机缝：装载链、headless 与 UI 两条卸载链、全转移矩阵的拒绝路径、
/// 失败隔离（原因记录与终态冻结）与转移记录。
/// </summary>
public sealed class PluginLifecycleStateMachineTests
{
    [Fact]
    public void 新状态机_停在已发现_无转移记录()
    {
        var machine = new PluginLifecycleStateMachine();

        Assert.Equal(PluginLifecycleState.Discovered, machine.Current);
        Assert.Empty(machine.Transitions);
        Assert.Null(machine.QuarantineReason);
        Assert.False(machine.IsTerminal);
    }

    [Fact]
    public void 装载链_逐级转移至活动()
    {
        var machine = new PluginLifecycleStateMachine();

        machine.Transition(PluginLifecycleState.Validated);
        machine.Transition(PluginLifecycleState.Loading);
        machine.Transition(PluginLifecycleState.Starting);
        machine.Transition(PluginLifecycleState.Active);

        Assert.Equal(PluginLifecycleState.Active, machine.Current);
        Assert.Equal(
            new[]
            {
                PluginLifecycleState.Validated,
                PluginLifecycleState.Loading,
                PluginLifecycleState.Starting,
                PluginLifecycleState.Active,
            },
            machine.Transitions.Select(transition => transition.To));
        Assert.Equal(PluginLifecycleState.Discovered, machine.Transitions[0].From);
    }

    [Fact]
    public void headless卸载链_停止后直接卸载()
    {
        PluginLifecycleStateMachine machine = ReachState(PluginLifecycleState.Active);

        machine.Transition(PluginLifecycleState.Stopping);
        machine.Transition(PluginLifecycleState.Unloading);
        machine.Transition(PluginLifecycleState.Unloaded);

        Assert.Equal(PluginLifecycleState.Unloaded, machine.Current);
        Assert.True(machine.IsTerminal);
        Assert.Equal(
            new[] { PluginLifecycleState.Stopping, PluginLifecycleState.Unloading, PluginLifecycleState.Unloaded },
            machine.Transitions.TakeLast(3).Select(transition => transition.To));
    }

    [Fact]
    public void UI卸载链_停止后先释放UI再卸载()
    {
        PluginLifecycleStateMachine machine = ReachState(PluginLifecycleState.Active);

        machine.Transition(PluginLifecycleState.Stopping);
        machine.Transition(PluginLifecycleState.ReleasingUi);
        machine.Transition(PluginLifecycleState.Unloading);
        machine.Transition(PluginLifecycleState.Unloaded);

        Assert.Equal(PluginLifecycleState.Unloaded, machine.Current);
    }

    [Fact]
    public void 任意非终态_可进入隔离并记录原因()
    {
        PluginLifecycleStateMachine machine = ReachState(PluginLifecycleState.Loading);

        machine.Quarantine("入口程序集缺失");

        Assert.Equal(PluginLifecycleState.Quarantined, machine.Current);
        Assert.Equal("入口程序集缺失", machine.QuarantineReason);
        Assert.True(machine.IsTerminal);
        Assert.Equal(
            new PluginLifecycleTransition(PluginLifecycleState.Loading, PluginLifecycleState.Quarantined),
            machine.Transitions[^1]);
    }

    [Fact]
    public void 隔离必须带原因_空原因拒绝且不改状态()
    {
        var machine = new PluginLifecycleStateMachine();

        Assert.ThrowsAny<ArgumentException>(() => machine.Quarantine("   "));

        Assert.Equal(PluginLifecycleState.Discovered, machine.Current);
        Assert.Empty(machine.Transitions);
    }

    [Fact]
    public void 隔离不能经Transition进入_必须走Quarantine()
    {
        var machine = new PluginLifecycleStateMachine();

        Assert.Throws<InvalidOperationException>(
            () => machine.Transition(PluginLifecycleState.Quarantined));
        Assert.Equal(PluginLifecycleState.Discovered, machine.Current);
    }

    [Fact]
    public void 终态冻结_已卸载与已隔离都不再接受转移()
    {
        PluginLifecycleStateMachine unloaded = ReachState(PluginLifecycleState.Unloaded);
        Assert.Throws<InvalidOperationException>(() => unloaded.Transition(PluginLifecycleState.Discovered));
        Assert.Throws<InvalidOperationException>(() => unloaded.Quarantine("再次隔离"));

        PluginLifecycleStateMachine quarantined = ReachState(PluginLifecycleState.Quarantined);
        Assert.Throws<InvalidOperationException>(() => quarantined.Transition(PluginLifecycleState.Active));
        Assert.Throws<InvalidOperationException>(() => quarantined.Quarantine("再次隔离"));
    }

    /// <summary>全转移矩阵：只有规范允许的相邻转移成立，其余（含跳级、回退与终态出走）一律拒绝。</summary>
    [Theory]
    [MemberData(nameof(TransitionMatrix))]
    public void 全转移矩阵_仅允许相邻转移(PluginLifecycleState from, PluginLifecycleState to)
    {
        PluginLifecycleStateMachine machine = ReachState(from);
        bool allowed = AllowedPairs.Contains((from, to));

        if (allowed)
        {
            machine.Transition(to);
            Assert.Equal(to, machine.Current);
            return;
        }

        Assert.Throws<InvalidOperationException>(() => machine.Transition(to));
        Assert.Equal(from, machine.Current);
    }

    /// <summary>测试侧的期望转移表：独立于状态机实现书写，避免断言退化为实现的镜像。</summary>
    private static readonly HashSet<(PluginLifecycleState From, PluginLifecycleState To)> AllowedPairs = new()
    {
        (PluginLifecycleState.Discovered, PluginLifecycleState.Validated),
        (PluginLifecycleState.Validated, PluginLifecycleState.Loading),
        (PluginLifecycleState.Loading, PluginLifecycleState.Starting),
        (PluginLifecycleState.Starting, PluginLifecycleState.Active),
        (PluginLifecycleState.Active, PluginLifecycleState.Stopping),
        (PluginLifecycleState.Stopping, PluginLifecycleState.ReleasingUi),
        (PluginLifecycleState.Stopping, PluginLifecycleState.Unloading),
        (PluginLifecycleState.ReleasingUi, PluginLifecycleState.Unloading),
        (PluginLifecycleState.Unloading, PluginLifecycleState.Unloaded),
    };

    /// <summary>全部状态两两组合（含跳到隔离态，验证它只能走 Quarantine）。</summary>
    public static IEnumerable<object[]> TransitionMatrix()
    {
        foreach (PluginLifecycleState from in Enum.GetValues<PluginLifecycleState>())
        {
            foreach (PluginLifecycleState to in Enum.GetValues<PluginLifecycleState>())
            {
                yield return new object[] { from, to };
            }
        }
    }

    /// <summary>把状态机推进到指定状态（只走规范允许的转移；隔离与已卸载分别经其入口构造）。</summary>
    private static PluginLifecycleStateMachine ReachState(PluginLifecycleState state)
    {
        var machine = new PluginLifecycleStateMachine();
        if (state == PluginLifecycleState.Discovered)
        {
            return machine;
        }

        if (state == PluginLifecycleState.Quarantined)
        {
            machine.Quarantine("测试隔离");
            return machine;
        }

        machine.Transition(PluginLifecycleState.Validated);
        if (state == PluginLifecycleState.Validated)
        {
            return machine;
        }

        machine.Transition(PluginLifecycleState.Loading);
        if (state == PluginLifecycleState.Loading)
        {
            return machine;
        }

        machine.Transition(PluginLifecycleState.Starting);
        if (state == PluginLifecycleState.Starting)
        {
            return machine;
        }

        machine.Transition(PluginLifecycleState.Active);
        if (state == PluginLifecycleState.Active)
        {
            return machine;
        }

        machine.Transition(PluginLifecycleState.Stopping);
        if (state == PluginLifecycleState.Stopping)
        {
            return machine;
        }

        machine.Transition(PluginLifecycleState.ReleasingUi);
        if (state == PluginLifecycleState.ReleasingUi)
        {
            return machine;
        }

        machine.Transition(PluginLifecycleState.Unloading);
        if (state == PluginLifecycleState.Unloading)
        {
            return machine;
        }

        machine.Transition(PluginLifecycleState.Unloaded);
        return machine;
    }
}
