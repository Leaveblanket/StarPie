using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StarPie.Abstractions.Ui;
using StarPie.Localization;
using StarPie.PluginHosting;
using StarPie.Services.Shell;
using StarPie.ViewModels.Navigation;

namespace StarPie.Tests;

/// <summary>
/// 托盘菜单扩展点：插件菜单项按权重排进宿主托盘菜单并路由到插件命令；
/// 无插件时菜单只剩内置条目（不出现空壳分隔或异常）。
/// </summary>
public sealed class PluginTrayMenuTests
{
    private const string PluginId = "com.example.ui";

    private sealed class PluginPageViewModel : ObservableObject { }

    private static IReadOnlyList<TrayMenuEntry> Compose(
        PluginUiCoordinator coordinator,
        IReadOnlyList<TrayMenuEntry>? builtIn = null)
        => StaTestHarness.Run(() => TrayMenuComposer.Compose(
            builtIn ?? Array.Empty<TrayMenuEntry>(),
            coordinator,
            new LocalizationService()));

    private static PluginMenuItemDescriptor MenuItem(string itemKey, int order)
        => new(itemKey, string.Empty, itemKey)
        {
            DisplayName = itemKey,
            Order = order,
        };

    [Fact]
    public void 无插件时_菜单与内置条目一致()
    {
        var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
        var builtIn = new[]
        {
            TrayMenuEntry.Header("StarPie"),
            TrayMenuEntry.Item("Preferences", () => { }),
        };

        IReadOnlyList<TrayMenuEntry> entries = Compose(coordinator, builtIn);

        Assert.Equal(2, entries.Count);
        Assert.Equal("StarPie", entries[0].Label);
        Assert.Equal("Preferences", entries[1].Label);
    }

    [Fact]
    public void 插件菜单项_按权重追加并显示本地化标题()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.RegisterCommand(new PluginCommandDescriptor("open", "PluginOpen", () => { }));
            host.RegisterMenuItem(new PluginMenuItemDescriptor("open", "PluginOpen", "open")
            {
                Order = 5,
            });

            IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposer.Compose(
                new[] { TrayMenuEntry.Item("Preferences", () => { }) },
                coordinator,
                new LocalizationService());

            Assert.Equal(3, entries.Count);
            Assert.Null(entries[1].Label); // 插件条目前的分隔线
            Assert.Equal(new LocalizationService().GetString("PluginOpen"), entries[2].Label);
            Assert.NotNull(entries[2].Callback);
        });
    }

    [Fact]
    public void 多插件多条菜单项_按权重再插件id排序()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            // 先建 B 后建 A：注册顺序与排序结果相反，排序键才是判据。
            PluginUiHost hostB = coordinator.GetOrCreateHost("com.example.b");
            PluginUiHost hostA = coordinator.GetOrCreateHost("com.example.a");
            hostB.RegisterMenuItem(MenuItem("b-early", order: 10));
            hostA.RegisterMenuItem(MenuItem("a-late", order: 20));
            hostB.RegisterMenuItem(MenuItem("b-late", order: 20));

            IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposer.Compose(
                new[] { TrayMenuEntry.Item("Preferences", () => { }) },
                coordinator,
                new LocalizationService());

            // 内置条目 + 分隔线之后：权重升序，同权重按插件 id 稳定序（A 在 B 之前）。
            Assert.Equal(
                new string?[] { "Preferences", null, "b-early", "a-late", "b-late" },
                entries.Select(entry => entry.Label));
        });
    }

    [Fact]
    public void 点击插件菜单项_执行所属插件注册的命令()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            int executions = 0;
            host.RegisterCommand(new PluginCommandDescriptor("open", "PluginOpen", () => executions++));
            host.RegisterMenuItem(new PluginMenuItemDescriptor("open", "PluginOpen", "open"));

            IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposer.Compose(
                Array.Empty<TrayMenuEntry>(), coordinator, new LocalizationService());

            Assert.Equal(2, entries.Count);
            TrayMenuEntry item = entries[1];
            item.Callback!.Invoke();

            Assert.Equal(1, executions);
        });
    }

    [Fact]
    public void 插件菜单项显示名_优先于宿主文案键()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.RegisterCommand(new PluginCommandDescriptor("open", "NoSuchCommandTitle", () => { }));
            host.RegisterMenuItem(new PluginMenuItemDescriptor("open", "NoSuchPluginMenuTitle", "open")
            {
                DisplayName = "打开示例",
            });

            IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposer.Compose(
                Array.Empty<TrayMenuEntry>(), coordinator, new LocalizationService());

            Assert.Equal("打开示例", entries[1].Label);
        });
    }

    [Fact]
    public void 插件卸载后_菜单项与分隔线一并消失()
    {
        StaTestHarness.Run(() =>
        {
            var coordinator = new PluginUiCoordinator(StaTestHarness.Application, StaTestHarness.Dispatcher);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.RegisterCommand(new PluginCommandDescriptor("open", "PluginOpen", () => { }));
            host.RegisterMenuItem(new PluginMenuItemDescriptor("open", "PluginOpen", "open"));

            host.Release();

            IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposer.Compose(
                new[] { TrayMenuEntry.Item("Preferences", () => { }) },
                coordinator,
                new LocalizationService());

            Assert.Single(entries);
            Assert.Equal("Preferences", entries[0].Label);
        });
    }
}
