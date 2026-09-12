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
    private readonly TestDialogService _dialogs = new();

    public PluginManagerViewModelTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-manager-tests").FullName;
        _localization.SetLanguage("zh-CN");
        _stateStore = new PluginStateStore(Path.Combine(_tempRoot, "plugin-state.json"));
        var registry = new CapabilityRegistry();
        registry.DeclareBuiltin(ProgramSourceCapability.Contract, new BuiltInStubScanner());
        // 插件包写进临时目录：彻底移除会真删包目录，绝不能指向测试输出目录里的随包插件。
        PluginTestPackage.CreateLoadable(_tempRoot, PluginId);
        _host = new PluginRuntimeHost(
            new PluginStartupScanner(
                new PluginDiscovery(_tempRoot, Path.Combine(_tempRoot, "user-plugins")),
                new PluginAdmissionPolicy(PluginAdmissionPolicy.DefaultBuiltInPluginIds),
                _stateStore,
                new PluginStartupReportWriter(Path.Combine(_tempRoot, "plugin-startup-report.json"))),
            _stateStore,
            new PluginLoadPipeline(registry),
            new PluginUnloadPipeline(() => { }),
            new PluginUninstallOptions
            {
                RemoveConfigSection = _ => { },
                PluginDataRoot = Path.Combine(_tempRoot, "plugin-data"),
                FlushPendingSaves = () => { },
            });
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
    public async Task 诊断面板_呈现本进程生效版本()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        Assert.Single(viewModel.Plugins).DiagnosticsCommand.Execute(null);

        Assert.Contains("本进程生效版本", viewModel.DiagnosticsText);
        Assert.Contains("1.0.0", viewModel.DiagnosticsText);
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

    [Fact]
    public async Task 页面_常显当前准入模式()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        // 默认（开发者模式关闭）：准入只认内置与审核清单。
        Assert.False(viewModel.IsDeveloperModeEnabled);
        Assert.Equal("仅内置与审核清单", viewModel.AdmissionModeText);

        _stateStore.Current.DeveloperModeEnabled = true;
        _stateStore.Save();
        viewModel.Refresh();

        Assert.True(viewModel.IsDeveloperModeEnabled);
        Assert.Equal("开发者模式", viewModel.AdmissionModeText);
    }

    [Fact]
    public async Task 列表_区分停用重载更新三态()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        PluginManagerItemViewModel active = Assert.Single(viewModel.Plugins);

        // 活动态：停用/重载/更新三个动作都可发起，互不等价。
        Assert.True(active.CanToggle);
        Assert.True(active.CanReload);
        Assert.True(active.CanUpdate);
        Assert.True(active.CanUninstall);
        Assert.Equal("停用", active.ToggleText);
        Assert.Equal("重载", active.ReloadText);
        Assert.Equal("更新", active.UpdateText);
        Assert.False(active.HasPendingRestart);

        // 已停用：可再启用，没有可重载/可更新的运行实例。
        await active.ToggleCommand.ExecuteAsync(null);
        PluginManagerItemViewModel disabled = Assert.Single(viewModel.Plugins);
        Assert.Equal("已停用", disabled.StatusText);
        Assert.False(disabled.CanReload);
        Assert.False(disabled.CanUpdate);
        Assert.True(disabled.CanUninstall);
    }

    [Fact]
    public async Task 界面插件更新后_条目表达下次启动生效()
    {
        // 界面插件：程序集留在进程内不可回收，更新只能隔离旧版本并等下次启动。
        const string UiPluginId = "com.example.manager-ui";
        const string UiSection = """
            "sdk": "1.0", "entryType": "StarPie.Tests.ProgramSourceTestPlugin"
            """;
        string packageRoot = Path.Combine(_tempRoot, "ui-plugins");
        PluginTestPackage.CreateLoadable(
            packageRoot, UiPluginId, version: "1.0.0", uiSection: UiSection);
        var uiState = new PluginStateStore(Path.Combine(_tempRoot, "ui-state.json"));
        var uiRegistry = new CapabilityRegistry();
        uiRegistry.DeclareContract(ProgramSourceCapability.Contract);
        // UI 托管替身：界面插件按"宿主托管 UI"装载（本测试只关心管理面状态语义）。
        var uiCoordinator = new StubPluginUiCoordinator();
        var uiHostWithUi = new PluginRuntimeHost(
            new PluginStartupScanner(
                new PluginDiscovery(packageRoot, Path.Combine(_tempRoot, "ui-user")),
                new PluginAdmissionPolicy(new[] { UiPluginId }),
                uiState,
                new PluginStartupReportWriter(Path.Combine(_tempRoot, "ui-report.json"))),
            uiState,
            new PluginLoadPipeline(uiRegistry, uiCoordinator: uiCoordinator),
            new PluginUnloadPipeline(() => { }, uiCoordinator: uiCoordinator),
            new PluginUninstallOptions
            {
                RemoveConfigSection = _ => { },
                PluginDataRoot = Path.Combine(_tempRoot, "ui-plugin-data"),
                FlushPendingSaves = () => { },
            });
        await uiHostWithUi.StartAsync(CancellationToken.None);
        Assert.Equal("活动", new PluginManagerViewModel(uiHostWithUi, new NavigationStore(), _localization)
            .Plugins.Single().StatusText);

        PluginTestPackage.CreateLoadable(
            packageRoot, UiPluginId, version: "2.0.0", uiSection: UiSection);
        var viewModel = new PluginManagerViewModel(uiHostWithUi, new NavigationStore(), _localization, dialogs: _dialogs);

        await viewModel.Plugins.Single().UpdateCommand.ExecuteAsync(null);

        PluginManagerItemViewModel item = Assert.Single(viewModel.Plugins);
        Assert.Equal("待重启", item.StatusText);
        Assert.True(item.HasPendingRestart);
        Assert.Contains("2.0.0", item.PendingRestartText);
        Assert.Contains("下次启动", item.PendingRestartText);
        Assert.Equal("停用", item.ToggleText);
        Assert.False(item.CanReload);
        Assert.False(item.CanUpdate);
        Assert.True(item.CanUninstall);
    }

    [Fact]
    public async Task 重载命令_走安全点卸载后重新装载()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();

        await Assert.Single(viewModel.Plugins).ReloadCommand.ExecuteAsync(null);

        Assert.Equal("活动", Assert.Single(viewModel.Plugins).StatusText);
        Assert.Equal(new[] { PluginId }, _host.ActivePluginIds);
    }

    [Fact]
    public async Task 彻底移除命令_确认后清空条目()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();
        _dialogs.ConfirmResult = true;

        await Assert.Single(viewModel.Plugins).UninstallCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.ConfirmCalls);
        Assert.Empty(viewModel.Plugins);
    }

    [Fact]
    public async Task 彻底移除命令_取消确认时不动作()
    {
        await _host.StartAsync(CancellationToken.None);
        PluginManagerViewModel viewModel = CreateViewModel();
        _dialogs.ConfirmResult = false;

        await Assert.Single(viewModel.Plugins).UninstallCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Plugins);
        Assert.Equal(new[] { PluginId }, _host.ActivePluginIds);
    }

    private PluginManagerViewModel CreateViewModel()
        => new(_host, _navigation, _localization, dialogs: _dialogs);

    /// <summary>内置来源替身：让能力契约成立，不参与页面断言。</summary>
    private sealed class BuiltInStubScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => new[] { new ProgramEntry("记事本 (Notepad)", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe") };
    }
}
