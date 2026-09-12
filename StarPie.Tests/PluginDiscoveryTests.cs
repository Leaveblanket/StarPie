using System;
using System.IO;
using System.Linq;
using StarPie.PluginRuntime.Discovery;

namespace StarPie.Tests;

/// <summary>
/// 插件包发现缝：发现顺序、候选包判定与发现期包内容违规。
/// </summary>
public sealed class PluginDiscoveryTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _installRoot;
    private readonly string _userRoot;

    public PluginDiscoveryTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-discovery-tests").FullName;
        _installRoot = Path.Combine(_tempRoot, "install", "plugins");
        _userRoot = Path.Combine(_tempRoot, "user", "plugins");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void 两处目录都不存在_插件缺席是正常态()
    {
        var discovery = new PluginDiscovery(_installRoot, _userRoot);

        Assert.Empty(discovery.Discover());
    }

    [Fact]
    public void 发现顺序_安装目录在前用户目录在后()
    {
        Directory.CreateDirectory(_installRoot);
        Directory.CreateDirectory(_userRoot);
        PluginTestPackage.Create(_installRoot, "com.example.install");
        PluginTestPackage.Create(_userRoot, "com.example.user");

        IReadOnlyList<PluginPackageCandidate> candidates = new PluginDiscovery(_installRoot, _userRoot).Discover();

        Assert.Equal(
            new[] { "com.example.install", "com.example.user" },
            candidates.Select(candidate => candidate.DirectoryName));
        Assert.Equal(
            new[] { PluginPackageOrigin.Install, PluginPackageOrigin.User },
            candidates.Select(candidate => candidate.Origin));
        Assert.All(candidates, candidate => Assert.Empty(candidate.Violations));
    }

    [Fact]
    public void 无清单的目录不算候选包()
    {
        Directory.CreateDirectory(Path.Combine(_installRoot, "plugin-data"));
        File.WriteAllText(Path.Combine(_installRoot, "plugin-data", "cache.json"), "{}");

        Assert.Empty(new PluginDiscovery(_installRoot, _userRoot).Discover());
    }

    [Fact]
    public void 同id同时存在于两处_两份候选都保留_冲突裁决归上层()
    {
        PluginTestPackage.Create(_installRoot, "com.example.a");
        PluginTestPackage.Create(_userRoot, "com.example.a");

        IReadOnlyList<PluginPackageCandidate> candidates = new PluginDiscovery(_installRoot, _userRoot).Discover();

        Assert.Equal(2, candidates.Count);
        Assert.Equal(new[] { PluginPackageOrigin.Install, PluginPackageOrigin.User }, candidates.Select(c => c.Origin));
    }

    [Fact]
    public void 包内出现SDK副本_记录发现期违规()
    {
        string package = PluginTestPackage.Create(_installRoot, "com.example.a");
        string nested = Path.Combine(package, "lib");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "StarPie.Sdk.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllBytes(Path.Combine(package, "StarPie.Sdk.Wpf.dll"), new byte[] { 0x4D, 0x5A });

        PluginPackageCandidate candidate = Assert.Single(new PluginDiscovery(_installRoot, _userRoot).Discover());

        Assert.Equal(2, candidate.Violations.Count);
        Assert.Contains(candidate.Violations, violation => violation.Contains("StarPie.Sdk.dll"));
        Assert.Contains(candidate.Violations, violation => violation.Contains("StarPie.Sdk.Wpf.dll"));
    }

    [Fact]
    public void 目录内候选包按目录名稳定序()
    {
        PluginTestPackage.Create(_installRoot, "com.example.z");
        PluginTestPackage.Create(_installRoot, "com.example.a");
        PluginTestPackage.Create(_installRoot, "com.example.m");

        IReadOnlyList<PluginPackageCandidate> candidates = new PluginDiscovery(_installRoot, _userRoot).Discover();

        Assert.Equal(
            new[] { "com.example.a", "com.example.m", "com.example.z" },
            candidates.Select(candidate => candidate.DirectoryName));
    }
}
