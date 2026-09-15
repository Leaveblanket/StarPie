using System;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using StarPie.Abstractions.Ui;
using StarPie.Kernel.Localization;
using StarPie.PluginHosting;
using StarPie.PluginHosting.Extensions;
using StarPie.PluginRuntime;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Discovery;
using StarPie.PluginRuntime.Hosting;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Registry;
using StarPie.PluginRuntime.State;
using StarPie.PluginRuntime.Unloading;
using StarPie.Services.Navigation;
using StarPie.ViewModels.Pages;

namespace StarPie.Tests;

/// <summary>
/// 宿主固定扩展点的插件注册面：导航页进目录（固定页之后、AutomationId 由宿主签发）、
/// 设置区与托盘菜单可枚举；未接目录的宿主保持注册可用，无插件时扩展点为空白。
/// </summary>
public sealed class PluginExtensionPointsTests
{
    private const string PluginId = "com.example.ui";
    private const string AutomationId = "NavPlugin_com.example.ui";

    private sealed class PluginPageViewModel : ObservableObject { }

    private static (PluginUiCoordinator Coordinator, NavigationCatalog Catalog) Create()
    {
        var catalog = new NavigationCatalog();
        foreach (NavigationSlot slot in NavigationSlots.All)
        {
            catalog.RegisterPage<PluginPageViewModel>(
                slot, NavigationSlots.GetAutomationId(slot), "Page" + slot, string.Empty);
        }

        return (new PluginUiCoordinator(
            StaTestHarness.Application, StaTestHarness.Dispatcher, navigationCatalog: catalog), catalog);
    }

    private static PluginPageDescriptor Page(string key = "main") => new(
        key, "PluginPageTitle", "M0 0", () => new PluginPageViewModel());

    [Fact]
    public async Task 插件页注册_宿主签发AutomationId并追加在固定页之后()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            var (coordinator, catalog) = Create();

            coordinator.GetOrCreateHost(PluginId).RegisterPage(Page());

            Assert.Equal(
                new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4", AutomationId },
                catalog.Entries.Select(entry => entry.AutomationId));
            Assert.Equal(PluginId, catalog.GetEntry(AutomationId).PluginId);
        });
    }

    [Fact]
    public async Task 摘除插件页_目录与登记表同时出账()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            var (coordinator, catalog) = Create();
            var page = Page();
            IDisposable handle = coordinator.GetOrCreateHost(PluginId).RegisterPage(page);

            handle.Dispose();

            Assert.DoesNotContain(catalog.Entries, entry => entry.AutomationId == AutomationId);
            Assert.Equal(0, coordinator.Assets.CountFor(PluginId));
        });
    }

    [Fact]
    public void 插件标题键不在宿主文案表且未给显示名_注册期告警且取走即清空()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(
                StaTestHarness.Application,
                StaTestHarness.Dispatcher,
                localization: new LocalizationService());
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            host.RegisterPage(new PluginPageDescriptor(
                "main", "NoSuchPluginPageTitle", "M0 0", () => new PluginPageViewModel()));
            host.RegisterSettingsSection(new PluginSettingsSectionDescriptor(
                "options", "NoSuchPluginSectionTitle", 10, () => new PluginPageViewModel()));
            host.RegisterMenuItem(new PluginMenuItemDescriptor(
                "about", "NoSuchPluginMenuTitle", "about"));

            IReadOnlyList<string> warnings = host.TakeWarnings();

            Assert.Equal(3, warnings.Count);
            Assert.Contains(warnings, warning => warning.Contains("NoSuchPluginPageTitle"));
            Assert.Contains(warnings, warning => warning.Contains("NoSuchPluginSectionTitle"));
            Assert.Contains(warnings, warning => warning.Contains("NoSuchPluginMenuTitle"));
            // 取走即清空：同一处缺失不在后续装载里重复告警。
            Assert.Empty(host.TakeWarnings());
        });
    }

    [Fact]
    public void 插件标题_给了显示名或键落在宿主文案表_均不告警()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(
                StaTestHarness.Application,
                StaTestHarness.Dispatcher,
                localization: new LocalizationService());
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            host.RegisterPage(new PluginPageDescriptor(
                "main", "NoSuchPluginPageTitle", "M0 0", () => new PluginPageViewModel())
            {
                DisplayName = "示例页",
            });
            host.RegisterSettingsSection(new PluginSettingsSectionDescriptor(
                "options", "ThemeLight", 10, () => new PluginPageViewModel()));

            Assert.Empty(host.TakeWarnings());
        });
    }

    [Fact]
    public void 未接文案服务的宿主_不做缺键判定也不告警()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            host.RegisterPage(new PluginPageDescriptor(
                "main", "NoSuchPluginPageTitle", "M0 0", () => new PluginPageViewModel()));

            Assert.Empty(host.TakeWarnings());
        });
    }

    [Fact]
    public void 插件页显示名_穿透到导航目录登记项()
    {
        StaTestHarness.Run(() =>
        {
            var (coordinator, catalog) = Create();

            coordinator.GetOrCreateHost(PluginId).RegisterPage(
                Page() with { DisplayName = "示例页" });

            Assert.Equal("示例页", catalog.GetEntry(AutomationId).DisplayName);
        });
    }

    [Fact]
    public void 插件设置区块显示名_优先于宿主文案键()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            var page = new PluginManagerViewModel(
                PluginRuntimeHostFixture.Create(),
                new NavigationStore(),
                new LocalizationService(),
                coordinator);
            coordinator.GetOrCreateHost(PluginId).RegisterSettingsSection(
                new PluginSettingsSectionDescriptor(
                    "options", "NoSuchPluginSectionTitle", 10, () => new PluginPageViewModel())
                {
                    DisplayName = "示例区块",
                });

            page.Refresh();

            Assert.Equal("示例区块", Assert.Single(page.PluginSettingsSections).Title);
        });
    }

    [Fact]
    public void 仅给显示名_注册可用且不再占宿主文案键位()
    {
        StaTestHarness.Run(() =>
        {
            var (coordinator, _) = Create();

            coordinator.GetOrCreateHost(PluginId).RegisterPage(
                Page() with { TitleKey = string.Empty, DisplayName = "示例页" });

            Assert.Single(coordinator.GetOrCreateHost(PluginId).Pages);
        });
    }

    [Fact]
    public void 标题键与显示名同时为空_注册被拒且不记账()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            Assert.IsType<ArgumentException>(Record.Exception(() => host.RegisterPage(
                new PluginPageDescriptor("main", string.Empty, "M0 0", () => new PluginPageViewModel()))));
            Assert.IsType<ArgumentException>(Record.Exception(() => host.RegisterMenuItem(
                new PluginMenuItemDescriptor("about", "   ", "about"))));
            Assert.IsType<ArgumentException>(Record.Exception(() => host.RegisterSettingsSection(
                new PluginSettingsSectionDescriptor("options", string.Empty, 10, () => new PluginPageViewModel()))));
            Assert.IsType<ArgumentException>(Record.Exception(() => host.RegisterWindow(
                new PluginWindowDescriptor("window", string.Empty, () => new Window()))));
            Assert.IsType<ArgumentException>(Record.Exception(() => host.RegisterCommand(
                new PluginCommandDescriptor("run", string.Empty, () => { }))));

            Assert.Empty(host.Pages);
            Assert.Empty(host.MenuItems);
            Assert.Empty(host.SettingsSections);
        });
    }

    [Fact]
    public void 未接导航目录_页面注册不抛异常且不出现空壳()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            Exception? error = Record.Exception(() => host.RegisterPage(Page()));

            Assert.Null(error);
            // 未接目录：注册照常记账（可被卸载清理），但不产生无处可挂的导航项。
            Assert.Equal(1, host.Assets.CountFor(PluginId));
            Assert.Single(host.Pages);
            Assert.Empty(coordinator.MenuItems);
        });
    }

    [Fact]
    public async Task 重复注册同一插件页_第二次失败且不重复记账()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            var (coordinator, catalog) = Create();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.RegisterPage(Page());

            Exception? second = Record.Exception(() => host.RegisterPage(Page()));

            Assert.IsType<InvalidOperationException>(second);
            Assert.Single(host.Pages);
            Assert.Equal(1, host.Assets.CountFor(PluginId));
            Assert.Single(catalog.Entries, entry => entry.AutomationId == AutomationId);
        });
    }

    [Fact]
    public async Task 无插件时_扩展点为空且无宿主上下文()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            var (coordinator, _) = Create();

            Assert.Empty(coordinator.SettingsSections);
            Assert.Empty(coordinator.MenuItems);

            var result = await coordinator.ReleaseAsync("com.example.absent", default);

            Assert.True(result.Succeeded);
            Assert.False(coordinator.HasHost("com.example.absent"));
        });
    }

    [Fact]
    public void 插件注册设置区与托盘菜单_按所属插件可枚举()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            host.RegisterSettingsSection(new PluginSettingsSectionDescriptor(
                "options", "PluginOptionsTitle", 10, () => new PluginPageViewModel()));
            host.RegisterMenuItem(new PluginMenuItemDescriptor("about", "PluginAboutTitle", "about"));

            PluginSettingsSection section = Assert.Single(coordinator.SettingsSections);
            Assert.Equal(PluginId, section.PluginId);
            Assert.Equal("options", section.Descriptor.SectionKey);
            PluginMenuItem item = Assert.Single(coordinator.MenuItems);
            Assert.Equal(PluginId, item.PluginId);
            Assert.Equal("about", item.Descriptor.ItemKey);
        });
    }

    [Fact]
    public void 无插件时_设置面无插件区块()
    {
        var page = new PluginManagerViewModel(
            PluginRuntimeHostFixture.Create(),
            new NavigationStore(),
            new LocalizationService(),
            new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher));

        Assert.Empty(page.PluginSettingsSections);
    }

    [Fact]
    public void 插件注册设置区块_设置面呈现区块标题与区块VM()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            var localization = new LocalizationService();
            var page = new PluginManagerViewModel(
                PluginRuntimeHostFixture.Create(),
                new NavigationStore(),
                localization,
                coordinator);
            var sectionViewModel = new PluginPageViewModel();
            coordinator.GetOrCreateHost(PluginId).RegisterSettingsSection(
                new PluginSettingsSectionDescriptor(
                    "options", "PluginOptionsTitle", 10, () => sectionViewModel));

            page.Refresh();

            PluginSettingsSectionViewModel section = Assert.Single(page.PluginSettingsSections);
            Assert.Equal(localization.GetString("PluginOptionsTitle"), section.Title);
            Assert.Same(sectionViewModel, section.ViewModel);
        });
    }

    /// <summary>插件管理页的最小宿主运行时夹具：空扫描报告足以构造页面。</summary>
    private static class PluginRuntimeHostFixture
    {
        internal static PluginRuntimeHost Create()
        {
            string root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "starpie-plugin-page-" + Guid.NewGuid().ToString("N"));
            var store = new PluginStateStore(System.IO.Path.Combine(root, "plugin-state.json"));
            var scanner = new PluginStartupScanner(
                new PluginDiscovery(
                    System.IO.Path.Combine(root, "plugins"),
                    System.IO.Path.Combine(root, "user-plugins")),
                new PluginAdmissionPolicy(
                    PluginAdmissionPolicy.DefaultBuiltInPluginIds,
                    new EmptyPluginReviewCatalog()),
                store,
                new PluginStartupReportWriter(
                    System.IO.Path.Combine(root, "plugin-startup-report.json")));
            return new PluginRuntimeHost(
                scanner,
                store,
                new PluginLoadPipeline(new CapabilityRegistry()),
                new PluginUnloadPipeline(() => { }));
        }
    }
}
