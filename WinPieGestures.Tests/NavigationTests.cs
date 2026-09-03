using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinPieGestures;
using WinPieGestures.Services;
using WinPieGestures.ViewModels;

namespace WinPieGestures.Tests;

/// <summary>
/// 导航件的行为覆盖 (T19, Spec 测试决策 17)：NavigationStore 当前页状态序列、
/// 泛型导航服务按容器解析切换、主框架 VM 的导航项数据驱动与选中态同步、
/// AboutViewModel 空壳可导航。只测外部行为——CurrentViewModel 的类型序列与选中态，
/// 不测实现细节。直接 new + 替身，不经容器。
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

public sealed class MainViewModelTests
{
    /// <summary>
    /// 五页面 VM 的真实实例夹具：MainViewModel 按具体页面 VM 类型导航，
    /// 用最简依赖构造真实对象（替代 mock 派生，锁定类型精确性）。
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
        public GeneralSettingsViewModel General { get; }
        public AboutViewModel About { get; }

        public PageVmFixture()
        {
            Behavior = new BehaviorSettingsViewModel(Config, Dialogs, Messenger);
            Profiles = new ProfileListViewModel(Config.Profiles, Dialogs, Messenger, Executor);
            Appearance = new AppearanceSettingsViewModel(
                new FakeConfigService { Current = Config }, Dialogs, Messenger, Profiles);
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
                messenger: Messenger);
            About = new AboutViewModel(Dialogs, () => true);
        }
    }

    private sealed class FakeConfigService : IConfigService
    {
        public AppConfig Current { get; set; } = new();
        public void Load() { }
        public void Save() { }
        public WheelProfile GetProfileForProcess(string processName) => Current.Profiles[0];
        public WheelProfile GetGlobalProfile() => Current.Profiles[0];
    }

    /// <summary>类型化导航服务替身：记录导航调用并把 store 切到夹具实例。</summary>
    private sealed class FakeNavigationService<TViewModel> : INavigationService<TViewModel>
        where TViewModel : ObservableObject
    {
        private readonly NavigationStore _store;
        private readonly TViewModel _target;

        public FakeNavigationService(NavigationStore store, TViewModel target)
        {
            _store = store;
            _target = target;
        }

        public int NavigateCalls { get; private set; }

        public void Navigate()
        {
            NavigateCalls++;
            _store.CurrentViewModel = _target;
        }
    }

    private static (MainViewModel Vm, NavigationStore Store, PageVmFixture Fixture) Create()
    {
        var fixture = new PageVmFixture();
        var store = new NavigationStore();
        var vm = new MainViewModel(
            store,
            new FakeNavigationService<BehaviorSettingsViewModel>(store, fixture.Behavior),
            new FakeNavigationService<AppearanceSettingsViewModel>(store, fixture.Appearance),
            new FakeNavigationService<ProfileListViewModel>(store, fixture.Profiles),
            new FakeNavigationService<GeneralSettingsViewModel>(store, fixture.General),
            new FakeNavigationService<AboutViewModel>(store, fixture.About),
            fixture.Messenger,
            fixture.Dialogs);
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

        Assert.Equal(I18n.T("TabTrigger"), vm.NavigationItems[0].Title);
        Assert.Equal(I18n.T("TabAppearance"), vm.NavigationItems[1].Title);
        Assert.Equal(I18n.T("TabGestures"), vm.NavigationItems[2].Title);
        Assert.Equal(I18n.T("TabAdvanced"), vm.NavigationItems[3].Title);
        Assert.Equal(I18n.T("TabAbout"), vm.NavigationItems[4].Title);
    }

    [Fact]
    public void InitialStoreEmpty_NoItemSelectedAndCurrentNull()
    {
        var (vm, store, fixture) = Create();

        Assert.Null(store.CurrentViewModel);
        Assert.Null(vm.CurrentViewModel);
        Assert.All(vm.NavigationItems, i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void IsExiting_DefaultsFalse_AndIsSettable()
    {
        // T22：App 退出状态归壳层 VM（组合根置位、主框架 Closing 读取），View 不反向依赖 Composition。
        var (vm, _, _) = Create();

        Assert.False(vm.IsExiting);

        vm.IsExiting = true;

        Assert.True(vm.IsExiting);
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
        var original = I18n.CurrentLanguage;
        try
        {
            I18n.SetLanguage("en");

            Assert.Equal(I18n.T("TabTrigger"), vm.NavigationItems[0].Title);
            Assert.Equal(I18n.T("TabAbout"), vm.NavigationItems[4].Title);
        }
        finally
        {
            I18n.CurrentLanguage = original;
        }
    }

    [Fact]
    public void WindowTitle_ReflectsI18nAndDevSuffix()
    {
        var (vm, _, _) = Create();

        Assert.Equal(I18n.T("WindowTitle") + DevInstance.Suffix, vm.WindowTitle);
    }

    [Fact]
    public void LanguageChanged_RaisesWindowTitlePropertyChanged_UntilDisposed()
    {
        // T25（ADR-0010 第 3 条）：WindowTitle 并入 RefreshTitles 刷新，Dispose 后不再订阅静态事件。
        var (vm, _, _) = Create();
        var original = I18n.CurrentLanguage;
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowTitle)) changes.Add(e.PropertyName);
        };
        try
        {
            I18n.SetLanguage("en");

            Assert.Contains(nameof(MainViewModel.WindowTitle), changes);
            Assert.Equal(I18n.T("WindowTitle") + DevInstance.Suffix, vm.WindowTitle);

            changes.Clear();
            vm.Dispose();
            I18n.SetLanguage("ja");
            Assert.DoesNotContain(nameof(MainViewModel.WindowTitle), changes);
        }
        finally
        {
            I18n.CurrentLanguage = original;
        }
    }
}
