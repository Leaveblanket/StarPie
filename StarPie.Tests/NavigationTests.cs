using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie;
using StarPie.Modules;
using StarPie.Services;
using StarPie.ViewModels;

namespace StarPie.Tests;

/// <summary>
/// 导航件的行为覆盖：NavigationStore 当前页状态序列、
/// 目录执行缝按槽位解析、主框架 VM 目录驱动的导航项与选中态同步。
/// 只测外部行为——CurrentViewModel 的类型序列与选中态，不测实现细节。直接 new + 替身，不经容器
/// （目录执行缝的容器解析语义用例例外——Host 内部解析缝，用微型容器验证）。
/// </summary>
public sealed class NavigationStoreTests
{
    private sealed class DummyPageViewModel : ObservableObject { }

    [Fact]
    public void Initial_CurrentViewModel_IsNull()
    {
        Assert.Null(new NavigationStore().CurrentViewModel);
    }

    [Fact]
    public void SetCurrentViewModel_RaisesPropertyChangedWithName()
    {
        var store = new NavigationStore();
        var names = new List<string?>();
        store.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        var page = new DummyPageViewModel();
        store.CurrentViewModel = page;

        Assert.Contains(nameof(NavigationStore.CurrentViewModel), names);
        Assert.Same(page, store.CurrentViewModel);
    }

    [Fact]
    public void SetSameInstance_DoesNotRaisePropertyChanged()
    {
        var store = new NavigationStore();
        var page = new DummyPageViewModel();
        store.CurrentViewModel = page;
        var raised = false;
        store.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationStore.CurrentViewModel)) raised = true;
        };

        store.CurrentViewModel = page;

        Assert.False(raised);
    }
}

/// <summary>
/// 导航目录执行缝的行为覆盖：按槽位从目录取注册项并惰性解析页面 VM
/// （容器单例）。微型容器用例覆盖 Host 内部解析缝语义（ADR-0021/#92）。
/// </summary>
public sealed class NavigationExecutorTests
{
    private sealed class TriggerViewModel : ObservableObject { }
    private sealed class AppearanceViewModel : ObservableObject { }

    private static (NavigationExecutor Executor, NavigationStore Store, IServiceProvider Provider) Create()
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
        return (new NavigationExecutor(store, catalog, provider), store, provider);
    }

    [Fact]
    public void Navigate_BySlot_SetsStoreCurrentViewModelToCatalogEntryInstance()
    {
        var (executor, store, provider) = Create();

        executor.Navigate(NavigationSlot.Trigger);

        Assert.Same(provider.GetRequiredService<TriggerViewModel>(), store.CurrentViewModel);
    }

    [Fact]
    public void Navigate_BySlot_ResolvesSingleton_SameInstanceAcrossNavigations()
    {
        var (executor, store, _) = Create();

        executor.Navigate(NavigationSlot.Appearance);
        var first = store.CurrentViewModel;
        store.CurrentViewModel = null;
        executor.Navigate(NavigationSlot.Appearance);

        Assert.Same(first, store.CurrentViewModel);
    }

    [Fact]
    public void Navigate_UnregisteredSlot_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationStore>();
        services.AddSingleton<TriggerViewModel>();
        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<NavigationStore>();
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");
        var executor = new NavigationExecutor(store, catalog, provider);

        Assert.Throws<InvalidOperationException>(() => executor.Navigate(NavigationSlot.Advanced));
    }
}

public sealed class MainViewModelTests
{
    private static readonly LocalizationService Localization = new();

    /// <summary>
    /// 四页面 VM 的真实实例夹具：导航项按目录注册的目标 VM 类型切换，用最简依赖构造真实对象
    /// （替代 mock 派生，锁定类型精确性）。
    /// </summary>
    private sealed class PageVmFixture
    {
        public WeakReferenceMessenger Messenger { get; } = TestHub.NewMessenger();
        public TestDialogService Dialogs { get; } = new();
        public TestActionExecutor Executor { get; } = new();
        public AppConfig Config { get; } = new()
        {
            Profiles = new List<WheelProfile> { new WheelProfile { ProcessName = "Global", SectorCount = 8 } }
        };

        public BehaviorSettingsViewModel Behavior { get; }
        public ProfileListViewModel Profiles { get; }
        public AppearanceSettingsViewModel Appearance { get; }
        public InterfaceThemeSettingsViewModel InterfaceTheme { get; }
        public WheelAppearanceSettingsViewModel WheelAppearance { get; }
        public GeneralSettingsViewModel General { get; }

        public PageVmFixture()
        {
            Behavior = new BehaviorSettingsViewModel(Config, Dialogs, Messenger);
            Profiles = new ProfileListViewModel(Config.Profiles, Dialogs, Messenger, Executor, Localization, new TestIconAssetService());
            var configService = new TestConfigService { Current = Config };
            InterfaceTheme = new InterfaceThemeSettingsViewModel(configService, Messenger, Localization);
            WheelAppearance = new WheelAppearanceSettingsViewModel(
                configService, Dialogs, Messenger, Profiles, Localization);
            Appearance = new AppearanceSettingsViewModel(Messenger, InterfaceTheme, WheelAppearance, new TestIconAssetService());
            General = new GeneralSettingsViewModel(
                Config,
                Dialogs,
                (_, _) => { },
                () => { },
                () => false,
                _ => { },
                _ => true,
                _ => true,
                currentConfig: () => Config,
                messenger: Messenger,
                localization: Localization);
        }
    }

    /// <summary>目录执行缝替身：记录槽位导航调用并把 store 切到夹具对应页面实例。</summary>
    private sealed class FakeNavigationExecutor : INavigationExecutor
    {
        private readonly NavigationStore _store;
        private readonly IReadOnlyDictionary<NavigationSlot, ObservableObject> _targets;
        private readonly IReadOnlyDictionary<string, ObservableObject> _targetsByIdentifier;

        public FakeNavigationExecutor(
            NavigationStore store,
            IReadOnlyDictionary<NavigationSlot, ObservableObject> targets,
            IReadOnlyDictionary<string, ObservableObject>? targetsByIdentifier = null)
        {
            _store = store;
            _targets = targets;
            _targetsByIdentifier = targetsByIdentifier ?? targets.ToDictionary(
                pair => NavigationSlots.GetAutomationId(pair.Key),
                pair => pair.Value);
        }

        public int NavigateCalls { get; private set; }

        public void Navigate(NavigationSlot slot)
        {
            NavigateCalls++;
            _store.CurrentViewModel = _targets[slot];
        }

        public void Navigate(string identifier)
        {
            NavigateCalls++;
            _store.CurrentViewModel = _targetsByIdentifier[identifier];
        }
    }

    private static (MainViewModel Vm, NavigationStore Store, PageVmFixture Fixture) Create()
    {
        var (vm, store, fixture, _) = CreateCore();
        return (vm, store, fixture);
    }

    private static (MainViewModel Vm, NavigationStore Store, PageVmFixture Fixture, FakeNavigationExecutor Navigation) CreateCore()
    {
        var fixture = new PageVmFixture();
        var store = new NavigationStore();

        // 目录由生产内置贡献者清单装配（内置清单 + 注册管线，与组合根同一入口）——测试同时
        // 锁定真实槽位表（顺序/标识/标题键/图标/目标类型）；替身只代目录执行缝。
        var catalog = new NavigationCatalog();
        foreach (ICompositionContributor contributor in BuiltInContributors.CreateAll(new AppHostDelegates()))
        {
            contributor.RegisterNavigation(catalog);
        }
        catalog.Validate();

        var navigation = new FakeNavigationExecutor(store, new Dictionary<NavigationSlot, ObservableObject>
        {
            [NavigationSlot.Trigger] = fixture.Behavior,
            [NavigationSlot.Appearance] = fixture.Appearance,
            [NavigationSlot.Gestures] = fixture.Profiles,
            [NavigationSlot.Advanced] = fixture.General
        });
        var vm = new MainViewModel(store, catalog, navigation, Localization);
        return (vm, store, fixture, navigation);
    }

    [Fact]
    public void Items_AreFiveInNavigationOrder()
    {
        var (vm, _, _) = Create();

        Assert.Equal(5, vm.NavigationItems.Count);
        Assert.Equal(new[]
        {
            typeof(BehaviorSettingsViewModel), typeof(AppearanceSettingsViewModel), typeof(ProfileListViewModel),
            typeof(GeneralSettingsViewModel), typeof(PluginManagerViewModel)
        }, vm.NavigationItems.Select(i => i.TargetViewModelType));
        Assert.Equal(new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4" },
            vm.NavigationItems.Select(i => i.AutomationId));
    }

    [Fact]
    public void Items_TitlesReflectI18n()
    {
        var (vm, _, _) = Create();

        Assert.Equal(Localization.GetString("PageTrigger"), vm.NavigationItems[0].Title);
        Assert.Equal(Localization.GetString("PageAppearance"), vm.NavigationItems[1].Title);
        Assert.Equal(Localization.GetString("PageGestures"), vm.NavigationItems[2].Title);
        Assert.Equal(Localization.GetString("PageAdvanced"), vm.NavigationItems[3].Title);
        Assert.Equal(Localization.GetString("PagePlugins"), vm.NavigationItems[4].Title);
    }

    [Fact]
    public void InitialStoreEmpty_NoItemSelectedAndCurrentNull()
    {
        var (vm, store, _) = Create();

        Assert.Null(store.CurrentViewModel);
        Assert.Null(vm.CurrentViewModel);
        Assert.All(vm.NavigationItems, i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void NavigateViaItemCommand_SwitchesCurrentViewModel_AndMarksItemSelected()
    {
        var (vm, store, fixture) = Create();

        vm.NavigationItems[1].NavigateCommand.Execute(null);

        Assert.Same(fixture.Appearance, store.CurrentViewModel);
        Assert.Same(fixture.Appearance, vm.CurrentViewModel);
        Assert.False(vm.NavigationItems[0].IsSelected);
        Assert.True(vm.NavigationItems[1].IsSelected);
    }

    [Fact]
    public void ItemSelectedExternally_NavigatesToTargetPage()
    {
        // UIA SelectionItem.Select（e2e 静默导航路径）只置选中态、不产生鼠标输入；
        // 导航由选中态驱动，点击命令与选中态两条路径等价。
        var (vm, store, fixture, navigation) = CreateCore();

        vm.NavigationItems[2].IsSelected = true;

        Assert.Same(fixture.Profiles, store.CurrentViewModel);
        Assert.Equal(1, navigation.NavigateCalls);
    }

    [Fact]
    public void SelectionSyncedFromStore_DoesNotRenavigate()
    {
        // SyncSelection 回灌的选中态指向已停驻的页面，不得触发二次导航（防回环）。
        var (vm, store, fixture, navigation) = CreateCore();

        store.CurrentViewModel = fixture.General;

        Assert.True(vm.NavigationItems[3].IsSelected);
        Assert.Equal(0, navigation.NavigateCalls);
    }

    [Fact]
    public void StoreChangedExternally_IsSelectedFollowsCurrentPage()
    {
        var (vm, store, fixture) = Create();

        store.CurrentViewModel = fixture.General;

        Assert.True(vm.NavigationItems[3].IsSelected);
        Assert.All(vm.NavigationItems.Take(3), i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void LanguageChanged_RefreshesItemTitles()
    {
        var (vm, _, _) = Create();
        var original = Localization.CurrentLanguage;
        try
        {
            Localization.SetLanguage("en");

            Assert.Equal(Localization.GetString("PageTrigger"), vm.NavigationItems[0].Title);
            Assert.Equal(Localization.GetString("PageAdvanced"), vm.NavigationItems[3].Title);
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void NavigationCommand_InvokesExecutorByCatalogSlot()
    {
        var (vm, store, fixture) = Create();

        // 目录注册槽位与点击项的 AutomationId 一一对应：NavPage3（槽位 Advanced）→ 高级页 VM。
        vm.NavigationItems[3].NavigateCommand.Execute(null);

        Assert.Same(fixture.General, store.CurrentViewModel);
        Assert.True(vm.NavigationItems[3].IsSelected);
    }
}

/// <summary>
/// 插件页在控制台导航区的动态呈现：目录增页即出现在固定页之后，目录摘页即消失，
/// 选中态与当前页保持同步；插件页导航经注册工厂创建页面 VM。
/// </summary>
public sealed class MainViewModelPluginPageTests
{
    private static readonly LocalizationService Localization = new();

    private sealed class PluginPageViewModel : ObservableObject { }
    private sealed class TriggerPageViewModel : ObservableObject { }
    private sealed class AppearancePageViewModel : ObservableObject { }
    private sealed class GesturesPageViewModel : ObservableObject { }
    private sealed class AdvancedPageViewModel : ObservableObject { }
    private sealed class PluginsPageViewModel : ObservableObject { }

    private static NavigationCatalog CreateCatalog()
    {
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerPageViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");
        catalog.RegisterPage<AppearancePageViewModel>(
            NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "PageAppearance", "");
        catalog.RegisterPage<GesturesPageViewModel>(
            NavigationSlot.Gestures, NavigationSlots.GetAutomationId(NavigationSlot.Gestures), "PageGestures", "");
        catalog.RegisterPage<AdvancedPageViewModel>(
            NavigationSlot.Advanced, NavigationSlots.GetAutomationId(NavigationSlot.Advanced), "PageAdvanced", "");
        catalog.RegisterPage<PluginsPageViewModel>(
            NavigationSlot.Plugins, NavigationSlots.GetAutomationId(NavigationSlot.Plugins), "PagePlugins", "");
        return catalog;
    }

    private static (MainViewModel Vm, NavigationCatalog Catalog, NavigationStore Store) Create()
    {
        var catalog = CreateCatalog();
        var store = new NavigationStore();
            var provider = new ServiceCollection()
                .AddSingleton(store)
                .AddSingleton(catalog)
                // 固定页 VM：与生产容器同形的可解析目标，供插件页摘除后的回落导航使用。
                .AddSingleton<TriggerPageViewModel>()
                .AddSingleton<AppearancePageViewModel>()
                .AddSingleton<GesturesPageViewModel>()
            .AddSingleton<AdvancedPageViewModel>()
            .AddSingleton<PluginsPageViewModel>()
            .BuildServiceProvider();
        var executor = new NavigationExecutor(store, catalog, provider);
        return (new MainViewModel(store, catalog, executor, Localization), catalog, store);
    }

    [Fact]
    public void 目录注册插件页_导航项追加在固定页之后()
    {
        var (vm, catalog, _) = Create();

        catalog.RegisterPluginPage(
            "com.example.ui", "NavPlugin_com.example.ui", "NavPlugin_com.example.ui", "PluginPage", "",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        Assert.Equal(6, vm.NavigationItems.Count);
        Assert.Equal("NavPlugin_com.example.ui", vm.NavigationItems[5].AutomationId);
    }

    [Fact]
    public void 目录摘除插件页_导航项随之移除且固定页不动()
    {
        var (vm, catalog, _) = Create();
        catalog.RegisterPluginPage(
            "com.example.ui", "NavPlugin_com.example.ui", "NavPlugin_com.example.ui", "PluginPage", "",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        catalog.RemovePluginPage("NavPlugin_com.example.ui");

        Assert.Equal(5, vm.NavigationItems.Count);
        Assert.Equal(
            new[] { "NavPage0", "NavPage1", "NavPage2", "NavPage3", "NavPage4" },
            vm.NavigationItems.Select(item => item.AutomationId));
    }

    [Fact]
    public void 插件页导航_经目录工厂创建页面并选中对应项()
    {
        var (vm, catalog, store) = Create();
        catalog.RegisterPluginPage(
            "com.example.ui", "NavPlugin_com.example.ui", "NavPlugin_com.example.ui", "PluginPage", "",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());

        vm.NavigationItems[5].NavigateCommand.Execute(null);

        Assert.IsType<PluginPageViewModel>(store.CurrentViewModel);
        Assert.True(vm.NavigationItems[5].IsSelected);
    }

    [Fact]
    public void 插件页被摘除时_当前页回落到固定页而不滞留已卸载页面()
    {
        var (vm, catalog, store) = Create();
        catalog.RegisterPluginPage(
            "com.example.ui", "NavPlugin_com.example.ui", "NavPlugin_com.example.ui", "PluginPage", "",
            typeof(PluginPageViewModel), () => new PluginPageViewModel());
        vm.NavigationItems[5].NavigateCommand.Execute(null);
        Assert.IsType<PluginPageViewModel>(store.CurrentViewModel);

        catalog.RemovePluginPage("NavPlugin_com.example.ui");

        Assert.IsType<TriggerPageViewModel>(store.CurrentViewModel);
        Assert.True(vm.NavigationItems[0].IsSelected);
    }
}
