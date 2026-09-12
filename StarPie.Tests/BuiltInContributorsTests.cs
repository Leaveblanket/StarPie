using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services;

namespace StarPie.Tests;

/// <summary>
/// 统一注册管线收口：内置贡献者清单（<see cref="BuiltInContributors"/>）是组合根注册期的
/// 唯一遍历对象——Id/Order 唯一且升序、每个贡献者都有真实注册体、导航贡献合并为四槽正典。
/// 与 <see cref="NavigationTests"/>（目录驱动导航行为）、<see cref="NavigationCatalogTests"/>
/// （目录自身收口）互补：本文件锁“管线清单本身”。
/// </summary>
public sealed class BuiltInContributorsTests
{
    private static IReadOnlyList<ICompositionContributor> CreateAll()
        => BuiltInContributors.CreateAll(new AppHostDelegates());

    [Fact]
    public void 内置清单_Id与Order唯一_且按Order升序()
    {
        IReadOnlyList<ICompositionContributor> contributors = CreateAll();

        Assert.NotEmpty(contributors);
        Assert.All(contributors, contributor => Assert.False(string.IsNullOrWhiteSpace(contributor.Id)));
        Assert.Equal(contributors.Count, contributors.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(contributors.Count, contributors.Select(c => c.Order).Distinct().Count());
        Assert.Equal(
            contributors.OrderBy(c => c.Order).Select(c => c.Id),
            contributors.Select(c => c.Id));
    }

    [Fact]
    public void 内置清单_每个贡献者都有服务注册体()
    {
        var services = new ServiceCollection();

        foreach (ICompositionContributor contributor in CreateAll())
        {
            int before = services.Count;
            contributor.RegisterServices(services);
            Assert.True(services.Count > before, $"贡献者 {contributor.Id} 未登记任何服务描述符");
        }
    }

    [Fact]
    public void 内置清单_导航贡献合并为五槽正典()
    {
        var catalog = new NavigationCatalog();
        foreach (ICompositionContributor contributor in CreateAll())
        {
            // 无导航页的贡献者走接口默认空实现：管线对两者是同一入口。
            contributor.RegisterNavigation(catalog);
        }

        catalog.Validate();

        Assert.Equal(
            new[] { 0, 1, 2, 3, 4 },
            catalog.Entries.Select(entry => (int)entry.Slot));
        Assert.Equal(
            new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4" },
            catalog.Entries.Select(entry => entry.AutomationId));
        Assert.Equal(
            new[] { "PageTrigger", "PageAppearance", "PageGestures", "PageAdvanced", "PagePlugins" },
            catalog.Entries.Select(entry => entry.TitleKey));
        Assert.Equal(
            new[]
            {
                typeof(BehaviorSettingsViewModel), typeof(AppearanceSettingsViewModel),
                typeof(ProfileListViewModel), typeof(GeneralSettingsViewModel),
                typeof(PluginManagerViewModel),
            },
            catalog.Entries.Select(entry => entry.ViewModelType));
    }
}
