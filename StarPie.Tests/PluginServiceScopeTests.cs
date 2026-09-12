using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.Manifest;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Registry;

namespace StarPie.Tests;

/// <summary>
/// 每插件服务作用域缝：宿主服务经 <see cref="IPluginContext"/> 取用、句柄账本可清零、
/// 释放幂等且释放后不再受理登记；插件异常入日志前转 DTO，不把插件对象带进长生命周期结构。
/// </summary>
public sealed class PluginServiceScopeTests
{
    private const string PluginId = "com.example.scope";

    [Fact]
    public void 作用域_上下文暴露插件id_与宿主服务()
    {
        PluginServiceScope scope = CreateActiveScope();

        Assert.Equal(PluginId, scope.PluginId);
        Assert.Equal(PluginId, scope.Context.PluginId);
        Assert.NotNull(scope.Context.Log);
        Assert.NotNull(scope.Context.Events);
        Assert.False(scope.IsDisposed);
        Assert.Equal(0, scope.HandleCount);
    }

    [Fact]
    public void 句柄登记与自行释放_账本随之增减()
    {
        PluginServiceScope scope = CreateActiveScope();

        IDisposable first = scope.RegisterHandle(new ProbeHandle());
        IDisposable second = scope.RegisterHandle(new ProbeHandle());
        Assert.Equal(2, scope.HandleCount);

        first.Dispose();
        Assert.Equal(1, scope.HandleCount);

        // 句柄 Dispose 幂等：重复释放不影响账本。
        first.Dispose();
        Assert.Equal(1, scope.HandleCount);

        second.Dispose();
        Assert.Equal(0, scope.HandleCount);
    }

    [Fact]
    public void 释放作用域_账本清零_幂等且释放后拒绝登记()
    {
        PluginServiceScope scope = CreateActiveScope();
        var handle = new ProbeHandle();
        scope.RegisterHandle(handle);

        scope.Dispose();

        Assert.True(scope.IsDisposed);
        Assert.Equal(0, scope.HandleCount);
        Assert.True(handle.Disposed);
        Assert.Throws<ObjectDisposedException>(() => scope.RegisterHandle(new ProbeHandle()));

        // 幂等：重复释放不抛、不改变已释放状态。
        scope.Dispose();
        Assert.True(scope.IsDisposed);
        Assert.Equal(0, scope.HandleCount);
    }

    [Fact]
    public void 句柄清理抛异常_作用域仍标记释放_且抛聚合异常()
    {
        PluginServiceScope scope = CreateActiveScope();
        scope.RegisterHandle(new ProbeHandle(throwOnDispose: true));
        var healthy = new ProbeHandle();
        scope.RegisterHandle(healthy);

        Assert.Throws<AggregateException>(() => scope.Dispose());

        // 一个坏句柄不影响其余句柄清理与账本清零。
        Assert.True(scope.IsDisposed);
        Assert.True(healthy.Disposed);
        Assert.Equal(0, scope.HandleCount);
    }

    [Fact]
    public void 事件订阅_句柄入账_发布投递_退订出账()
    {
        PluginServiceScope scope = CreateActiveScope();
        var received = new List<ScopeProbeEvent>();

        IDisposable subscription = scope.Context.Events.Subscribe<ScopeProbeEvent>(received.Add);
        Assert.Equal(1, scope.HandleCount);

        scope.Publish(new ScopeProbeEvent("one"));
        scope.Publish(new ScopeProbeEvent("two"));
        Assert.Equal(new[] { "one", "two" }, received.Select(item => item.Payload));

        subscription.Dispose();
        Assert.Equal(0, scope.HandleCount);
        scope.Publish(new ScopeProbeEvent("three"));
        Assert.Equal(2, received.Count);
    }

    [Fact]
    public void 释放作用域_强制清理订阅_不再投递()
    {
        PluginServiceScope scope = CreateActiveScope();
        var received = new List<ScopeProbeEvent>();
        scope.Context.Events.Subscribe<ScopeProbeEvent>(received.Add);

        scope.Dispose();

        Assert.Equal(0, scope.HandleCount);
        scope.Publish(new ScopeProbeEvent("after-dispose"));
        Assert.Empty(received);
    }

    [Fact]
    public void 非活动态_发布不投递()
    {
        // 状态机停在 Starting：插件代码未获准执行，宿主中介事件不得投递。
        var lifecycle = new PluginLifecycleStateMachine();
        lifecycle.Transition(PluginLifecycleState.Validated);
        lifecycle.Transition(PluginLifecycleState.Loading);
        lifecycle.Transition(PluginLifecycleState.Starting);
        PluginServiceScope scope = CreateScope(lifecycle: lifecycle);
        var received = new List<ScopeProbeEvent>();
        scope.Context.Events.Subscribe<ScopeProbeEvent>(received.Add);

        scope.Publish(new ScopeProbeEvent("starting"));

        Assert.Empty(received);
    }

    [Fact]
    public void 停止中_发布不投递()
    {
        var lifecycle = new PluginLifecycleStateMachine();
        PluginServiceScope scope = CreateActiveScope(lifecycle: lifecycle);
        var received = new List<ScopeProbeEvent>();
        scope.Context.Events.Subscribe<ScopeProbeEvent>(received.Add);
        scope.Publish(new ScopeProbeEvent("active"));
        Assert.Single(received);

        lifecycle.Transition(PluginLifecycleState.Stopping);
        scope.Publish(new ScopeProbeEvent("stopping"));

        Assert.Single(received);
    }

    [Fact]
    public void 事件处理器抛异常_记日志但不打断发布_不影响其余订阅者()
    {
        var sink = new RecordingPluginLogSink();
        PluginServiceScope scope = CreateActiveScope(sink: sink);
        var received = new List<ScopeProbeEvent>();
        scope.Context.Events.Subscribe<ScopeProbeEvent>(
            _ => throw new InvalidOperationException("事件处理器爆炸"));
        scope.Context.Events.Subscribe<ScopeProbeEvent>(received.Add);

        scope.Publish(new ScopeProbeEvent("one"));

        Assert.Single(received);
        PluginLogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal(PluginLogLevel.Error, entry.Level);
        Assert.Contains(nameof(InvalidOperationException), entry.ExceptionType);
        Assert.Equal("事件处理器爆炸", entry.ExceptionMessage);
    }

    [Fact]
    public void 日志_带插件id与级别_异常内容落为字符串()
    {
        var sink = new RecordingPluginLogSink();
        PluginServiceScope scope = CreateActiveScope(sink: sink);

        scope.Context.Log.Write(PluginLogLevel.Warning, "普通日志");
        scope.Context.Log.Write(PluginLogLevel.Error, "带异常", new InvalidOperationException("爆炸"));

        PluginLogEntry plain = sink.Entries[0];
        Assert.Equal(PluginId, plain.PluginId);
        Assert.Equal(PluginLogLevel.Warning, plain.Level);
        Assert.Equal("普通日志", plain.Message);
        Assert.Null(plain.ExceptionType);

        PluginLogEntry fault = sink.Entries[1];
        Assert.Contains(nameof(InvalidOperationException), fault.ExceptionType);
        Assert.Equal("爆炸", fault.ExceptionMessage);
        Assert.False(string.IsNullOrEmpty(fault.ExceptionDetail));
    }

    [Fact]
    public void 日志_异常转DTO后不root插件异常实例()
    {
        var sink = new RecordingPluginLogSink();
        PluginServiceScope scope = CreateActiveScope(sink: sink);

        WeakReference probe = WriteProbeExceptionLog(scope.Context.Log);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(probe.IsAlive, "日志 sink 不得持有插件异常实例");
        PluginLogEntry entry = Assert.Single(sink.Entries);
        Assert.Contains(nameof(PluginProbeException), entry.ExceptionType);
        Assert.Equal("探针异常", entry.ExceptionMessage);
    }

    [Fact]
    public void 释放后写日志_静默丢弃()
    {
        var sink = new RecordingPluginLogSink();
        PluginServiceScope scope = CreateActiveScope(sink: sink);
        scope.Dispose();

        scope.Context.Log.Write(PluginLogLevel.Error, "释放后");
        scope.Context.Log.Write(PluginLogLevel.Error, "释放后带异常", new InvalidOperationException("x"));

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void 日志条目_声明面只含字符串与值类型()
    {
        Type[] allowed =
        {
            typeof(string), typeof(PluginLogLevel), typeof(DateTimeOffset),
        };
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // 工厂方法（Create/FromException）是异常转 DTO 的边界入口，其入参允许出现 Exception；
        // 结论面是所有属性（长生命周期结构实际保存的字段）只含字符串与值类型。
        Type[] memberTypes = typeof(PluginLogEntry)
            .GetProperties(Declared).Select(property => property.PropertyType)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(memberTypes);
        Assert.All(memberTypes, type => Assert.Contains(type, allowed));
    }

    /// <summary>在独立方法内构造插件异常，避免 JIT 把局部变量保活到 GC 之后。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WriteProbeExceptionLog(IPluginLog log)
    {
        var probe = new PluginProbeException("探针异常");
        log.Write(PluginLogLevel.Error, "插件调用失败", probe);
        return new WeakReference(probe);
    }

    private static PluginServiceScope CreateActiveScope(
        PluginLifecycleStateMachine? lifecycle = null,
        CapabilityRegistry? registry = null,
        RecordingPluginLogSink? sink = null)
    {
        var machine = lifecycle ?? new PluginLifecycleStateMachine();
        if (machine.Current != PluginLifecycleState.Active)
        {
            machine.Transition(PluginLifecycleState.Validated);
            machine.Transition(PluginLifecycleState.Loading);
            machine.Transition(PluginLifecycleState.Starting);
            machine.Transition(PluginLifecycleState.Active);
        }

        return CreateScope(machine, registry, sink);
    }

    private static PluginServiceScope CreateScope(
        PluginLifecycleStateMachine? lifecycle = null,
        CapabilityRegistry? registry = null,
        RecordingPluginLogSink? sink = null)
        => new(
            new PluginManifest
            {
                SchemaVersion = 1,
                Id = PluginId,
                Name = PluginId,
                Version = "1.0.0",
                Sdk = "1.0",
                EntryAssembly = "StarPie.Tests.dll",
                EntryType = "StarPie.Tests.ScopeProbePlugin",
            },
            lifecycle ?? new PluginLifecycleStateMachine(),
            registry ?? new CapabilityRegistry(),
            sink);
}

/// <summary>宿主中介事件探针。</summary>
public sealed record ScopeProbeEvent(string Payload);

/// <summary>定义在测试集内的自定义异常：模拟插件集自定义类型。</summary>
public sealed class PluginProbeException : Exception
{
    /// <summary>构造探针异常。</summary>
    public PluginProbeException(string message) : base(message)
    {
    }
}

/// <summary>作用域句柄探针：记录是否被清理。</summary>
public sealed class ProbeHandle : IDisposable
{
    private readonly bool _throwOnDispose;

    /// <summary>构造探针；<paramref name="throwOnDispose"/> 为 true 时释放即抛。</summary>
    public ProbeHandle(bool throwOnDispose = false) => _throwOnDispose = throwOnDispose;

    /// <summary>是否已释放。</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Disposed = true;
        if (_throwOnDispose)
        {
            throw new InvalidOperationException("句柄清理失败（探针）");
        }
    }
}
