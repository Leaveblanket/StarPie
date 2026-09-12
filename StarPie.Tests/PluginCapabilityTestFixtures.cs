using System;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.Manifest;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Registry;
using StarPie.Services.Programs;

namespace StarPie.Tests;

/// <summary>守卫适配器夹具用的能力接口：窄接口 + 同步/异步各一条路径。</summary>
public interface IGuardProbeCapability
{
    /// <summary>同步回显。</summary>
    string Echo(string value);

    /// <summary>异步回显（尊重取消令牌，供超时路径）。</summary>
    Task<string> EchoAsync(string value, CancellationToken cancellationToken);
}

/// <summary>手写守卫适配器：每个能力接口一个窄适配器，调用一律经 <see cref="CapabilityGuard"/>。</summary>
public sealed class GuardedProbeCapability : IGuardProbeCapability
{
    private const string CapabilityId = "guard-probe";

    private readonly CapabilityGuard _guard;
    private readonly Func<IGuardProbeCapability> _resolve;

    /// <summary>构造适配器：只持守卫与实例解析器，不缓存插件实例。</summary>
    /// <param name="guard">该插件的能力守卫。</param>
    /// <param name="resolve">实例解析器（每次调用现取）。</param>
    public GuardedProbeCapability(CapabilityGuard guard, Func<IGuardProbeCapability> resolve)
    {
        _guard = guard;
        _resolve = resolve;
    }

    /// <inheritdoc/>
    public string Echo(string value) => _guard.Invoke(CapabilityId, () => _resolve().Echo(value));

    /// <inheritdoc/>
    public Task<string> EchoAsync(string value, CancellationToken cancellationToken)
        => _guard.InvokeAsync(
            CapabilityId,
            token => _resolve().EchoAsync(value, token),
            cancellationToken: cancellationToken);
}

/// <summary>能力实现探针：回显 + 可编排的失败/阻塞，带调用计数。</summary>
public sealed class ProbeCapability : IGuardProbeCapability
{
    /// <summary>同步调用实现；缺省回显。</summary>
    public Func<string, string>? EchoImpl { get; set; }

    /// <summary>异步调用实现；缺省回显。</summary>
    public Func<string, CancellationToken, Task<string>>? EchoAsyncImpl { get; set; }

    /// <summary>同步调用次数。</summary>
    public int EchoCalls { get; private set; }

    /// <inheritdoc/>
    public string Echo(string value)
    {
        EchoCalls++;
        return EchoImpl?.Invoke(value) ?? value;
    }

    /// <inheritdoc/>
    public Task<string> EchoAsync(string value, CancellationToken cancellationToken)
        => EchoAsyncImpl?.Invoke(value, cancellationToken) ?? Task.FromResult(value);
}

/// <summary>程序来源能力（SDK 共享契约）的手写守卫适配器。</summary>
public sealed class GuardedProgramScanner : IProgramScanner
{
    private const string CapabilityId = "program-source";

    private readonly CapabilityGuard _guard;
    private readonly Func<IProgramScanner> _resolve;

    /// <summary>构造适配器：只持守卫与实例解析器，不缓存插件实例。</summary>
    /// <param name="guard">该插件的能力守卫。</param>
    /// <param name="resolve">实例解析器（每次调用现取）。</param>
    public GuardedProgramScanner(CapabilityGuard guard, Func<IProgramScanner> resolve)
    {
        _guard = guard;
        _resolve = resolve;
    }

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        => _guard.Invoke(CapabilityId, () => _resolve().ScanInstalledPrograms());
}

/// <summary>能力与作用域测试夹具：清单、契约与活动态作用域的构造集中一处。</summary>
internal static class PluginCapabilityTestDoubles
{
    /// <summary>探针能力 id。</summary>
    internal const string ProbeCapabilityId = "guard-probe";

    /// <summary>探针能力契约（宿主声明面）。</summary>
    internal static CapabilityContract ProbeContract { get; } = new(
        ProbeCapabilityId,
        1,
        typeof(IGuardProbeCapability),
        (guard, resolve) => new GuardedProbeCapability(guard, () => (IGuardProbeCapability)resolve()));

    /// <summary>程序来源能力 id（测试夹具借用 SDK 的程序扫描契约）。</summary>
    internal const string ProgramSourceCapabilityId = "program-source";

    /// <summary>程序来源能力契约。</summary>
    internal static CapabilityContract ProgramSourceContract { get; } = new(
        ProgramSourceCapabilityId,
        1,
        typeof(IProgramScanner),
        (guard, resolve) => new GuardedProgramScanner(guard, () => (IProgramScanner)resolve()));

    /// <summary>构造只声明程序来源契约的能力表。</summary>
    internal static CapabilityRegistry CreateProgramSourceRegistry()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(ProgramSourceContract);
        return registry;
    }

    /// <summary>构造清单：可声明探针能力与 priority。</summary>
    internal static PluginManifest Manifest(
        string pluginId,
        int priority = 0,
        bool declareProbeCapability = true)
        => new()
        {
            SchemaVersion = 1,
            Id = pluginId,
            Name = pluginId,
            Version = "1.0.0",
            Sdk = "1.0",
            EntryAssembly = "StarPie.Tests.dll",
            EntryType = "StarPie.Tests.ProbePlugin",
            Priority = priority,
            Capabilities = declareProbeCapability
                ? new System.Collections.Generic.List<PluginCapabilityReference>
                {
                    new() { Id = ProbeCapabilityId, Abi = 1 },
                }
                : null,
        };

    /// <summary>构造状态机并沿装载链推到指定状态（只支持装载链上的状态）。</summary>
    internal static PluginLifecycleStateMachine Lifecycle(PluginLifecycleState state)
    {
        var lifecycle = new PluginLifecycleStateMachine();
        if (state == PluginLifecycleState.Discovered)
        {
            return lifecycle;
        }

        lifecycle.Transition(PluginLifecycleState.Validated);
        if (state == PluginLifecycleState.Validated)
        {
            return lifecycle;
        }

        lifecycle.Transition(PluginLifecycleState.Loading);
        if (state == PluginLifecycleState.Loading)
        {
            return lifecycle;
        }

        lifecycle.Transition(PluginLifecycleState.Starting);
        if (state == PluginLifecycleState.Starting)
        {
            return lifecycle;
        }

        lifecycle.Transition(PluginLifecycleState.Active);
        if (state != PluginLifecycleState.Active)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "夹具只支持装载链状态（Discover→Active）");
        }

        return lifecycle;
    }

    /// <summary>构造作用域：缺省活动态、契约已声明、清单已声明探针能力。</summary>
    internal static PluginServiceScope CreateScope(
        CapabilityRegistry registry,
        string pluginId = "com.example.probe",
        int priority = 0,
        PluginLifecycleState state = PluginLifecycleState.Active,
        IPluginLogSink? logSink = null,
        CapabilityGuardOptions? guardOptions = null,
        bool declareProbeCapability = true,
        PluginLifecycleStateMachine? lifecycle = null)
        => new(
            Manifest(pluginId, priority, declareProbeCapability),
            lifecycle ?? Lifecycle(state),
            registry,
            logSink,
            guardOptions);
}
