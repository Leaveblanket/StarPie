using System;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Registry;

namespace StarPie.Tests;

/// <summary>
/// 能力调用守卫缝：状态检查、在途计数、超时、异常捕获、连续失败熔断与隔离。
/// </summary>
public sealed class CapabilityGuardTests
{
    private const string PluginId = "com.example.guard";
    private const string CapabilityId = PluginCapabilityTestDoubles.ProbeCapabilityId;

    [Fact]
    public void 非活动态_拒绝新调用_不计在途()
    {
        var lifecycle = new PluginLifecycleStateMachine();
        lifecycle.Transition(PluginLifecycleState.Validated);
        var sink = new RecordingPluginLogSink();
        var guard = new CapabilityGuard(PluginId, lifecycle, sink);

        var exception = Assert.Throws<CapabilityUnavailableException>(
            () => guard.Invoke(CapabilityId, () => "never"));

        Assert.Equal(PluginId, exception.PluginId);
        Assert.Equal(PluginLifecycleState.Validated, exception.State);
        Assert.Equal(0, guard.InFlightCount);
        Assert.Equal(0, guard.ConsecutiveFailures);
        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void 成功调用_连续失败复位_在途归零()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var guard = new CapabilityGuard(PluginId, lifecycle);
        Assert.Throws<InvalidOperationException>(
            () => guard.Invoke<string>(CapabilityId, () => throw new InvalidOperationException("一次失败")));
        Assert.Equal(1, guard.ConsecutiveFailures);

        string result = guard.Invoke(CapabilityId, () => "ok");

        Assert.Equal("ok", result);
        Assert.Equal(0, guard.ConsecutiveFailures);
        Assert.Equal(0, guard.InFlightCount);
        Assert.False(guard.IsCircuitOpen);
    }

    [Fact]
    public void 同步异常_记账重抛_日志只留DTO()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var sink = new RecordingPluginLogSink();
        var guard = new CapabilityGuard(PluginId, lifecycle, sink);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => guard.Invoke<string>(CapabilityId, () => throw new InvalidOperationException("同步爆炸")));

        Assert.Equal("同步爆炸", thrown.Message);
        Assert.Equal(1, guard.ConsecutiveFailures);
        Assert.Equal(0, guard.InFlightCount);
        PluginLogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal(PluginId, entry.PluginId);
        Assert.Equal(PluginLogLevel.Error, entry.Level);
        Assert.Contains(nameof(InvalidOperationException), entry.ExceptionType);
        Assert.Equal("同步爆炸", entry.ExceptionMessage);
    }

    [Fact]
    public void 连续失败达阈值_熔断并隔离_后续调用快速失败且不再进入插件()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var sink = new RecordingPluginLogSink();
        var guard = new CapabilityGuard(
            PluginId,
            lifecycle,
            sink,
            new CapabilityGuardOptions { FailureThreshold = 2 });
        var probe = new ProbeCapability
        {
            EchoImpl = _ => throw new InvalidOperationException("插件坏了"),
        };
        var adapter = new GuardedProbeCapability(guard, () => probe);

        Assert.Throws<InvalidOperationException>(() => adapter.Echo("one"));
        Assert.Throws<InvalidOperationException>(() => adapter.Echo("two"));

        Assert.True(guard.IsCircuitOpen);
        Assert.Equal(PluginLifecycleState.Quarantined, lifecycle.Current);
        Assert.Contains("连续失败", lifecycle.QuarantineReason);
        Assert.Equal(2, probe.EchoCalls);
        Assert.Equal(PluginLogLevel.Critical, sink.Entries[^1].Level);

        // 熔断后快速失败：不再进入插件实现。
        Assert.Throws<CapabilityCircuitOpenException>(() => adapter.Echo("three"));
        Assert.Equal(2, probe.EchoCalls);
    }

    [Fact]
    public async Task 异步超时_记失败并抛超时异常_在途归零()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var sink = new RecordingPluginLogSink();
        var guard = new CapabilityGuard(
            PluginId,
            lifecycle,
            sink,
            new CapabilityGuardOptions { CallTimeout = TimeSpan.FromMilliseconds(80) });
        var probe = new ProbeCapability
        {
            EchoAsyncImpl = async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return "never";
            },
        };

        var timeout = await Assert.ThrowsAsync<CapabilityCallTimeoutException>(
            () => new GuardedProbeCapability(guard, () => probe).EchoAsync("slow", CancellationToken.None));

        Assert.Equal(PluginId, timeout.PluginId);
        Assert.Equal(CapabilityId, timeout.CapabilityId);
        Assert.Equal(1, guard.ConsecutiveFailures);
        Assert.True(await guard.WaitForInFlightAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.Equal(PluginLifecycleState.Active, lifecycle.Current);
        Assert.Equal(PluginLogLevel.Error, Assert.Single(sink.Entries).Level);
    }

    [Fact]
    public async Task 调用方主动取消_不计失败_在途归零()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var guard = new CapabilityGuard(
            PluginId,
            lifecycle,
            logSink: null,
            new CapabilityGuardOptions { CallTimeout = TimeSpan.FromMilliseconds(150) });
        int pluginEntered = 0;
        int pluginCompleted = 0;
        var probe = new ProbeCapability
        {
            EchoAsyncImpl = async (_, token) =>
            {
                Interlocked.Increment(ref pluginEntered);
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                finally
                {
                    Interlocked.Increment(ref pluginCompleted);
                }

                return "never";
            },
        };
        using var cancellation = new CancellationTokenSource();
        Task<string> pending = new GuardedProbeCapability(guard, () => probe)
            .EchoAsync("slow", cancellation.Token);
        cancellation.Cancel();

        Exception? thrown = await Record.ExceptionAsync(() => pending);
        Assert.NotNull(thrown);
        Assert.IsAssignableFrom<OperationCanceledException>(thrown);
        Assert.Equal(1, pluginEntered);

        Assert.Equal(0, guard.ConsecutiveFailures);
        Assert.True(await guard.WaitForInFlightAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.Equal(0, guard.InFlightCount);
        // 超时源到期后取消了协作式实现：在途按插件代码真实结束出账。
        Assert.Equal(1, pluginCompleted);
        Assert.Equal(PluginLifecycleState.Active, lifecycle.Current);
    }

    [Fact]
    public async Task 在途排空_归零等待返回true_超时返回false()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var guard = new CapabilityGuard(PluginId, lifecycle);
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeCapability { EchoAsyncImpl = (_, _) => gate.Task };

        Task<string> pending = new GuardedProbeCapability(guard, () => probe)
            .EchoAsync("wait", CancellationToken.None);
        Assert.Equal(1, guard.InFlightCount);
        Assert.False(await guard.WaitForInFlightAsync(
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None));

        gate.SetResult("done");
        Assert.Equal("done", await pending);
        Assert.Equal(0, guard.InFlightCount);
        Assert.True(await guard.WaitForInFlightAsync(
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None));
    }

    [Fact]
    public void 活动态转停止中_拒绝新调用()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var guard = new CapabilityGuard(PluginId, lifecycle);
        var probe = new ProbeCapability();
        var adapter = new GuardedProbeCapability(guard, () => probe);
        Assert.Equal("ok", adapter.Echo("ok"));

        lifecycle.Transition(PluginLifecycleState.Stopping);

        var exception = Assert.Throws<CapabilityUnavailableException>(() => adapter.Echo("late"));
        Assert.Equal(PluginLifecycleState.Stopping, exception.State);
        Assert.Equal(1, probe.EchoCalls);
        Assert.Equal(0, guard.InFlightCount);
    }

    [Fact]
    public async Task 同步调用超时_抛超时异常_在途保持到插件代码真正结束()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var guard = new CapabilityGuard(
            PluginId,
            lifecycle,
            logSink: null,
            new CapabilityGuardOptions { CallTimeout = TimeSpan.FromMilliseconds(80) });
        using var gate = new ManualResetEventSlim(initialState: false);
        var probe = new ProbeCapability
        {
            EchoImpl = _ =>
            {
                gate.Wait();
                return "late";
            },
        };
        var adapter = new GuardedProbeCapability(guard, () => probe);

        await Assert.ThrowsAsync<CapabilityCallTimeoutException>(() => Task.Run(() => adapter.Echo("slow")));

        // 超时只是调用方不再等待：插件代码还在跑，在途计数不得谎报归零。
        Assert.Equal(1, guard.InFlightCount);
        Assert.Equal(1, guard.ConsecutiveFailures);
        Assert.False(await guard.WaitForInFlightAsync(
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None));

        gate.Set();

        Assert.True(await guard.WaitForInFlightAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.Equal(0, guard.InFlightCount);
    }

    [Fact]
    public void 熔断阈值非法_构造即拒绝()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CapabilityGuard(
            PluginId,
            lifecycle,
            logSink: null,
            new CapabilityGuardOptions { FailureThreshold = 0 }));
    }
}
