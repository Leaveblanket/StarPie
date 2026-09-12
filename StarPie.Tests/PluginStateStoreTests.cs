using System;
using System.IO;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.State;

namespace StarPie.Tests;

/// <summary>
/// 宿主插件状态存储缝：加载回退、落盘往返与条目取值。
/// </summary>
public sealed class PluginStateStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _statePath;

    public PluginStateStoreTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-state-tests").FullName;
        _statePath = Path.Combine(_tempRoot, "data", "plugin-state.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void 文件缺失_空状态且开发者模式默认关闭()
    {
        var store = new PluginStateStore(_statePath);

        // 未显式 Load：首次读取当前状态即自动加载，缺失即空状态。
        Assert.False(store.Current.DeveloperModeEnabled);
        Assert.Empty(store.Current.Plugins);
        Assert.False(File.Exists(_statePath));
    }

    [Fact]
    public void 首次读取自动加载_既有条目不会被空文档覆盖()
    {
        var writer = new PluginStateStore(_statePath);
        writer.Current.GetOrCreate("com.example.a").Enabled = false;
        writer.Save();

        // 新实例不显式 Load：Current 读到磁盘内容，而非空文档。
        var reader = new PluginStateStore(_statePath);
        Assert.False(reader.Current.Plugins["com.example.a"].Enabled);

        reader.Save();

        Assert.Contains("com.example.a", File.ReadAllText(_statePath));
    }

    [Fact]
    public void 保存后重载_启用停用版本路径准入来源与隔离全部往返()
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        PluginStateEntry entry = store.Current.GetOrCreate("com.example.a");
        entry.Enabled = false;
        entry.Version = "1.2.3";
        entry.PackagePath = Path.Combine(_tempRoot, "plugins", "com.example.a");
        entry.Admission = PluginAdmission.DeveloperMode;
        entry.AdmissionReason = "开发者模式放行";
        entry.Quarantine = new PluginQuarantineState("卸载资产未清零", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)));
        store.Save();

        var reloaded = new PluginStateStore(_statePath);
        reloaded.Load();

        PluginStateEntry actual = reloaded.Current.Plugins["com.example.a"];
        Assert.False(actual.Enabled);
        Assert.Equal("1.2.3", actual.Version);
        Assert.Equal(entry.PackagePath, actual.PackagePath);
        Assert.Equal(PluginAdmission.DeveloperMode, actual.Admission);
        Assert.Equal("开发者模式放行", actual.AdmissionReason);
        Assert.Equal("卸载资产未清零", actual.Quarantine!.Reason);
        Assert.Equal(entry.Quarantine!.Since, actual.Quarantine!.Since);
    }

    [Fact]
    public void 状态文件_枚举写为可读名称而不是序号()
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        store.Current.GetOrCreate("com.example.a").Admission = PluginAdmission.BuiltIn;
        store.Save();

        string json = File.ReadAllText(_statePath);

        Assert.Contains("\"Admission\": \"BuiltIn\"", json);
    }

    [Fact]
    public void JSON损坏_回退空状态且不覆盖原文件()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        const string corrupt = "{ this is not json";
        File.WriteAllText(_statePath, corrupt);
        var store = new PluginStateStore(_statePath);

        store.Load();

        Assert.False(store.Current.DeveloperModeEnabled);
        Assert.Empty(store.Current.Plugins);
        Assert.Equal(corrupt, File.ReadAllText(_statePath));
    }

    [Fact]
    public void 保存_目录不存在时自行创建()
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        store.Current.DeveloperModeEnabled = true;

        store.Save();

        Assert.True(File.Exists(_statePath));
    }

    [Fact]
    public void GetOrCreate_同一id复用同一条目()
    {
        var store = new PluginStateStore(_statePath);

        PluginStateEntry first = store.Current.GetOrCreate("com.example.a");
        PluginStateEntry second = store.Current.GetOrCreate("com.example.a");

        Assert.Same(first, second);
        Assert.True(first.Enabled);
        Assert.Equal(PluginAdmission.Rejected, first.Admission);
    }
}
