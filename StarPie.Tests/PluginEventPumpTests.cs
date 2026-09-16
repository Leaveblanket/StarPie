using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.HostServices;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Registry;
using StarPie.Services.Messages;

namespace StarPie.Tests;

/// <summary>
/// headless 插件宿主消息泵测试：装载即登记（弱引用），宿主 Send 托盘状态消息
/// 经泵同步广播到活动作用域（订阅方在 Send 调用线程执行）；作用域释放后不再投递；
/// 出口被回收后登记自动归零；单插件投递异常被兜底，不沿 Send 传播。
/// </summary>
public sealed class PluginEventPumpTests : IDisposable
{
    private const string PluginId = "com.example.tray-subscriber";

    private readonly string _tempRoot;
    private readonly string _pluginsRoot;

    public PluginEventPumpTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-pump-tests").FullName;
        _pluginsRoot = Path.Combine(_tempRoot, "plugins");
        Directory.CreateDirectory(_pluginsRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task 宿主Send_双方向消息_同步投递到已登记作用域()
    {
        var messenger = new WeakReferenceMessenger();
        var pump = new PluginEventPump(messenger);
        PluginLoadResult result = await new PluginLoadPipeline(
                new CapabilityRegistry(), eventPump: pump)
            .LoadAsync(CreateRequest(PluginId, typeof(TrayEventSubscriberTestPlugin)), CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Active, result.Status);
        Assert.Equal(1, pump.LiveSinkCount);
        Assert.Equal(0, ReadPluginStaticInt(result, "TrayEventSubscriberTestPlugin", "MinimizedCount"));
        Assert.Equal(0, ReadPluginStaticInt(result, "TrayEventSubscriberTestPlugin", "RestoredCount"));

        messenger.Send(MinimizedToTrayMessage.Instance);
        Assert.Equal(1, ReadPluginStaticInt(result, "TrayEventSubscriberTestPlugin", "MinimizedCount"));
        Assert.Equal(0, ReadPluginStaticInt(result, "TrayEventSubscriberTestPlugin", "RestoredCount"));

        messenger.Send(RestoredFromTrayMessage.Instance);
        Assert.Equal(1, ReadPluginStaticInt(result, "TrayEventSubscriberTestPlugin", "RestoredCount"));
    }

    [Fact]
    public async Task 作用域释放后_不再投递()
    {
        var messenger = new WeakReferenceMessenger();
        var pump = new PluginEventPump(messenger);
        PluginLoadResult result = await new PluginLoadPipeline(
                new CapabilityRegistry(), eventPump: pump)
            .LoadAsync(CreateRequest(PluginId, typeof(TrayEventSubscriberTestPlugin)), CancellationToken.None);

        result.Scope!.Dispose();
        messenger.Send(MinimizedToTrayMessage.Instance);
        messenger.Send(RestoredFromTrayMessage.Instance);

        Assert.Equal(0, ReadPluginStaticInt(result, "TrayEventSubscriberTestPlugin", "MinimizedCount"));
        Assert.Equal(0, ReadPluginStaticInt(result, "TrayEventSubscriberTestPlugin", "RestoredCount"));
    }

    [Fact]
    public void 出口被回收后_弱引用登记自动归零()
    {
        var messenger = new WeakReferenceMessenger();
        var pump = new PluginEventPump(messenger);

        WeakReference dropped = RegisterTransientSink(pump);
        Assert.Equal(1, pump.LiveSinkCount);

        for (int i = 0; i < 5 && dropped.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(dropped.IsAlive, "测试未丢弃作用域强引用，弱引用登记判定失真");
        // 登记不持有出口：回收后登记表自行失效，无需成对注销。
        Assert.Equal(0, pump.LiveSinkCount);
    }

    [Fact]
    public async Task 单插件投递异常_被兜底_不沿Send传播()
    {
        var messenger = new WeakReferenceMessenger();
        var pump = new PluginEventPump(messenger);

        // 先装载会抛异常的订阅者,再装载正常订阅者——异常不阻断后者收投递
        PluginLoadResult throwing = await new PluginLoadPipeline(
                new CapabilityRegistry(), eventPump: pump)
            .LoadAsync(CreateRequest("com.example.tray-throwing", typeof(TrayThrowingSubscriberTestPlugin)), CancellationToken.None);
        PluginLoadResult healthy = await new PluginLoadPipeline(
                new CapabilityRegistry(), eventPump: pump)
            .LoadAsync(CreateRequest(PluginId, typeof(TrayEventSubscriberTestPlugin)), CancellationToken.None);

        messenger.Send(MinimizedToTrayMessage.Instance);

        Assert.Equal(1, ReadPluginStaticInt(healthy, "TrayEventSubscriberTestPlugin", "MinimizedCount"));
    }

    private PluginLoadRequest CreateRequest(string pluginId, Type entryType)
        => PluginTestPackage.CreateLoadRequest(
            _pluginsRoot,
            pluginId,
            entryType,
            entryTypeName: null,
            capabilitiesJson: null,
            priority: 0);

    /// <summary>在独立方法内构造并登记事件出口，令局部强引用随方法返回失效（Debug JIT 保活下仍可回收）。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterTransientSink(PluginEventPump pump)
    {
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(new CapabilityRegistry());
        pump.Register(scope);
        return new WeakReference(scope);
    }

    private static int ReadPluginStaticInt(
        PluginLoadResult result,
        string typeName,
        string fieldName)
    {
        Assembly entryAssembly = result.LoadContext!.Assemblies
            .Single(assembly => assembly.GetName().Name == "StarPie.Tests");
        Type entryType = entryAssembly.GetType($"StarPie.Tests.{typeName}", throwOnError: true)!;
        FieldInfo counter = entryType.GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Static)!;
        return (int)counter.GetValue(null)!;
    }
}
