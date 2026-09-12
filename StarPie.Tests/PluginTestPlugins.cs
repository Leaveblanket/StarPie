using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.Services.Messages;
using StarPie.Services.Programs;

namespace StarPie.Tests;

/// <summary>
/// 装载管线夹具插件：记录宿主传入的上下文 id（静态面在插件 ALC 的副本内独立，
/// 宿主经反射读取实例属性）。
/// </summary>
public sealed class RecordingTestPlugin : IPlugin
{
    /// <summary>宿主经 <see cref="IPluginContext.PluginId"/> 传入的插件 id；启动前为 null。</summary>
    public string? ObservedPluginId { get; private set; }

    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        ObservedPluginId = context.PluginId;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>启动即抛异常的夹具：验证启动失败进入隔离而非活动态。</summary>
public sealed class ThrowingTestPlugin : IPlugin
{
    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("夹具启动爆炸");

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>未实现 <see cref="IPlugin"/> 的夹具类型：验证入口类型判定。</summary>
public sealed class NotAnEntryTestPlugin
{
}

/// <summary>尊重取消令牌的夹具：启动一直等待，直到宿主取消。</summary>
public sealed class TokenAwareTestPlugin : IPlugin
{
    /// <inheritdoc/>
    public async Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        => await Task.Delay(Timeout.Infinite, cancellationToken);

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>能力注册夹具：启动期注册一条程序来源能力（实现 SDK 的 IProgramScanner）。</summary>
public sealed class ProgramSourceTestPlugin : IPlugin, IProgramScanner
{
    private string _entryName = "unstarted";

    /// <inheritdoc/>
    public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        => new[] { new ProgramEntry(_entryName, @"C:\fixture.exe", "fixture.exe") };

    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        _entryName = context.PluginId;
        context.RegisterCapability<IProgramScanner>(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>能力失败夹具：注册的程序来源每次扫描都抛异常（验证守卫熔断与隔离）。</summary>
public sealed class ThrowingProgramSourceTestPlugin : IPlugin, IProgramScanner
{
    /// <inheritdoc/>
    public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        => throw new InvalidOperationException("夹具扫描爆炸");

    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        context.RegisterCapability<IProgramScanner>(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// 事件订阅夹具：启动期订阅宿主事件并计数；计数经静态字段暴露给测试（同一 ALC 副本内）。
/// </summary>
public sealed class EventSubscriberTestPlugin : IPlugin
{
    /// <summary>已收到的宿主事件数（经反射从该插件 ALC 内的类型读取）。</summary>
    public static int ReceivedCount;

    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        context.Events.Subscribe<MinimizedToTrayMessage>(
            _ => Interlocked.Increment(ref ReceivedCount));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>插件集自定义异常：验证异常经日志后不 root 插件 ALC 内的类型实例。</summary>
public sealed class PluginCustomException : Exception
{
    /// <summary>构造插件自定义异常。</summary>
    public PluginCustomException(string message) : base(message)
    {
    }
}

/// <summary>抛插件自定义异常的能力夹具：走真实 ALC 副本的自定义异常类型。</summary>
public sealed class CustomExceptionProgramSourceTestPlugin : IPlugin, IProgramScanner
{
    /// <inheritdoc/>
    public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        => throw new PluginCustomException("插件自定义异常");

    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        context.RegisterCapability<IProgramScanner>(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>停止即抛异常的夹具：验证卸载失败路径进入隔离而非静默成功。</summary>
public sealed class ThrowingStopTestPlugin : IPlugin
{
    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException("夹具停止爆炸");
}

/// <summary>StopAsync 写标记文件的夹具：宿主侧据此观察"配置落盘先于停用"。</summary>
public sealed class MarkerStopTestPlugin : IPlugin
{
    private string? _markerPath;

    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        _markerPath = PluginUnloadPipelineTests.StopMarkerPath(context.PluginId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_markerPath is not null)
        {
            File.WriteAllText(_markerPath, "stopped");
        }

        return Task.CompletedTask;
    }
}
