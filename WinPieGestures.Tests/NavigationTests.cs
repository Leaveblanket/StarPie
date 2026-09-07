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
/// 泛型导航服务按容器解析切换、目录执行缝按槽位解析、主框架 VM 目录驱动的导航项与选中态同步。
/// 只测外部行为——CurrentViewModel 的类型序列与选中态，不测实现细节。直接 new + 替身，不经容器
/// （导航服务与目录执行缝的容器解析语义用例例外——已批准解析缝，用微型容器验证）。
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

public sealed class NavigationServiceTests
{
    private sealed class PageAViewModel : ObservableObject { }
    private sealed class PageBViewModel : ObservableObject { }

    private static (NavigationStore Store, IServiceProvider Services) CreateHub(Action<ServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationStore>();
        services.AddSingleton<PageAViewModel>();
        services.AddSingleton<PageBViewModel>();
        extra?.Invoke(services);
        return (new NavigationStore(), services.BuildServiceProvider());
    }

    [Fact]
    public void Navigate_SetsStoreCurrentViewModelToResolvedInstance()
    {
        var (store, provider) = CreateHub();
        var service = new NavigationService<PageAViewModel>(store, provider);

        service.Navigate();

        Assert.IsType<PageAViewModel>(store.CurrentViewModel);
    }

    [Fact]
    public void Navigate_ResolvesSingleton_SameInstanceAcrossNavigations()
    {
        var (store, provider) = CreateHub();
        var service = new NavigationService<PageAViewModel>(store, provider);

        service.Navigate();
        var first = store.CurrentViewModel;
        store.CurrentViewModel = null;
        service.Navigate();

        Assert.Same(first, store.CurrentViewModel);
    }

    [Fact]
    public void Navigate_TypeSequence_SwitchesBetweenPageTypes()
    {
        var (store, provider) = CreateHub();
        var toA = new NavigationService<PageAViewModel>(store, provider);
        var toB = new NavigationService<PageBViewModel>(store, provider);

        toA.Navigate();
        toB.Navigate();

        Assert.IsType<PageBViewModel>(store.CurrentViewModel);
        Assert.IsNotType<PageAViewModel>(store.CurrentViewModel);
    }
}

/// <summary>
/// 导航目录执行缝的行为覆盖：按槽位从目录取注册项并惰性解析页面 VM
/// （容器单例）。微型容器用例与 <see cref="NavigationServiceTests"/> 同属已批准解析缝验证。
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
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabTrigger", "");
        catalog.RegisterPage<AppearanceViewModel>(
            NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "TabAppearance", "");
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
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "TabTrigger", "");
        var executor = new NavigationExecutor(store, catalog, provider);

        Assert.Throws<InvalidOperationException>(() => executor.Navigate(NavigationSlot.Advanced));
    }
}

public sealed class MainViewModelTests
{
    private static readonly LocalizationService Localization = new();

    /// <summary>
    /// 五页面 VM 的真实实例夹具：导航项按目录注册的目标 VM 类型切换，用最简依赖构造真实对象
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
        public AboutViewModel About { get; }

        public PageVmFixture()
        {
            Behavior = new BehaviorSettingsViewModel(Config, Dialogs, Messenger);
            Profiles = new ProfileListViewModel(Config.Profiles, Dialogs, Messenger, Executor, Localization);
            var configService = new TestConfigService { Current = Config };
            InterfaceTheme = new InterfaceThemeSettingsViewModel(configService, Messenger, Localization);
            WheelAppearance = new WheelAppearanceSettingsViewModel(
                configService, Dialogs, Messenger, Profiles, Localization);
            Appearance = new AppearanceSettingsViewModel(Messenger, InterfaceTheme, WheelAppearance);
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
            About = new AboutViewModel(Dialogs, () => true, Localization);
        }
    }

    /// <summary>目录执行缝替身：记录槽位导航调用并把 store 切到夹具对应页面实例。</summary>
    private sealed class FakeNavigationExecutor : INavigationExecutor
    {
        private readonly NavigationStore _store;
        private readonly IReadOnlyDictionary<NavigationSlot, ObservableObject> _targets;

        public FakeNavigationExecutor(
            NavigationStore store,
            IReadOnlyDictionary<NavigationSlot, ObservableObject> targets)
        {
            _store = store;
            _targets = targets;
        }

        public int NavigateCalls { get; private set; }

        public void Navigate(NavigationSlot slot)
        {
            NavigateCalls++;
            _store.CurrentViewModel = _targets[slot];
        }
    }

    private static (MainViewModel Vm, NavigationStore Store, PageVmFixture Fixture) Create()
    {
        var fixture = new PageVmFixture();
        var store = new NavigationStore();

        // 目录由生产模块注册器装配（Gestures/Shell/Host 三注册器）——测试同时锁定真实
        // 槽位表（顺序/标识/标题键/图标/目标类型）；替身只代目录执行缝。
        var catalog = new NavigationCatalog();
        GesturesModuleRegistrar.RegisterNavigation(catalog);
        ShellModuleRegistrar.RegisterNavigation(catalog);
        HostModuleRegistrar.RegisterNavigation(catalog);
        catalog.Validate();

        var navigation = new FakeNavigationExecutor(store, new Dictionary<NavigationSlot, ObservableObject>
        {
            [NavigationSlot.Trigger] = fixture.Behavior,
            [NavigationSlot.Appearance] = fixture.Appearance,
            [NavigationSlot.Gestures] = fixture.Profiles,
            [NavigationSlot.Advanced] = fixture.General,
            [NavigationSlot.About] = fixture.About
        });
        var vm = new MainViewModel(store, catalog, navigation, Localization);
        return (vm, store, fixture);
    }

    [Fact]
    public void Items_AreFiveInNavigationOrder()
    {
        var (vm, _, _) = Create();

        Assert.Equal(5, vm.NavigationItems.Count);
        Assert.Equal(new[]
        {
            typeof(BehaviorSettingsViewModel), typeof(AppearanceSettingsViewModel), typeof(ProfileListViewModel),
            typeof(GeneralSettingsViewModel), typeof(AboutViewModel)
        }, vm.NavigationItems.Select(i => i.TargetViewModelType));
        Assert.Equal(new[] { "NavTab0", "NavTab1", "NavTab2", "NavTab3", "NavTab4" },
            vm.NavigationItems.Select(i => i.AutomationId));
    }

    [Fact]
    public void Items_TitlesReflectI18n()
    {
        var (vm, _, _) = Create();

        Assert.Equal(Localization.GetString("TabTrigger"), vm.NavigationItems[0].Title);
        Assert.Equal(Localization.GetString("TabAppearance"), vm.NavigationItems[1].Title);
        Assert.Equal(Localization.GetString("TabGestures"), vm.NavigationItems[2].Title);
        Assert.Equal(Localization.GetString("TabAdvanced"), vm.NavigationItems[3].Title);
        Assert.Equal(Localization.GetString("TabAbout"), vm.NavigationItems[4].Title);
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
    public void StoreChangedExternally_IsSelectedFollowsCurrentPage()
    {
        var (vm, store, fixture) = Create();

        store.CurrentViewModel = fixture.About;

        Assert.True(vm.NavigationItems[4].IsSelected);
        Assert.All(vm.NavigationItems.Take(4), i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void LanguageChanged_RefreshesItemTitles()
    {
        var (vm, _, _) = Create();
        var original = Localization.CurrentLanguage;
        try
        {
            Localization.SetLanguage("en");

            Assert.Equal(Localization.GetString("TabTrigger"), vm.NavigationItems[0].Title);
            Assert.Equal(Localization.GetString("TabAbout"), vm.NavigationItems[4].Title);
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

        // 目录注册槽位与点击项的 AutomationId 一一对应：NavTab4（槽位 About）→ About VM。
        vm.NavigationItems[4].NavigateCommand.Execute(null);

        Assert.Same(fixture.About, store.CurrentViewModel);
        Assert.True(vm.NavigationItems[4].IsSelected);
    }
}
