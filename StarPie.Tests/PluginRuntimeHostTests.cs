using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StarPie.PluginRuntime;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Discovery;
using StarPie.PluginRuntime.Hosting;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Registry;
using StarPie.PluginRuntime.State;
using StarPie.PluginRuntime.Unloading;
using StarPie.Programs;
using StarPie.Services.Programs;

namespace StarPie.Tests;

/// <summary>
/// 随包 headless 插件的完整生命周期：按生产路径装载
/// <c>plugins/&lt;id&gt;/</c> 下的真实插件包（发现 → 内置准入 → ALC 装载 → 能力进能力表），
/// 停用走安全点卸载并落「停用」意图，再启用回到活动态；停用态的程序来源只剩内置来源。
/// </summary>
public sealed class PluginRuntimeHostTests : IDisposable
{
    private const string PluginId = "starpie.builtin.program-source";

    private static System.Reflection.Assembly? _cachedPluginAssembly;

    private readonly string _tempRoot;
    private readonly PluginStateStore _stateStore;
    private readonly CapabilityRegistry _registry = new();
    private readonly PluginRuntimeHost _host;

    public PluginRuntimeHostTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-builtin-plugin-tests").FullName;
        _stateStore = new PluginStateStore(Path.Combine(_tempRoot, "plugin-state.json"));
        _registry.DeclareBuiltin(ProgramSourceCapability.Contract, new BuiltInStubScanner());
        _host = new PluginRuntimeHost(
            CreateScanner(),
            _stateStore,
            new PluginLoadPipeline(_registry),
            new PluginUnloadPipeline(() => { }));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task 内置插件_装载停用再启用_能力随生命周期进出能力表()
    {
        await _host.StartAsync(CancellationToken.None);

        // 装载：内置准入 + 活动态；能力表里内置来源在前、插件来源在后。
        PluginStartupReportEntry entry = Assert.Single(
            _host.Report!.Plugins,
            item => item.PluginId == PluginId);
        Assert.Equal(PluginAdmission.BuiltIn, entry.Admission);
        Assert.True(entry.Enabled);
        Assert.Equal(new[] { PluginId }, _host.ActivePluginIds);
        Assert.Equal(2, _registry.GetAll<IProgramScanner>().Count);

        // 插件来源真跑得起来：跨 ALC 调用返回候选列表（不吞异常、不空手而归）。
        Assert.NotNull(_registry.GetAll<IProgramScanner>()[1].ScanInstalledPrograms());

        // 停用：安全点卸载 + 「停用」意图落盘；插件来源退出能力表，只剩内置来源。
        PluginUnloadResult unload = Assert.IsType<PluginUnloadResult>(
            await _host.DisableAsync(PluginId, CancellationToken.None));
        Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
        Assert.Empty(_host.ActivePluginIds);
        Assert.Single(_registry.GetAll<IProgramScanner>());
        Assert.False(_stateStore.Current.Plugins[PluginId].Enabled);

        // 再启用：装载回到活动态，能力重新可见。
        PluginLoadResult reload = await _host.EnableAsync(PluginId, CancellationToken.None);
        Assert.Equal(PluginLoadStatus.Active, reload.Status);
        Assert.Equal(new[] { PluginId }, _host.ActivePluginIds);
        Assert.Equal(2, _registry.GetAll<IProgramScanner>().Count);
        Assert.True(_stateStore.Current.Plugins[PluginId].Enabled);
    }

    [Fact]
    public async Task 状态为停用_启动不装载_程序来源只剩内置()
    {
        await File.WriteAllTextAsync(
            _stateStore.StatePath,
            JsonSerializer.Serialize(new PluginStateDocument
            {
                DeveloperModeEnabled = false,
                Plugins = new Dictionary<string, PluginStateEntry>(StringComparer.Ordinal)
                {
                    [PluginId] = new() { Enabled = false },
                },
            }),
            TestContext.Current.CancellationToken);

        await _host.StartAsync(CancellationToken.None);

        Assert.Empty(_host.ActivePluginIds);
        Assert.Single(_registry.GetAll<IProgramScanner>());
        Assert.False(Assert.Single(_host.Report!.Plugins, item => item.PluginId == PluginId).Enabled);
    }

    [Fact]
    public async Task 已隔离状态_启动不装载_并保留隔离原因()
    {
        _stateStore.Current.GetOrCreate(PluginId).Quarantine =
            new PluginQuarantineState("夹具隔离：装载失败", DateTimeOffset.Now);
        _stateStore.Save();

        await _host.StartAsync(CancellationToken.None);

        Assert.Empty(_host.ActivePluginIds);
        Assert.Single(_registry.GetAll<IProgramScanner>());
        Assert.NotNull(_stateStore.Current.Plugins[PluginId].Quarantine);
    }

    [Fact]
    public async Task 隔离插件_显式重试_清隔离并重新装载()
    {
        _stateStore.Current.GetOrCreate(PluginId).Quarantine =
            new PluginQuarantineState("夹具隔离：装载失败", DateTimeOffset.Now);
        _stateStore.Save();
        await _host.StartAsync(CancellationToken.None);
        Assert.Empty(_host.ActivePluginIds);

        PluginLoadResult retry = await _host.RetryAsync(PluginId, CancellationToken.None);

        // 重试是隔离的唯一自动出口：清隔离、按启用意图装载，回到活动态。
        Assert.Equal(PluginLoadStatus.Active, retry.Status);
        Assert.Equal(new[] { PluginId }, _host.ActivePluginIds);
        Assert.Null(_stateStore.Current.Plugins[PluginId].Quarantine);
        Assert.True(_stateStore.Current.Plugins[PluginId].Enabled);
    }

    [Fact]
    public async Task 重试仍失败_保持隔离_修复包后重试恢复()
    {
        // 独立宿主：临时目录里放一份入口程序集不可用的包，id 视为内置（绕过准入拒绝）。
        const string RetryPluginId = "com.example.retry";
        string packageRoot = Path.Combine(_tempRoot, "retry-plugins");
        PluginTestPackage.Create(packageRoot, RetryPluginId);
        var retryState = new PluginStateStore(Path.Combine(_tempRoot, "retry-state.json"));
        var retryRegistry = new CapabilityRegistry();
        retryRegistry.DeclareContract(ProgramSourceCapability.Contract);
        var retryHost = new PluginRuntimeHost(
            new PluginStartupScanner(
                new PluginDiscovery(packageRoot, Path.Combine(_tempRoot, "retry-user-plugins")),
                new PluginAdmissionPolicy(new[] { RetryPluginId }),
                retryState,
                new PluginStartupReportWriter(Path.Combine(_tempRoot, "retry-report.json"))),
            retryState,
            new PluginLoadPipeline(retryRegistry),
            new PluginUnloadPipeline(() => { }));

        await retryHost.StartAsync(CancellationToken.None);
        Assert.NotNull(retryState.Current.Plugins[RetryPluginId].Quarantine);

        PluginLoadResult stillBroken = await retryHost.RetryAsync(RetryPluginId, CancellationToken.None);

        // 重试仍失败：保持隔离（写新原因），不谎报装载。
        Assert.Equal(PluginLoadStatus.Quarantined, stillBroken.Status);
        Assert.NotNull(retryState.Current.Plugins[RetryPluginId].Quarantine);
        Assert.Empty(retryHost.ActivePluginIds);

        // 修复包（换成真实入口程序集）后重试：隔离被清除并装载成功。
        string entryAssemblyName = Path.GetFileName(typeof(ProgramSourceTestPlugin).Assembly.Location);
        File.Copy(
            typeof(ProgramSourceTestPlugin).Assembly.Location,
            Path.Combine(packageRoot, RetryPluginId, entryAssemblyName),
            overwrite: true);
        await File.WriteAllTextAsync(
            Path.Combine(packageRoot, RetryPluginId, "plugin.json"),
            PluginTestPackage.Manifest(
                RetryPluginId,
                entryAssembly: entryAssemblyName,
                entryType: typeof(ProgramSourceTestPlugin).FullName!,
                capabilitiesJson: PluginTestPackage.ProgramSourceCapabilitiesJson),
            TestContext.Current.CancellationToken);

        PluginLoadResult recovered = await retryHost.RetryAsync(RetryPluginId, CancellationToken.None);

        Assert.True(
            recovered.Status == PluginLoadStatus.Active,
            $"修复包后重试仍未恢复：{recovered.FailureReason}");
        Assert.Null(retryState.Current.Plugins[RetryPluginId].Quarantine);
        Assert.Equal(new[] { RetryPluginId }, retryHost.ActivePluginIds);
    }

    [Fact]
    public async Task 装载期隔离_停用会回收失败装载留下的资源()
    {
        const string RetryPluginId = "com.example.retry-disable";
        (PluginRuntimeHost host, PluginStateStore state) = CreateIsolatedPackageHost(RetryPluginId);
        await host.StartAsync(CancellationToken.None);
        Assert.NotNull(state.Current.Plugins[RetryPluginId].Quarantine);

        PluginUnloadResult? unload = await host.DisableAsync(RetryPluginId, CancellationToken.None);

        // 隔离结论不变，但失败装载留下的资源已收口，不把泄漏留到重启。
        Assert.IsType<PluginUnloadResult>(unload);
        Assert.True(unload.Reclaimed, $"回收未完成：{unload.FailureReason}");
        Assert.False(state.Current.Plugins[RetryPluginId].Enabled);
        Assert.NotNull(state.Current.Plugins[RetryPluginId].Quarantine);
        Assert.DoesNotContain(
            AssemblyLoadContext.All,
            context => context.Name == $"StarPie.Plugin.{RetryPluginId}");
    }

    [Fact]
    public async Task 装载期隔离_重试先回收旧ALC_不累积()
    {
        const string RetryPluginId = "com.example.retry-reclaim";
        (PluginRuntimeHost host, PluginStateStore _) = CreateIsolatedPackageHost(RetryPluginId);
        await host.StartAsync(CancellationToken.None);

        await host.RetryAsync(RetryPluginId, CancellationToken.None);

        // 每轮重试前先把上一轮失败的 ALC 收干净：名字相同的上下文不会在进程里越积越多。
        Assert.Single(
            AssemblyLoadContext.All,
            context => context.Name == $"StarPie.Plugin.{RetryPluginId}");
    }

    [Fact]
    public async Task 诊断报告_呈现隔离原因与残留清单()
    {
        _stateStore.Current.GetOrCreate(PluginId).Quarantine = new PluginQuarantineState(
            "夹具隔离：回收未过",
            DateTimeOffset.Now)
        {
            Residuals = new[]
            {
                new PluginResidual
                {
                    Kind = PluginResidualKind.PluginObject,
                    Detail = "入口实例 Fixture.Plugin",
                },
                new PluginResidual
                {
                    Kind = PluginResidualKind.Type,
                    Detail = "Fixture.dll：Fixture.Plugin",
                },
            },
        };
        _stateStore.Save();
        await _host.StartAsync(CancellationToken.None);

        PluginDiagnosticsReport report = _host.GetDiagnostics(PluginId);

        Assert.Equal(PluginRuntimeStatus.Quarantined, report.Status);
        Assert.Equal("夹具隔离：回收未过", report.QuarantineReason);
        Assert.Equal(2, report.Residuals.Count);
        Assert.Contains(report.Residuals, item => item.Detail.Contains("Fixture.Plugin"));
        Assert.Equal(
            PluginRuntimeStatus.Quarantined,
            _host.DescribePlugins().Single(item => item.PluginId == PluginId).Status);
    }

    [Fact]
    public async Task 诊断报告_状态判定_活动与停用()
    {
        await _host.StartAsync(CancellationToken.None);
        Assert.Equal(PluginRuntimeStatus.Active, _host.GetDiagnostics(PluginId).Status);

        await _host.DisableAsync(PluginId, CancellationToken.None);

        PluginDiagnosticsReport report = _host.GetDiagnostics(PluginId);
        Assert.Equal(PluginRuntimeStatus.Disabled, report.Status);
        Assert.False(report.Enabled);
    }

    [Fact]
    public async Task 经消费者聚合器扫描后_停用仍能回收插件程序集()
    {
        await _host.StartAsync(CancellationToken.None);

        // 走 UI 的消费路径：经能力表取插件来源并真实扫描一次（守卫适配器 + 返回 DTO）。
        var aggregator = new ProgramSourceAggregator(_registry);
        Assert.NotEmpty(aggregator.ScanInstalledPrograms());

        PluginUnloadResult unload = Assert.IsType<PluginUnloadResult>(
            await _host.DisableAsync(PluginId, CancellationToken.None));

        Assert.True(unload.Reclaimed, $"卸载未回收：{unload.FailureReason}");
        Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
    }

    [Fact]
    public async Task 降级判定宿主_停用为已停用_程序集残留进诊断且可再启用()
    {
        var state = new PluginStateStore(Path.Combine(_tempRoot, "diagnostic-state.json"));
        var host = new PluginRuntimeHost(
            new PluginStartupScanner(
                new PluginDiscovery(PluginPaths.InstallDirectory, Path.Combine(_tempRoot, "diagnostic-user")),
                new PluginAdmissionPolicy(PluginAdmissionPolicy.DefaultBuiltInPluginIds),
                state,
                new PluginStartupReportWriter(Path.Combine(_tempRoot, "diagnostic-report.json"))),
            state,
            new PluginLoadPipeline(_registry),
            new PluginUnloadPipeline(() => { }, reclaimPolicy: PluginReclaimPolicy.Diagnostic));
        await host.StartAsync(CancellationToken.None);

        // 等价现场：宿主框架缓存插件 ALC 里的入口程序集（WPF 的 BAML 架构上下文即如此），
        // 插件入口实例本身仍可回收。
        AssemblyLoadContext context = Assert.Single(
            AssemblyLoadContext.All,
            item => string.Equals(item.Name, $"StarPie.Plugin.{PluginId}", StringComparison.Ordinal));
        _cachedPluginAssembly = context.Assemblies.First();

        try
        {
            PluginUnloadResult unload = Assert.IsType<PluginUnloadResult>(
                await host.DisableAsync(PluginId, CancellationToken.None));

            // 降级判定：停用如实生效（不是隔离），程序集残留只进诊断，供管理面展示、重启后释放。
            Assert.True(
                unload.Status == PluginUnloadStatus.Unloaded,
                $"诊断：{string.Join(" | ", unload.Diagnostics)}；原因：{unload.FailureReason}");
            Assert.True(unload.Reclaimed, $"降级判定应视同请求耗尽：{unload.FailureReason}");
            Assert.Contains(unload.Diagnostics, line => line.Contains("宿主降级"));
            Assert.Contains(unload.Residuals, item => item.Kind == PluginResidualKind.LoadContext);

            PluginDiagnosticsReport report = host.GetDiagnostics(PluginId);
            Assert.Equal(PluginRuntimeStatus.Disabled, report.Status);
            Assert.Contains(report.Residuals, item => item.Kind == PluginResidualKind.LoadContext);
            Assert.NotNull(report.ReclaimNote);
            Assert.Contains("重启宿主后释放", report.ReclaimNote);

            // 停用不是终态：可再次启用，旧 ALC 的释放留给重启。
            PluginLoadResult reenabled = await host.EnableAsync(PluginId, CancellationToken.None);
            Assert.Equal(PluginLoadStatus.Active, reenabled.Status);
            Assert.Empty(host.GetDiagnostics(PluginId).Residuals);
        }
        finally
        {
            _cachedPluginAssembly = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    [Fact]
    public void 随包插件包_不含宿主与SDK程序集副本()
    {
        // 共享契约与宿主实现绝不随包分发，命中即被发现层拒绝——
        // 这里在构建产物上直接断言，避免"整目录复制"式回归。
        string packageDirectory = Path.Combine(PluginPaths.InstallDirectory, PluginId);
        string[] packagedAssemblies = Directory.GetFiles(packageDirectory, "*.dll", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;

        Assert.Equal(new[] { "StarPie.Plugin.Programs.dll" }, packagedAssemblies);
        Assert.True(File.Exists(Path.Combine(packageDirectory, "plugin.json")), "插件包必须带 plugin.json");
    }

    [Fact]
    public async Task 停用后重启_停用意图保留_不再自动装载()
    {
        await _host.StartAsync(CancellationToken.None);
        await _host.DisableAsync(PluginId, CancellationToken.None);

        // 第二次启动扫描：准入结果重算，但用户的停用意图不被扫描覆盖。
        await _host.StartAsync(CancellationToken.None);

        Assert.Empty(_host.ActivePluginIds);
        Assert.False(_stateStore.Current.Plugins[PluginId].Enabled);
    }

    /// <summary>按生产路径构造启动扫描器：随包插件目录 = 测试输出目录旁 <c>plugins/</c>。</summary>
    private PluginStartupScanner CreateScanner()
        => new(
            new PluginDiscovery(PluginPaths.InstallDirectory, Path.Combine(_tempRoot, "user-plugins")),
            new PluginAdmissionPolicy(PluginAdmissionPolicy.DefaultBuiltInPluginIds),
            _stateStore,
            new PluginStartupReportWriter(Path.Combine(_tempRoot, "plugin-startup-report.json")));

    /// <summary>构造独立宿主：临时目录里放入口程序集不可用的包，id 视为内置（装载必然隔离）。</summary>
    private (PluginRuntimeHost Host, PluginStateStore State) CreateIsolatedPackageHost(string pluginId)
    {
        string packageRoot = Path.Combine(_tempRoot, "isolated-" + pluginId);
        PluginTestPackage.Create(packageRoot, pluginId);
        var state = new PluginStateStore(Path.Combine(_tempRoot, pluginId + "-state.json"));
        var host = new PluginRuntimeHost(
            new PluginStartupScanner(
                new PluginDiscovery(packageRoot, Path.Combine(_tempRoot, pluginId + "-user")),
                new PluginAdmissionPolicy(new[] { pluginId }),
                state,
                new PluginStartupReportWriter(Path.Combine(_tempRoot, pluginId + "-report.json"))),
            state,
            new PluginLoadPipeline(new CapabilityRegistry()),
            new PluginUnloadPipeline(() => { }));
        return (host, state);
    }

    /// <summary>内置来源替身：只提供一条固定候选，用来观察聚合结果随插件生命周期变化。</summary>
    private sealed class BuiltInStubScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => new[] { new ProgramEntry("记事本 (Notepad)", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe") };
    }
}
