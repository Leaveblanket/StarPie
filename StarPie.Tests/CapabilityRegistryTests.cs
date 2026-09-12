using System;
using System.Linq;
using StarPie.HostServices;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Registry;

namespace StarPie.Tests;

/// <summary>
/// 能力表缝：内置优先、清单 priority + plugin id 稳定序、非活动插件不可见、注册校验与作用域摘除。
/// </summary>
public sealed class CapabilityRegistryTests
{
    [Fact]
    public void 内置优先_插件按priority与pluginId稳定序()
    {
        var registry = new CapabilityRegistry();
        var builtin = new ProbeCapability { EchoImpl = value => $"builtin:{value}" };
        registry.DeclareBuiltin(PluginCapabilityTestDoubles.ProbeContract, builtin);

        // 注册顺序打乱，验证结果只由 priority 与 plugin id 决定。
        PluginServiceScope bravo = Register(registry, "com.example.bravo", priority: 0, value: "bravo");
        PluginServiceScope charlie = Register(registry, "com.example.charlie", priority: -1, value: "charlie");
        PluginServiceScope alpha = Register(registry, "com.example.alpha", priority: 0, value: "alpha");

        string[] order = registry.GetAll<IGuardProbeCapability>()
            .Select(capability => capability.Echo("x"))
            .ToArray();

        Assert.Equal(
            new[] { "builtin:x", "charlie", "alpha", "bravo" },
            order);
        Assert.Same(builtin, registry.GetAll<IGuardProbeCapability>()[0]);
        Assert.Equal(0, charlie.HandleCount);
        Assert.Equal(0, alpha.HandleCount);
    }

    [Fact]
    public void 内置条目不可被插件覆盖_插件条目追加在内置之后()
    {
        var registry = new CapabilityRegistry();
        var builtin = new ProbeCapability { EchoImpl = _ => "builtin" };
        registry.DeclareBuiltin(PluginCapabilityTestDoubles.ProbeContract, builtin);
        Register(registry, "com.example.probe", priority: 0, value: "plugin");

        var entries = registry.GetAll<IGuardProbeCapability>();

        Assert.Equal(2, entries.Count);
        Assert.Same(builtin, entries[0]);
        Assert.Equal("plugin", entries[1].Echo("x"));
    }

    [Fact]
    public void 非活动插件条目不可见_进入活动态后可见()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Starting);
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(
            registry,
            pluginId: "com.example.probe",
            lifecycle: lifecycle);
        scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability());

        // 启动期（Starting）注册已入表但不可被消费者取用：只有 Active 允许执行插件代码。
        Assert.Empty(registry.GetAll<IGuardProbeCapability>());

        lifecycle.Transition(PluginLifecycleState.Active);
        Assert.Single(registry.GetAll<IGuardProbeCapability>());
    }

    [Fact]
    public void 活动态转停止中_条目不再可见()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(
            registry,
            pluginId: "com.example.probe",
            lifecycle: lifecycle);
        scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability());
        Assert.Single(registry.GetAll<IGuardProbeCapability>());

        lifecycle.Transition(PluginLifecycleState.Stopping);

        // Stopping 起拒绝新调用：能力表不再向消费者暴露该插件条目（在途调用由守卫排空）。
        Assert.Empty(registry.GetAll<IGuardProbeCapability>());
    }

    [Fact]
    public void 未声明的能力契约_取用即抛()
    {
        var registry = new CapabilityRegistry();

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.GetAll<IGuardProbeCapability>());

        Assert.Contains("未声明的能力契约", exception.Message);
    }

    [Fact]
    public void 契约重复声明_拒绝()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);

        Assert.Throws<InvalidOperationException>(
            () => registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract));
    }

    [Fact]
    public void 宿主未声明契约_插件注册被拒()
    {
        var registry = new CapabilityRegistry();
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(registry);

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability()));

        Assert.Contains("宿主未声明能力契约", exception.Message);
    }

    [Fact]
    public void 清单未声明该能力_插件注册被拒()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(
            registry,
            declareProbeCapability: false);

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability()));

        Assert.Contains("清单未声明能力", exception.Message);
    }

    [Fact]
    public void 同一插件重复注册同一能力_拒绝()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(registry);
        scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability());

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability()));

        Assert.Contains("能力重复注册", exception.Message);
        Assert.Single(registry.GetAll<IGuardProbeCapability>());
    }

    [Fact]
    public void 作用域释放_插件条目摘除()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        PluginServiceScope scope = Register(registry, "com.example.probe", priority: 0, value: "plugin");
        Assert.Single(registry.GetAll<IGuardProbeCapability>());

        scope.Dispose();

        Assert.Empty(registry.GetAll<IGuardProbeCapability>());
    }

    [Fact]
    public void 插件条目连续失败熔断_条目随之不可见()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(
            registry,
            pluginId: "com.example.probe",
            guardOptions: new CapabilityGuardOptions { FailureThreshold = 1 },
            lifecycle: lifecycle);
        scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability
        {
            EchoImpl = _ => throw new InvalidOperationException("插件坏了"),
        });

        IGuardProbeCapability capability = Assert.Single(registry.GetAll<IGuardProbeCapability>());
        Assert.Throws<InvalidOperationException>(() => capability.Echo("boom"));

        Assert.Equal(PluginLifecycleState.Quarantined, lifecycle.Current);
        Assert.Empty(registry.GetAll<IGuardProbeCapability>());
    }

    private static PluginServiceScope Register(
        CapabilityRegistry registry,
        string pluginId,
        int priority,
        string value)
    {
        if (registry.FindContract(typeof(IGuardProbeCapability)) is null)
        {
            registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        }

        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(
            registry,
            pluginId,
            priority);
        scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability
        {
            EchoImpl = _ => value,
        });
        return scope;
    }
}
