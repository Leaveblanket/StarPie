using System;
using System.IO;
using System.Linq;
using StarPie.Kernel.Localization;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.State;

namespace StarPie.Tests;

/// <summary>
/// 开发者模式开关缝：默认关闭、开启须确认披露、开关跨重启持久化与披露文案取词。
/// </summary>
public sealed class PluginDeveloperModeServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _statePath;

    public PluginDeveloperModeServiceTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-devmode-tests").FullName;
        _statePath = Path.Combine(_tempRoot, "plugin-state.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void 默认关闭_且不产生状态文件()
    {
        PluginDeveloperModeService service = CreateService(new PluginStateStore(_statePath));

        Assert.False(service.IsEnabled);
        Assert.False(File.Exists(_statePath));
    }

    [Fact]
    public void 未先显式加载_开启开发者模式不会丢掉既有条目()
    {
        var writer = new PluginStateStore(_statePath);
        writer.Current.GetOrCreate("com.example.a").Enabled = false;
        writer.Save();

        // 管理面可能在启动扫描前调用：读取与落盘都必须基于磁盘上的既有宿主状态。
        PluginDeveloperModeService service = CreateService(new PluginStateStore(_statePath));
        Assert.True(service.Enable(disclosureAcknowledged: true));

        var reader = new PluginStateStore(_statePath);
        reader.Load();
        Assert.True(reader.Current.DeveloperModeEnabled);
        Assert.False(reader.Current.Plugins["com.example.a"].Enabled);
    }

    [Fact]
    public void 未确认披露_拒绝开启且状态不变()
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        PluginDeveloperModeService service = CreateService(store);

        bool enabled = service.Enable(disclosureAcknowledged: false);

        Assert.False(enabled);
        Assert.False(service.IsEnabled);
        Assert.False(File.Exists(_statePath));
    }

    [Fact]
    public void 确认披露_开启并落盘_重启后仍开启()
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        PluginDeveloperModeService service = CreateService(store);

        Assert.True(service.Enable(disclosureAcknowledged: true));

        Assert.True(service.IsEnabled);
        Assert.True(File.Exists(_statePath));

        var restartedStore = new PluginStateStore(_statePath);
        restartedStore.Load();
        Assert.True(CreateService(restartedStore).IsEnabled);
    }

    [Fact]
    public void 关闭_落盘且重启后仍关闭()
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        PluginDeveloperModeService service = CreateService(store);
        Assert.True(service.Enable(disclosureAcknowledged: true));

        service.Disable();

        Assert.False(service.IsEnabled);
        var restartedStore = new PluginStateStore(_statePath);
        restartedStore.Load();
        Assert.False(CreateService(restartedStore).IsEnabled);
    }

    [Fact]
    public void 披露条目_四语言均为非空本地化文案()
    {
        foreach (string language in new[] { "zh-CN", "zh-TW", "en", "ja" })
        {
            var localization = new LocalizationService();
            localization.SetLanguage(language);
            PluginDeveloperModeService service = CreateService(new PluginStateStore(_statePath), localization);

            string title = service.DisclosureTitle;
            IReadOnlyList<string> points = service.GetDisclosurePoints();

            Assert.False(string.IsNullOrWhiteSpace(title));
            Assert.DoesNotContain("PluginDevMode", title);
            Assert.Equal(4, points.Count);
            Assert.Equal(4, points.Distinct(StringComparer.Ordinal).Count());
            Assert.All(points, point =>
            {
                Assert.False(string.IsNullOrWhiteSpace(point));
                Assert.DoesNotContain("PluginDevMode", point);
            });
        }
    }

    private static PluginDeveloperModeService CreateService(PluginStateStore store, ILocalizationService? localization = null)
        => new(store, localization ?? new LocalizationService());
}
