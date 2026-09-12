using System;
using System.Collections.Generic;
using System.Linq;
using StarPie.HostServices;
using StarPie.Manifest;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Registry;
using StarPie.Programs;
using StarPie.Services.Programs;

namespace StarPie.Tests;

/// <summary>
/// 程序来源聚合与扩展点降级：内置来源 + 插件来源按能力表顺序合并，插件缺席 / 停用 / 失败时
/// 只剩内置来源，选择器不会空转（扩展点的降级行为）。
/// </summary>
public sealed class ProgramSourceAggregatorTests
{
    private const string PluginId = "com.example.programs";

    [Fact]
    public void 聚合_内置在前_插件条目在后_同路径去重()
    {
        var registry = new CapabilityRegistry();
        var builtIn = new StubProgramScanner(new ProgramEntry("记事本 (Notepad)", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe"));
        registry.DeclareBuiltin(ProgramSourceCapability.Contract, builtIn);
        using PluginServiceScope scope = CreatePluginScope(
            registry,
            new StubProgramScanner(
                new ProgramEntry("notepad", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe"),
                new ProgramEntry("示例程序", @"C:\Apps\sample.exe", @"C:\Apps\sample.exe")));

        IReadOnlyList<ProgramEntry> programs = new ProgramSourceAggregator(registry).ScanInstalledPrograms();

        Assert.Equal(2, programs.Count);
        // 去重保留先出现的显示名（内置条目在前，插件的裸名不覆盖它）。
        Assert.Equal("记事本 (Notepad)", programs.Single(entry => entry.Path == @"C:\Windows\notepad.exe").Name);
        Assert.Contains(programs, entry => entry.Path == @"C:\Apps\sample.exe");
    }

    [Fact]
    public void 插件停用后_聚合只剩内置来源()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareBuiltin(
            ProgramSourceCapability.Contract,
            new StubProgramScanner(new ProgramEntry("记事本 (Notepad)", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe")));
        PluginServiceScope scope = CreatePluginScope(
            registry,
            new StubProgramScanner(new ProgramEntry("示例程序", @"C:\Apps\sample.exe", @"C:\Apps\sample.exe")));
        var aggregator = new ProgramSourceAggregator(registry);
        Assert.Equal(2, aggregator.ScanInstalledPrograms().Count);

        scope.Dispose();

        ProgramEntry only = Assert.Single(aggregator.ScanInstalledPrograms());
        Assert.Equal(@"C:\Windows\notepad.exe", only.Path);
    }

    [Fact]
    public void 插件来源失败_只跳过该来源_内置条目仍在()
    {
        var registry = new CapabilityRegistry();
        registry.DeclareBuiltin(
            ProgramSourceCapability.Contract,
            new StubProgramScanner(new ProgramEntry("记事本 (Notepad)", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe")));
        using PluginServiceScope scope = CreatePluginScope(
            registry,
            new StubProgramScanner(new InvalidOperationException("夹具扫描爆炸")));

        ProgramEntry only = Assert.Single(new ProgramSourceAggregator(registry).ScanInstalledPrograms());

        Assert.Equal("记事本 (Notepad)", only.Name);
    }

    /// <summary>构造活动态插件作用域并注册程序来源能力（清单声明与契约 id/ABI 对齐）。</summary>
    private static PluginServiceScope CreatePluginScope(CapabilityRegistry registry, IProgramScanner scanner)
    {
        var manifest = new PluginManifest
        {
            SchemaVersion = 1,
            Id = PluginId,
            Name = PluginId,
            Version = "1.0.0",
            Sdk = "1.0",
            EntryAssembly = "StarPie.Tests.dll",
            EntryType = "StarPie.Tests.StubProgramSourcePlugin",
            Capabilities = new List<PluginCapabilityReference>
            {
                new() { Id = ProgramSourceCapability.Contract.Id, Abi = ProgramSourceCapability.Contract.Abi },
            },
        };
        var scope = new PluginServiceScope(
            manifest,
            PluginCapabilityTestDoubles.Lifecycle(PluginLifecycleState.Active),
            registry);
        scope.Context.RegisterCapability<IProgramScanner>(scanner);
        return scope;
    }

    /// <summary>固定列表或固定异常的来源替身：聚合只关心「来源返回什么 / 抛不抛」。</summary>
    private sealed class StubProgramScanner : IProgramScanner
    {
        private readonly IReadOnlyList<ProgramEntry> _entries;
        private readonly Exception? _failure;

        internal StubProgramScanner(params ProgramEntry[] entries) => _entries = entries;

        internal StubProgramScanner(Exception failure)
        {
            _failure = failure;
            _entries = Array.Empty<ProgramEntry>();
        }

        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => _failure is null ? _entries : throw _failure;
    }
}
