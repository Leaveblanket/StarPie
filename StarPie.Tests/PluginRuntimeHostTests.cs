using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public void 随包插件包_不含宿主与SDK程序集副本()
    {
        // 包内容判据（plugins.md §5.1 约束 3）：共享契约与宿主实现绝不随包分发，
        // 命中即被发现层拒绝——这里在构建产物上直接断言，避免"整目录复制"式回归。
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

    /// <summary>内置来源替身：只提供一条固定候选，用来观察聚合结果随插件生命周期变化。</summary>
    private sealed class BuiltInStubScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => new[] { new ProgramEntry("记事本 (Notepad)", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe") };
    }
}
