using System;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugins;

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
