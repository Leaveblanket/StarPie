using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Kernel.Localization;
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
using StarPie.Services.Navigation;
using StarPie.Services.Programs;
using StarPie.ViewModels.Pages;

namespace StarPie.Tests;

/// <summary>
/// 插件管理页 VM：列表投影、启停经宿主管线、诊断面板呈现隔离原因与可定位残留清单。
/// </summary>
public sealed class PluginManagerViewModelTests : IDisposable
{
    private const string PluginId = "starpie.builtin.program-source";

    private readonly string _tempRoot;
    private readonly PluginStateStore _stateStore;
    private readonly PluginRuntimeHost _host;
    private readonly NavigationStore _navigation = new();
    private readonly LocalizationService _localization = new();

    public PluginManagerViewModelTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-manager-tests").FullName;
        _localization.SetLanguage("zh-CN");
        _stateStore = new PluginStateStore(Path.Combine(_tempRoot, "plugin-state.json"));
        var registry = new CapabilityRegistry();
        registry.DeclareBuiltin(ProgramSourceCapability.Contract, new BuiltInStubScanner());
        _host = new PluginRuntimeHost(
            new PluginStartupScanner(
                new PluginDiscovery(PluginPaths.InstallDirectory, Path.Combine(_tempRoot, "user-plugins")),
                new PluginAdmissionPolicy(PluginAdmissionPolicy.DefaultBuiltInPluginIds),
                _stateStore,
                new PluginStartupReportWriter(Path.Combine(_tempRoot, "plugin-startup-report.json"))),
            _stateStore,
            new PluginLoadPipeline(registry),
            new PluginUnloadPipeline(() => { }));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task 列表_呈现扫描到的插件与状态()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        PluginManagerItemViewModel item = Assert.Single(viewModel.Plugins);

        Assert.Equal(PluginId, item.PluginId);
        Assert.Equal("活动", item.StatusText);
        Assert.Equal("内置", item.AdmissionText);
        Assert.True(item.CanToggle);
        Assert.False(item.CanRetry);
        Assert.False(item.HasQuarantine);
    }

    [Fact]
    public async Task 停用命令_经宿主管线卸载并刷新状态()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        await Assert.Single(viewModel.Plugins).ToggleCommand.ExecuteAsync(null);

        Assert.Empty(_host.ActivePluginIds);
        Assert.False(_stateStore.Current.Plugins[PluginId].Enabled);
        Assert.Equal("已停用", Assert.Single(viewModel.Plugins).StatusText);
        Assert.Equal("启用", Assert.Single(viewModel.Plugins).ToggleText);

        // 同一条目命令按刷新后的状态切回装载。
        await Assert.Single(viewModel.Plugins).ToggleCommand.ExecuteAsync(null);

        Assert.Equal(new[] { PluginId }, _host.ActivePluginIds);
        Assert.Equal("活动", Assert.Single(viewModel.Plugins).StatusText);
    }

    [Fact]
    public async Task 诊断面板_呈现隔离原因与残留清单()
    {
        _stateStore.Current.GetOrCreate(PluginId).Quarantine = new PluginQuarantineState(
            "夹具隔离：回收未过",
            DateTimeOffset.Now)
        {
            Residuals = new[]
            {
                new PluginResidual
                {
                    Kind = PluginResidualKind.Type,
                    Detail = "StarPie.Tests：StarPie.Tests.ProgramSourceTestPlugin",
                },
            },
        };
        _stateStore.Save();
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        Assert.Single(viewModel.Plugins).DiagnosticsCommand.Execute(null);

        PluginManagerItemViewModel item = Assert.Single(viewModel.Plugins);
        Assert.Equal("已隔离", item.StatusText);
        Assert.True(item.CanRetry);
        // 隔离态另有"停用"入口：显式放弃隔离并落停用意图（与"重试"并列，互不替代）。
        Assert.True(item.CanToggle);
        Assert.Equal("停用", item.ToggleText);
        Assert.Contains("夹具隔离：回收未过", viewModel.DiagnosticsText);
        Assert.Contains("残留清单", viewModel.DiagnosticsText);
        Assert.Contains("ProgramSourceTestPlugin", viewModel.DiagnosticsText);
    }

    [Fact]
    public async Task 隔离态停用命令_落停用意图且隔离原因保持可见()
    {
        _stateStore.Current.GetOrCreate(PluginId).Quarantine = new PluginQuarantineState(
            "夹具隔离：装载失败",
            DateTimeOffset.Now);
        _stateStore.Save();
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        await Assert.Single(viewModel.Plugins).ToggleCommand.ExecuteAsync(null);

        // 隔离是持久状态：停用只落"不再装载"的意图，不抹掉用户要定位的隔离原因。
        Assert.False(_stateStore.Current.Plugins[PluginId].Enabled);
        Assert.Equal("已隔离", Assert.Single(viewModel.Plugins).StatusText);
    }

    [Fact]
    public async Task 导航到本页_重读宿主报告()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();
        Assert.Equal("活动", Assert.Single(viewModel.Plugins).StatusText);

        // 宿主侧状态在页面之外被改变（例如守卫熔断隔离）：导航回本页时重读报告。
        await _host.DisableAsync(PluginId, CancellationToken.None);
        _navigation.CurrentViewModel = viewModel;

        Assert.Equal("已停用", Assert.Single(viewModel.Plugins).StatusText);
    }

    private PluginManagerViewModel CreateViewModel()
        => new(_host, _navigation, _localization);

    /// <summary>内置来源替身：让能力契约成立，不参与页面断言。</summary>
    private sealed class BuiltInStubScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => new[] { new ProgramEntry("记事本 (Notepad)", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe") };
    }
}
