using System;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace StarPie.Tests;

/// <summary>
/// 导航视图出账与恢复重放测试：出账置空当前页并记录槽位；恢复重放按最后
/// 槽位命中目录回填当前页（选中态随 Store 变更由导航区回灌）；lastSlot 为空不重放；
/// 重复出账 no-op；插件页 VM（工厂新建型）随出账真实回收（WeakReference 判定）。
/// </summary>
public sealed class NavigationSuspensionTests
{
    private sealed class TriggerViewModel : ObservableObject { }
    private sealed class AppearanceViewModel : ObservableObject { }

    /// <summary>插件页 VM：非容器单例（工厂新建型），出账后应真实回收。</summary>
    private sealed class PluginPageViewModel : ObservableObject { }

    private static (NavigationSuspension Suspension, NavigationExecutor Executor, NavigationStore Store, NavigationCatalog Catalog, IServiceProvider Provider)
        Create(Func<PluginPageViewModel>? pluginPageFactory = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationStore>();
        services.AddSingleton<TriggerViewModel>();
        services.AddSingleton<AppearanceViewModel>();
        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<NavigationStore>();
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");
        catalog.RegisterPage<AppearanceViewModel>(
            NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "PageAppearance", "");
        catalog.RegisterPluginPage(
            "com.example.test", "NavPlugin_com.example.test",
            "NavPlugin_com.example.test", "PluginPageTitle", "",
            typeof(PluginPageViewModel),
            () => (object)(pluginPageFactory?.Invoke() ?? new PluginPageViewModel()));
        var session = new ConsolePageSession(provider.CreateScope);
        session.Begin();
        var executor = new NavigationExecutor(store, catalog, session);
        return (new NavigationSuspension(store, catalog, executor), executor, store, catalog, provider);
    }

    [Fact]
    public void 出账_当前页置空并记录最后导航槽位()
    {
        var (suspension, executor, store, _, _) = Create();
        executor.Navigate(NavigationSlot.Appearance);

        Assert.True(suspension.Release());
        Assert.Null(store.CurrentViewModel);
        Assert.Equal(NavigationSlots.GetAutomationId(NavigationSlot.Appearance), suspension.PendingIdentifier);
    }

    [Fact]
    public void 恢复重放_按最后导航槽位命中目录()
    {
        var (suspension, executor, store, _, _) = Create();
        executor.Navigate(NavigationSlot.Appearance);
        suspension.Release();

        Assert.True(suspension.Restore());
        Assert.IsType<AppearanceViewModel>(store.CurrentViewModel);
        Assert.Null(suspension.PendingIdentifier);
    }

    [Fact]
    public void 恢复重放_插件页按标识命中工厂()
    {
        var (suspension, executor, store, _, _) = Create();
        executor.Navigate("NavPlugin_com.example.test");
        Assert.IsType<PluginPageViewModel>(store.CurrentViewModel);
        suspension.Release();

        Assert.True(suspension.Restore());
        Assert.IsType<PluginPageViewModel>(store.CurrentViewModel);
    }

    [Fact]
    public void lastSlot为空_恢复不重放()
    {
        var (suspension, executor, store, _, _) = Create();

        // 从未出账：恢复 no-op
        Assert.False(suspension.Restore());
        Assert.Null(store.CurrentViewModel);

        // 已重放后再恢复:同样 no-op(不重复导航)
        executor.Navigate(NavigationSlot.Trigger);
        suspension.Release();
        Assert.True(suspension.Restore());
        Assert.False(suspension.Restore());
        Assert.Null(suspension.PendingIdentifier);
    }

    [Fact]
    public void 重复出账_幂等_不覆盖已记录槽位()
    {
        var (suspension, executor, _, _, _) = Create();
        executor.Navigate(NavigationSlot.Appearance);
        Assert.True(suspension.Release());

        Assert.False(suspension.Release());
        Assert.Equal(NavigationSlots.GetAutomationId(NavigationSlot.Appearance), suspension.PendingIdentifier);
    }

    [Fact]
    public void 出账恢复_导航选中态保持()
    {
        var (suspension, executor, store, catalog, _) = Create();
        executor.Navigate(NavigationSlot.Appearance);
        var main = new MainViewModel(store, catalog, executor, new LocalizationService());
        NavigationItemViewModel item = main.NavigationItems.First(i => i.TargetViewModelType == typeof(AppearanceViewModel));
        Assert.True(item.IsSelected);

        // 出账：当前页置空，全部导航项选中态清空
        suspension.Release();
        Assert.False(item.IsSelected);

        // 恢复重放：选中态按目录回灌，保持出账前的选中页
        suspension.Restore();
        Assert.True(item.IsSelected);
        Assert.IsType<AppearanceViewModel>(store.CurrentViewModel);
    }

    [Fact]
    public void 插件页VM随出账真实回收_无静态根()
    {
        var reference = NavigatePluginPageThenRelease();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.IsAlive, "插件页 VM（工厂新建型）随出账应真实回收，出现滞留");
    }

    /// <summary>
    /// 导航到插件页后出账，只把弱引用带回调用方：强引用全部留在本方法栈上，返回即消失
    /// ——同 <c>ConsolePageSessionTests</c> 的处理。留在断言方法里时，优化后的 JIT 会把
    /// "已置空"的局部变量当作死存储丢掉、旧值继续占着栈槽，断言在 Release 下假红。
    /// </summary>
    private static WeakReference NavigatePluginPageThenRelease()
    {
        PluginPageViewModel? created = null;
        var (suspension, executor, _, _, _) = Create(pluginPageFactory: () => created = new PluginPageViewModel());

        executor.Navigate("NavPlugin_com.example.test");
        PluginPageViewModel? instance = created;
        Assert.NotNull(instance);

        // 出账：当前页引用从 NavigationStore 摘除（工厂新建型 VM 无其它根，应可回收）
        Assert.True(suspension.Release());

        var reference = new WeakReference(instance!);
        // 工厂闭包被目录长期持有，其捕获字段仍指向实例——置空才是"无其它根"的前提
        created = null;
        instance = null;
        return reference;
    }
}
