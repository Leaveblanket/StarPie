using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.Manifest;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.Registry;
using StarPie.PluginRuntime.Unloading;
using StarPie.Services.Programs;

namespace StarPie.Tests;

/// <summary>
/// 安全点卸载缝：无在途调用 → 配置落盘 → 能力摘除 → StopAsync → 作用域释放 → ALC.Unload，
/// 并以 WeakReference 判定 headless 插件对象、ALC 与程序集回收；失败路径进隔离而非静默成功。
/// </summary>
public sealed class PluginUnloadPipelineTests : IDisposable
{
    private const string PluginId = "com.example.unload";

    private static IPlugin? _leakHolder;

    private static Type? _leakTypeHolder;

    private readonly string _tempRoot;
    private readonly string _pluginsRoot;

    public PluginUnloadPipelineTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-unload-tests").FullName;
        _pluginsRoot = Path.Combine(_tempRoot, "plugins");
        Directory.CreateDirectory(_pluginsRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task 装载运行卸载_插件对象与ALC与程序集均回收()
    {
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginUnloadRequest request = await LoadFixtureAsync(
            PluginId,
            typeof(ProgramSourceTestPlugin),
            registry,
            PluginTestPackage.ProgramSourceCapabilitiesJson);
        int flushCalls = 0;
        var pipeline = new PluginUnloadPipeline(() => flushCalls++);

        // 运行：能力可被消费者取用并调用。
        IProgramScanner scanner = Assert.Single(registry.GetAll<IProgramScanner>());
        Assert.Equal(PluginId, Assert.Single(scanner.ScanInstalledPrograms()).Name);

        PluginUnloadResult result = await pipeline.UnloadAsync(request, CancellationToken.None);

        Assert.True(
            result.Status == PluginUnloadStatus.Unloaded,
            $"诊断：{string.Join(" | ", result.Diagnostics)}；原因：{result.FailureReason}");
        Assert.Null(result.FailureReason);
        Assert.Equal(PluginId, result.PluginId);
        Assert.Equal(1, flushCalls);
        Assert.Equal(PluginLifecycleState.Unloaded, result.Lifecycle.Current);
        Assert.Empty(registry.GetAll<IProgramScanner>());
        Assert.Contains(result.Diagnostics, line => line.Contains("插件对象") && line.Contains("已回收"));
        Assert.Contains(result.Diagnostics, line => line.Contains("ALC") && line.Contains("已回收"));
        Assert.Contains(result.Diagnostics, line => line.Contains("程序集") && line.Contains("已回收"));
        Assert.Contains(result.Diagnostics, line => line.Contains("作用域") && line.Contains("账本 0"));
        Assert.DoesNotContain(
            AssemblyLoadContext.All,
            context => context.Name == $"StarPie.Plugin.{PluginId}");
    }

    [Fact]
    public async Task 重复装载卸载_多轮无累积泄漏()
    {
        var pipeline = new PluginUnloadPipeline(() => { });

        // 同一个 plugin id 反复装载卸载：每轮断言的 ALC 名字相同，前一轮残留不会被换名掩盖。
        for (int round = 0; round < 3; round++)
        {
            CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
            PluginUnloadRequest request = await LoadFixtureAsync(
                PluginId,
                typeof(ProgramSourceTestPlugin),
                registry,
                PluginTestPackage.ProgramSourceCapabilitiesJson);
            Assert.Single(registry.GetAll<IProgramScanner>()).ScanInstalledPrograms();

            PluginUnloadResult result = await pipeline.UnloadAsync(request, CancellationToken.None);

            Assert.True(
                result.Status == PluginUnloadStatus.Unloaded,
                $"第 {round} 轮：{string.Join(" | ", result.Diagnostics)}");
            Assert.Contains(result.Diagnostics, line => line.Contains("程序集") && line.Contains("已回收"));
            Assert.DoesNotContain(
                AssemblyLoadContext.All,
                context => context.Name == $"StarPie.Plugin.{PluginId}");
        }
    }

    [Fact]
    public async Task 已隔离插件_回收资源但不谎报已卸载()
    {
        var registry = new CapabilityRegistry();
        PluginLoadResult loaded = await LoadRawAsync(PluginId, typeof(RecordingTestPlugin), registry);
        const string QuarantineReason = "调用守卫熔断：能力调用连续失败";
        loaded.Lifecycle.Quarantine(QuarantineReason);
        PluginUnloadRequest request = PluginUnloadRequest.FromLoaded(loaded);

        PluginUnloadResult result = await new PluginUnloadPipeline(() => { })
            .UnloadAsync(request, CancellationToken.None);

        // 隔离结论不变，也不谎报已卸载；作用域与 ALC 不再滞留到重启。
        Assert.Equal(PluginUnloadStatus.Quarantined, result.Status);
        Assert.Contains(QuarantineReason, result.FailureReason);
        Assert.Equal(QuarantineReason, result.Lifecycle.QuarantineReason);
        Assert.Contains(result.Diagnostics, line => line.Contains("作用域") && line.Contains("账本 0"));
        Assert.Contains(result.Diagnostics, line => line.Contains("ALC") && line.Contains("已回收"));
        Assert.DoesNotContain(
            AssemblyLoadContext.All,
            context => context.Name == $"StarPie.Plugin.{PluginId}");
    }

    [Fact]
    public async Task 卸载顺序_配置落盘先于停用()
    {
        // 每次运行唯一 id：标记文件落 %TEMP%，避免与上一次运行的残留互相干扰。
        string pluginId = $"com.example.unload.marker.{Guid.NewGuid():N}";
        string stopMarker = StopMarkerPath(pluginId);
        var registry = new CapabilityRegistry();
        PluginUnloadRequest request = await LoadFixtureAsync(
            pluginId,
            typeof(MarkerStopTestPlugin),
            registry,
            capabilitiesJson: null);
        bool flushObservedBeforeStop = false;
        var pipeline = new PluginUnloadPipeline(() => flushObservedBeforeStop = !File.Exists(stopMarker));

        PluginUnloadResult result = await pipeline.UnloadAsync(request, CancellationToken.None);

        Assert.Equal(PluginUnloadStatus.Unloaded, result.Status);
        Assert.True(flushObservedBeforeStop, "配置落盘必须在 StopAsync 之前完成");
        Assert.True(File.Exists(stopMarker), "StopAsync 应已被调用");
        File.Delete(stopMarker);
    }

    [Fact]
    public async Task 卸载中途被并发熔断隔离_不抛异常且仍完成回收()
    {
        var registry = new CapabilityRegistry();
        PluginLoadResult loaded = await LoadRawAsync(PluginId, typeof(RecordingTestPlugin), registry);
        PluginUnloadRequest request = PluginUnloadRequest.FromLoaded(loaded);
        const string QuarantineReason = "并发熔断：能力调用连续失败";
        var pipeline = new PluginUnloadPipeline(() => loaded.Lifecycle.Quarantine(QuarantineReason));

        // 状态机在卸载第一步（配置落盘）中途被隔离：后续转移全部失效，但回收照走。
        PluginUnloadResult result = await pipeline.UnloadAsync(request, CancellationToken.None);

        Assert.Equal(PluginUnloadStatus.Quarantined, result.Status);
        Assert.Contains(QuarantineReason, result.FailureReason);
        Assert.Equal(PluginLifecycleState.Quarantined, result.Lifecycle.Current);
        Assert.Contains(result.Diagnostics, line => line.Contains("跳过转移"));
        Assert.Contains(result.Diagnostics, line => line.Contains("作用域") && line.Contains("账本 0"));
        Assert.Contains(result.Diagnostics, line => line.Contains("ALC") && line.Contains("已回收"));
        Assert.DoesNotContain(
            AssemblyLoadContext.All,
            context => context.Name == $"StarPie.Plugin.{PluginId}");
    }

    [Fact]
    public async Task 交接_装载结果不再持有插件对象()
    {
        var registry = new CapabilityRegistry();
        PluginLoadResult loaded = await LoadRawAsync(PluginId, typeof(RecordingTestPlugin), registry);
        AssertLoadResultHoldsPlugin(loaded);

        PluginUnloadRequest request = PluginUnloadRequest.FromLoaded(loaded);

        // 交接是机械的：插件对象的所有权只剩卸载请求这一条路径。
        Assert.Null(loaded.Plugin);
        Assert.Null(loaded.LoadContext);
        Assert.Null(loaded.Scope);
        Assert.Equal(PluginLifecycleState.Active, loaded.Lifecycle.Current);

        PluginUnloadResult result = await new PluginUnloadPipeline(() => { })
            .UnloadAsync(request, CancellationToken.None);

        Assert.Equal(PluginUnloadStatus.Unloaded, result.Status);
        Assert.DoesNotContain(
            AssemblyLoadContext.All,
            context => context.Name == $"StarPie.Plugin.{PluginId}");
    }

    [Fact]
    public async Task 在途调用未归零_隔离且不释放作用域()
    {
        PluginLifecycleStateMachine lifecycle = PluginCapabilityTestDoubles.Lifecycle(
            PluginLifecycleState.Active);
        var registry = new CapabilityRegistry();
        registry.DeclareContract(PluginCapabilityTestDoubles.ProbeContract);
        PluginServiceScope scope = PluginCapabilityTestDoubles.CreateScope(
            registry,
            pluginId: PluginId,
            lifecycle: lifecycle);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Context.RegisterCapability<IGuardProbeCapability>(new ProbeCapability
        {
            EchoImpl = _ =>
            {
                entered.TrySetResult(true);
                return gate.Task.GetAwaiter().GetResult();
            },
        });
        var alc = new PluginLoadContext(PluginId, typeof(RecordingTestPlugin).Assembly.Location);
        var loaded = new PluginLoadResult(
            PluginId,
            PluginLoadStatus.Active,
            null,
            new RecordingTestPlugin(),
            alc,
            lifecycle,
            scope);
        PluginUnloadRequest request = PluginUnloadRequest.FromLoaded(loaded);
        IGuardProbeCapability capability = Assert.Single(registry.GetAll<IGuardProbeCapability>());
        Task<string> inFlight = Task.Run(() => capability.Echo("blocked"));
        await entered.Task;
        Assert.Equal(1, scope.Guard.InFlightCount);

        PluginUnloadResult result = await new PluginUnloadPipeline(
            () => { },
            drainTimeout: TimeSpan.FromMilliseconds(80))
            .UnloadAsync(request, CancellationToken.None);

        Assert.Equal(PluginUnloadStatus.Quarantined, result.Status);
        Assert.False(result.Reclaimed);
        Assert.Contains("在途", result.FailureReason);
        Assert.Equal(PluginLifecycleState.Quarantined, lifecycle.Current);
        Assert.Contains(result.Diagnostics, line => line.Contains("未归零"));
        Assert.Contains(result.Residuals, item =>
            item.Kind == PluginResidualKind.InFlightCall && item.Detail.Contains("在途"));

        // 未归零即不进入危险区：作用域未释放、能力条目已摘除（不再对外可见）。
        Assert.False(scope.IsDisposed);
        Assert.Empty(registry.GetAll<IGuardProbeCapability>());

        gate.SetResult("released");
        Assert.Equal("released", await inFlight);
        Assert.True(await scope.Guard.WaitForInFlightAsync(
            TimeSpan.FromSeconds(5),
            CancellationToken.None));
    }

    [Fact]
    public async Task 回收判定失败_残留清单可定位到类型与程序集()
    {
        string pluginId = $"com.example.leak.{Guid.NewGuid():N}";
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginLoadResult loaded = await LoadRawAsync(
            pluginId,
            typeof(ProgramSourceTestPlugin),
            registry,
            PluginTestPackage.ProgramSourceCapabilitiesJson);
        _leakHolder = loaded.Plugin;

        try
        {
            PluginUnloadRequest request = PluginUnloadRequest.FromLoaded(loaded);

            PluginUnloadResult result = await new PluginUnloadPipeline(() => { })
                .UnloadAsync(request, CancellationToken.None);

            // 泄漏现场必须可定位：类别 + 具体类型/程序集全名，而不是一句"泄漏了"。
            Assert.Equal(PluginUnloadStatus.Quarantined, result.Status);
            Assert.False(result.Reclaimed);
            Assert.Contains(result.Residuals, item =>
                item.Kind == PluginResidualKind.PluginObject
                && item.Detail.Contains("ProgramSourceTestPlugin"));
            Assert.Contains(result.Residuals, item => item.Kind == PluginResidualKind.LoadContext);
            Assert.Contains(result.Residuals, item =>
                item.Kind == PluginResidualKind.Assembly && item.Detail.Contains("StarPie.Tests"));
            Assert.Contains(result.Residuals, item =>
                item.Kind == PluginResidualKind.Type
                && item.Detail.Contains("ProgramSourceTestPlugin"));
        }
        finally
        {
            // 泄漏是刻意构造的：收尾清引用并回收，避免影响同进程后续用例的 ALC 断言。
            _leakHolder = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    [Fact]
    public async Task 降级判定_程序集残留只记诊断不隔离()
    {
        string pluginId = $"com.example.leak.downgrade.{Guid.NewGuid():N}";
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginLoadResult loaded = await LoadRawAsync(
            pluginId,
            typeof(ProgramSourceTestPlugin),
            registry,
            PluginTestPackage.ProgramSourceCapabilitiesJson);
        // 只钉住插件程序集里的类型：入口实例可回收，ALC 与程序集不能——这是 WPF 宿主框架
        // 缓存程序集的等价现场（宿主经 AppDomain 程序集加载事件强引用插件程序集）。
        // 取类型走独立帧：调用帧残留的插件实例栈槽会 root 入口实例，判定会误报。
        _leakTypeHolder = CapturePluginType(loaded);
        PluginUnloadRequest request = PluginUnloadRequest.FromLoaded(loaded);

        try
        {
            PluginUnloadResult result = await new PluginUnloadPipeline(
                () => { },
                reclaimPolicy: PluginReclaimPolicy.Diagnostic)
                .UnloadAsync(request, CancellationToken.None);

            // 宿主降级：ALC 与程序集残留不阻断停用；但插件自有对象（入口实例）仍硬判。
            Assert.True(
                result.Status == PluginUnloadStatus.Unloaded,
                $"诊断：{string.Join(" | ", result.Diagnostics)}；原因：{result.FailureReason}");
            Assert.Null(result.FailureReason);
            Assert.True(result.Reclaimed, $"降级判定应视同请求耗尽：{result.FailureReason}");
            Assert.Contains(result.Diagnostics, line => line.Contains("插件对象") && line.Contains("已回收"));
            Assert.Contains(result.Diagnostics, line => line.Contains("宿主降级") && line.Contains("重启"));
            Assert.DoesNotContain(result.Residuals, item => item.Kind == PluginResidualKind.PluginObject);
            Assert.Contains(result.Residuals, item => item.Kind == PluginResidualKind.LoadContext);
            Assert.Contains(result.Residuals, item => item.Kind == PluginResidualKind.Assembly);
        }
        finally
        {
            // 刻意构造的程序集残留：收尾清引用并回收，避免影响同进程后续用例的 ALC 断言。
            _leakTypeHolder = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    [Fact]
    public async Task 硬判判定_程序集残留仍隔离()
    {
        string pluginId = $"com.example.leak.hard.{Guid.NewGuid():N}";
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginLoadResult loaded = await LoadRawAsync(
            pluginId,
            typeof(ProgramSourceTestPlugin),
            registry,
            PluginTestPackage.ProgramSourceCapabilitiesJson);
        _leakTypeHolder = CapturePluginType(loaded);
        PluginUnloadRequest request = PluginUnloadRequest.FromLoaded(loaded);

        try
        {
            // 同一现场在硬判档（缺省）下必须隔离：判据随宿主环境分级，不随现场放宽。
            PluginUnloadResult result = await new PluginUnloadPipeline(() => { })
                .UnloadAsync(request, CancellationToken.None);

            Assert.Equal(PluginUnloadStatus.Quarantined, result.Status);
            Assert.False(result.Reclaimed);
            Assert.Contains("回收判定未通过", result.FailureReason);
            Assert.Contains(result.Residuals, item => item.Kind == PluginResidualKind.LoadContext);
        }
        finally
        {
            _leakTypeHolder = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    [Fact]
    public async Task 停用抛异常_隔离但仍释放作用域并回收ALC()
    {
        var registry = new CapabilityRegistry();
        PluginUnloadRequest request = await LoadFixtureAsync(
            PluginId,
            typeof(ThrowingStopTestPlugin),
            registry,
            PluginTestPackage.ProgramSourceCapabilitiesJson);
        var logSink = new RecordingPluginLogSink();

        PluginUnloadResult result = await new PluginUnloadPipeline(() => { }, logSink: logSink)
            .UnloadAsync(request, CancellationToken.None);

        Assert.Equal(PluginUnloadStatus.Quarantined, result.Status);
        Assert.Contains("夹具停止爆炸", result.FailureReason);
        Assert.Equal(PluginLifecycleState.Quarantined, result.Lifecycle.Current);
        // 失败进日志：插件异常经 DTO 三段字符串入 sink，异常实例不随结果带出。
        Assert.Contains(
            logSink.Entries,
            entry => entry.PluginId == PluginId
                && entry.ExceptionType == typeof(InvalidOperationException).FullName
                && entry.ExceptionMessage == "夹具停止爆炸");
        Assert.Contains(
            logSink.Entries,
            entry => entry.ExceptionType is null && entry.Message.Contains("卸载未完成"));
        // 失败不掩盖清理：作用域与 ALC 仍走完回收。
        Assert.Contains(result.Diagnostics, line => line.Contains("作用域") && line.Contains("账本 0"));
        Assert.Contains(result.Diagnostics, line => line.Contains("ALC") && line.Contains("已回收"));
        Assert.DoesNotContain(
            AssemblyLoadContext.All,
            context => context.Name == $"StarPie.Plugin.{PluginId}");
    }

    [Fact]
    public async Task 配置落盘失败_隔离而非静默成功()
    {
        var registry = new CapabilityRegistry();
        PluginUnloadRequest request = await LoadFixtureAsync(
            PluginId,
            typeof(RecordingTestPlugin),
            registry,
            capabilitiesJson: null);

        PluginUnloadResult result = await new PluginUnloadPipeline(
            () => throw new InvalidOperationException("落盘爆炸"))
            .UnloadAsync(request, CancellationToken.None);

        Assert.Equal(PluginUnloadStatus.Quarantined, result.Status);
        Assert.Contains("落盘爆炸", result.FailureReason);
        Assert.Contains(result.Diagnostics, line => line.Contains("配置落盘") && line.Contains("失败"));
    }

    /// <summary>夹具 StopAsync 落点（插件 ALC 内的实现写同路径文件，用于跨 ALC 观察顺序）。</summary>
    internal static string StopMarkerPath(string pluginId)
        => Path.Combine(Path.GetTempPath(), $"starpie-stop-{pluginId}.marker");

    /// <summary>独立帧取插件 ALC 内的类型：调用帧不留插件实例栈槽，入口实例仍可回收。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type CapturePluginType(PluginLoadResult loaded) => loaded.Plugin!.GetType();

    /// <summary>夹具装载缝：装载结果只在本帧存在（需要断言交接契约的用例直接取用）。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<PluginLoadResult> LoadRawAsync(
        string pluginId,
        Type entryType,
        CapabilityRegistry registry,
        string? capabilitiesJson = null)
    {
        PluginLoadResult loaded = await new PluginLoadPipeline(registry)
            .LoadAsync(
                PluginTestPackage.CreateLoadRequest(
                    _pluginsRoot,
                    pluginId,
                    entryType,
                    capabilitiesJson: capabilitiesJson),
                CancellationToken.None);
        Assert.Equal(PluginLoadStatus.Active, loaded.Status);
        return loaded;
    }

    /// <summary>夹具装载缝：交出的只有卸载请求，调用方不再持有装载结果。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<PluginUnloadRequest> LoadFixtureAsync(
        string pluginId,
        Type entryType,
        CapabilityRegistry registry,
        string? capabilitiesJson)
    {
        PluginLoadResult loaded = await LoadRawAsync(pluginId, entryType, registry, capabilitiesJson);
        return PluginUnloadRequest.FromLoaded(loaded);
    }

    /// <summary>交接前的持有断言（独立帧，避免调用帧的栈槽在回收判定时仍 root 插件对象）。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertLoadResultHoldsPlugin(PluginLoadResult loaded)
    {
        Assert.NotNull(loaded.Plugin);
        Assert.NotNull(loaded.LoadContext);
        Assert.NotNull(loaded.Scope);
    }
}
