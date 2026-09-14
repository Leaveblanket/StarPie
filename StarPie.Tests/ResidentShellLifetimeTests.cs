using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Media;
using System.Windows.Threading;
using StarPie.Services.Programs;
using StarPie.ViewModels.Navigation;
using StarPie.Views.Navigation;

namespace StarPie.Tests;

/// <summary>
/// 常驻壳层与瞬态设置台的窗口生命周期正确性（#156，ADR-0039 决策 1/5/6/7/8）：
/// INV1 常驻期全局窗口集合只剩常驻锚窗口；INV2 关闭后窗口脱离全局窗口集合并卸载、会话级 VM 弱引用死亡；
/// INV3 无设置台时 <see cref="Application.MainWindow"/> 指向锚窗口，绝不指向已关闭的瞬态窗口；
/// 外加瞬态窗口收尾纪律的行为与"零 WPF 窗口状态下 UI 线程消息泵仍在"的地基假设。
/// </summary>
/// <remarks>
/// 用例跑在 <see cref="StaTestHarness"/> 的真实 WPF 对象上；设置台窗口在离屏位置显示——
/// 布局/渲染/视觉树真实发生，但不占用用户屏幕。全局窗口集合断言取基线差集：测试进程内共享
/// Application，其它用例留下的窗口不是本用例的判据。
/// <para>
/// <b>已知边界（实测，非本设计引入）</b>：本类不判定"关闭后窗口与视图树对象可回收"。
/// 已显示窗口经 <c>Close()</c> 后 HWND 已销毁、已退出全局窗口集合、主窗口属性已改写，
/// 但该 <see cref="Window"/> 托管对象仍被强引用；<c>x:Name</c> 生成字段随窗口对象存活，
/// 故窗口的视图树与 DataContext 也随之可达。实测对照：从未显示的窗口、或在同一 Dispatcher
/// 操作内创建并关闭的窗口可回收，跨 Dispatcher 操作显示并关闭的窗口不可回收；
/// 自 WPF 程序集静态根做字段/集合/委托目标 BFS（深度 8）与 Dispatcher 队列检查均未定位到持有者，
/// 故不在本层断言，改由结构不变量（INV1/INV3）与 #159 的常驻内存测量承担。
/// </para>
/// </remarks>
public sealed class ResidentShellLifetimeTests
{
    /// <summary>离屏定位：窗口真实显示，但不闪现在用户屏幕上。</summary>
    private const double OffScreen = -32000;

    /// <summary>App 级资源字典是否已并入测试 Application（进程内一次）。</summary>
    private static bool _applicationResourcesMerged;

    /// <summary>
    /// 把 Ui 集的 App 级资源字典并入测试 Application：壳窗口按 App.xaml 的合并清单取样式
    /// （主题默认画刷 + 全局控件样式 + 热键录制控件样式），裸 <see cref="Application"/>
    /// 下 StaticResource 查找会失败。合并清单与 App.xaml 保持一致。
    /// </summary>
    private static void EnsureApplicationResources()
    {
        if (_applicationResourcesMerged || Application.Current is not { } application)
        {
            return;
        }

        _applicationResourcesMerged = true;
        foreach (string source in new[]
                 {
                     "pack://application:,,,/StarPie;component/Themes/Light.xaml",
                     "pack://application:,,,/StarPie;component/Views/Styles/ModernControls.xaml",
                     "pack://application:,,,/StarPie;component/Views/Styles/HotkeyRecorderBox.xaml",
                 })
        {
            application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
        }
    }

    // ==== 被测对象的装配（直接 new + 替身，不经容器）====

    private sealed class NoShortcutResolver : IShortcutTargetResolver
    {
        public bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex)
        {
            targetPath = "";
            iconPath = "";
            iconIndex = 0;
            return false;
        }
    }

    private sealed class EmptyProgramScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms() => Array.Empty<ProgramEntry>();
    }

    /// <summary>夹具页面 VM：会话作用域内的唯一解析目标。</summary>
    private sealed class TriggerPageViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void Raise() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(null));
    }

    /// <summary>会话作用域替身：只解析夹具的页面 VM（夹具不经容器）。</summary>
    private static IServiceScope NewSessionScope() => new FixtureScope();

    private sealed class FixtureScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FixtureServiceProvider();

        public void Dispose() { }
    }

    private sealed class FixtureServiceProvider : IServiceProvider
    {
        private readonly TriggerPageViewModel _page = new();

        public object? GetService(Type serviceType)
            => serviceType == typeof(TriggerPageViewModel) ? _page : null;
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentEffectiveTheme => "Light";

        public void SetTheme(string themeName) { }

        public void ApplyWindowTheme(FrameworkElement? rootElement) { }

        public string ResolveEffectiveTheme(string themeName)
            => string.IsNullOrEmpty(themeName) || themeName == "System" ? "Light" : themeName;

        public bool IsWindowsInDarkTheme() => false;
    }

    /// <summary>一次设置台装配：租户本体与窗口/视图（会话对象，用例直接观察）+ 常驻事件源 + 会话 VM 弱引用。</summary>
    private sealed class ConsoleFixture
    {
        public required SettingsConsole Console { get; init; }
        public required Window Anchor { get; init; }
        public required MainView View { get; init; }
        public required SidebarView Sidebar { get; init; }
        public required DialogService Dialogs { get; init; }
        public required MainViewModel MainViewModel { get; init; }
        public required ShellViewModel ShellViewModel { get; init; }
        public required NavigationStore Store { get; init; }
        public required INavigationExecutor Navigation { get; init; }
    }

    private static ConsoleFixture CreateFixture()
    {
        EnsureApplicationResources();
        var localization = new LocalizationService();
        var config = new TestConfigService();
        var messenger = TestHub.NewMessenger();
        var iconAssets = new TestIconAssetService();
        var catalog = new NavigationCatalog();
        catalog.RegisterPage<TriggerPageViewModel>(
            NavigationSlot.Trigger, NavigationSlots.GetAutomationId(NavigationSlot.Trigger), "PageTrigger", "");
        catalog.RegisterPage<TriggerPageViewModel>(
            NavigationSlot.Appearance, NavigationSlots.GetAutomationId(NavigationSlot.Appearance), "PageAppearance", "");
        var store = new NavigationStore();
        var session = new ConsolePageSession(NewSessionScope);
        session.Begin();
        var executor = new NavigationExecutor(store, catalog, session);
        var dialogs = new DialogService(
            new FakeThemeService(),
            localization,
            iconAssets,
            new NoShortcutResolver(),
            new EmptyProgramScanner());
        var saveOrchestrator = new SettingsSaveOrchestrator(config, new TestSaveDebouncer(), messenger);
        var interfaceTheme = new InterfaceThemeSettingsViewModel(config, messenger, localization);

        var anchor = new ShellAnchorWindow();
        if (Application.Current is { } application)
        {
            application.MainWindow = anchor;
        }

        var main = new MainViewModel(store, catalog, executor, localization);
        var shell = new ShellViewModel(messenger, dialogs, localization);
        var console = new SettingsConsole(
            main,
            shell,
            new FakeThemeService(),
            dialogs,
            interfaceTheme,
            saveOrchestrator,
            iconAssets,
            new NavigationSuspension(store, catalog, executor),
            messenger,
            anchor,
            background: false,
            session,
            isExiting: () => false);

        console.Show();

        // 显示即离屏：窗口在消息循环恢复前移出屏幕，不闪现在用户屏幕上。
        var view = (MainView)Application.Current.MainWindow!;
        view.Left = OffScreen;
        view.Top = OffScreen;
        view.UpdateLayout();

        // 侧栏视图在 MainView.xaml 里直接实例化（不依赖 App 级页面模板字典），视觉树中就位。
        SidebarView sidebar = FindDescendant<SidebarView>(view)
            ?? throw new InvalidOperationException("侧栏视图未出现在壳窗口视觉树中");

        return new ConsoleFixture
        {
            Console = console,
            Anchor = anchor,
            View = view,
            Sidebar = sidebar,
            Dialogs = dialogs,
            MainViewModel = main,
            ShellViewModel = shell,
            Store = store,
            Navigation = executor,
        };
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static void CollectAll()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static Window[] ResidentWindows()
        => StaTestHarness.Run(() => Application.Current.Windows.OfType<Window>().ToArray());

    // ==== INV1：常驻期全局窗口集合只剩锚窗口 ====

    [Fact]
    public void 关闭设置台后_全局窗口集合回到基线_只剩常驻锚窗口()
    {
        var fixture = StaTestHarness.Run(CreateFixture);
        Window[] before = ResidentWindows();
        Assert.Contains(fixture.View, before);

        StaTestHarness.Run(fixture.Console.Dispose);

        Window[] after = ResidentWindows();
        Assert.DoesNotContain(fixture.View, after);
        Assert.Contains(fixture.Anchor, after);
        // 基线差集：关闭前后只差设置台窗口本身（无新增瞬态窗口、无残留）。
        Assert.Equal(before.Where(window => !ReferenceEquals(window, fixture.View)), after);
    }

    // ==== INV2：关闭后窗口出账、视图树卸载、会话 VM 弱引用死亡 ====

    [Fact]
    public void 关闭设置台后_窗口已关闭并脱离全局窗口集合_设置台不再开着()
    {
        var fixture = StaTestHarness.Run(CreateFixture);
        Assert.True(StaTestHarness.Run(() => fixture.View.IsLoaded));
        Assert.True(StaTestHarness.Run(() => fixture.Sidebar.IsLoaded));

        StaTestHarness.Run(fixture.Console.Dispose);

        Assert.False(fixture.Console.IsOpen, "关闭后设置台仍报告开着");
        Assert.False(StaTestHarness.Run(() => fixture.View.IsLoaded), "关闭后窗口仍是已加载状态");
        Assert.False(StaTestHarness.Run(() => fixture.Sidebar.IsLoaded), "关闭后视图树仍挂在已加载树上");
        Assert.DoesNotContain(fixture.View, ResidentWindows());
    }

    [Fact]
    public void 直接关窗_设置台自动收敛释放_壳层据IsOpen判定重开()
    {
        // 关窗路径不止"壳层调 Close"：关闭按钮/Alt+F4/WM_CLOSE 都只关窗口。设置台必须自己观测
        // Closed 收敛到释放，否则壳层持有的设置台会停在已关闭窗口上——重开时既开不了新窗
        //（已关闭的窗口不能再 Show）也释放不了旧对象图。
        var fixture = StaTestHarness.Run(CreateFixture);

        StaTestHarness.Run(() => fixture.View.Close());

        Assert.False(fixture.Console.IsOpen, "窗口已关闭，设置台必须报告未开（壳层据此新建会话）");
        Assert.Null(StaTestHarness.Run(() => fixture.Dialogs.Owner));
        Assert.DoesNotContain(fixture.View, ResidentWindows());
        Assert.Same(fixture.Anchor, StaTestHarness.Run(() => Application.Current.MainWindow));
    }

    [Fact]
    public void 关窗后_宿主导航状态不滞留会话内页面VM_重开不渲染已释放实例()
    {
        // 后台静默形态与退出态不走出账动作，关窗收尾必须兜底出账：否则常驻的 NavigationStore
        // 仍指着会话内页面 VM（随会话作用域已释放），重开时"同类型已停驻"短路会让页面渲染已释放实例。
        var fixture = StaTestHarness.Run(CreateFixture);
        StaTestHarness.Run(() => fixture.Navigation.Navigate(NavigationSlot.Trigger));
        Assert.NotNull(StaTestHarness.Run(() => fixture.Store.CurrentViewModel));

        StaTestHarness.Run(() => fixture.View.Close());

        Assert.Null(StaTestHarness.Run(() => fixture.Store.CurrentViewModel));
    }

    [Fact]
    public void 收尾纪律_断开窗口内容与DataContext_窗口不再直接挂住壳区VM与视图树()
    {
        // 收尾要把窗口对其内容与 DataContext 的引用一并丢弃：被滞留的窗口对象只应挂住自己，
        // 不应再顺带挂住整棵视图树与挂在 DataContext 上的 VM（见类 remarks 的已知边界）。
        var fixture = StaTestHarness.Run(CreateFixture);
        Assert.NotNull(StaTestHarness.Run(() => fixture.View.Content));
        Assert.Same(fixture.ShellViewModel, StaTestHarness.Run(() => fixture.View.DataContext));

        StaTestHarness.Run(fixture.Console.Dispose);

        Assert.Null(StaTestHarness.Run(() => fixture.View.Content));
        Assert.Null(StaTestHarness.Run(() => fixture.View.DataContext));
    }

    [Fact]
    public void 设置台开着时_对话框服务Owner指向设置台_关闭后解绑()
    {
        // Owner 随设置台开/关绑定与解绑：离开释放仍持强引用会让已关闭窗口永久不可回收。
        var fixture = StaTestHarness.Run(CreateFixture);

        Assert.Same(fixture.View, StaTestHarness.Run(() => fixture.Dialogs.Owner));

        StaTestHarness.Run(fixture.Console.Dispose);

        Assert.Null(StaTestHarness.Run(() => fixture.Dialogs.Owner));
    }

    // ==== INV3：无设置台时主窗口属性指向锚窗口 ====

    [Fact]
    public void 无设置台时_主窗口属性指向锚窗口_绝不指向已关闭的瞬态窗口()
    {
        var fixture = StaTestHarness.Run(CreateFixture);
        Assert.Same(fixture.View, StaTestHarness.Run(() => Application.Current.MainWindow));

        StaTestHarness.Run(fixture.Console.Dispose);

        Assert.Same(fixture.Anchor, StaTestHarness.Run(() => Application.Current.MainWindow));
    }

    [Fact]
    public void 重建设置台_主窗口属性回到新设置台窗口_旧窗口已出账()
    {
        var fixture = StaTestHarness.Run(CreateFixture);
        StaTestHarness.Run(fixture.Console.Dispose);

        var reopened = StaTestHarness.Run(() =>
        {
            ConsoleFixture next = CreateFixture();
            return (next.Console, next.View, MainWindow: Application.Current.MainWindow);
        });

        Assert.Same(reopened.View, reopened.MainWindow);
        Assert.NotSame(fixture.View, reopened.MainWindow);
        Assert.DoesNotContain(fixture.View, ResidentWindows());

        StaTestHarness.Run(reopened.Console.Dispose);
    }

    // ==== 瞬态窗口收尾纪律（唯一实现）====

    [Fact]
    public void 收尾纪律_关闭窗口_从全局窗口集合出账_主窗口属性回退锚窗口()
    {
        var (anchor, window) = StaTestHarness.Run(() =>
        {
            var resident = new ShellAnchorWindow();
            Application.Current.MainWindow = resident;
            var transient = new Window { Left = OffScreen, Top = OffScreen, ShowActivated = false };
            transient.Show();
            Application.Current.MainWindow = transient;
            return (resident, transient);
        });

        StaTestHarness.Run(() => TransientWindowTeardown.Complete(window, anchor));

        Assert.DoesNotContain(window, ResidentWindows());
        Assert.Same(anchor, StaTestHarness.Run(() => Application.Current.MainWindow));
    }

    [Fact]
    public void 收尾纪律_清窗口动画_关闭后窗口不再挂着动画时钟()
    {
        // 淡出动画的时钟会持有窗口：收尾必须先把动画摘干净，否则窗口被动画滞留。
        Window? window = StaTestHarness.Run(() =>
        {
            var transient = new Window { Left = OffScreen, Top = OffScreen, ShowActivated = false };
            transient.Show();
            transient.BeginAnimation(
                Window.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromMinutes(10)));
            return transient;
        });
        Assert.True(StaTestHarness.Run(() => window.HasAnimatedProperties));

        StaTestHarness.Run(() => TransientWindowTeardown.Complete(window));

        Assert.False(StaTestHarness.Run(() => window.HasAnimatedProperties), "收尾未清除窗口动画");
    }

    // ==== INV5：无控制台时轮盘路径可用且产物可回收 ====

    [Fact]
    public void 无控制台时_轮盘预热路径仍可用_产物可回收()
    {
        // 托盘态只保留托盘与手势：轮盘不依赖设置台——会话已结束、导航状态已出账（无控制台），
        // 轮盘离屏预热仍能跑完，且产物（窗口 + 视图）放弃引用后可回收。
        var fixture = StaTestHarness.Run(CreateFixture);
        StaTestHarness.Run(fixture.Console.Dispose);
        Assert.Null(StaTestHarness.Run(() => fixture.Store.CurrentViewModel));

        WeakReference wheel = StaTestHarness.Run(() =>
        {
            var viewModel = new WheelViewModel(
                new GesturePoint(200, 200),
                new WheelProfile(),
                new AppConfig(),
                new LocalizationService());
            return WheelWarmup.Run(viewModel, new FakeThemeService(), new LocalizationService(), new TestIconAssetService());
        });

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(wheel.IsAlive, "无控制台时轮盘预热产物未被回收");
    }

    // ==== 地基假设：零 WPF 窗口状态下 UI 线程消息泵仍在 ====

    [Fact]
    public async Task 零窗口状态下_UI线程消息泵仍在_全局钩子的安装线程不随窗口消失()
    {
        // WH_MOUSE_LL 的回调投递要求安装线程有消息泵。设置台关闭后进程内再无承载 HWND 的
        // WPF 窗口（只剩永不显示的锚窗口），此刻低优先级的载荷仍须被处理——消息泵活着。
        // 诚实边界：本用例证明泵在，不产生真实全局鼠标事件（真实事件投递属 e2e 范围）。
        var fixture = StaTestHarness.Run(CreateFixture);
        StaTestHarness.Run(fixture.Console.Dispose);

        var pumped = new TaskCompletionSource();
        _ = StaTestHarness.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => pumped.SetResult());

        // 阻塞点在测试线程而非 UI 线程：消息循环仍在 harness 线程上跑，Background 优先级载荷会被处理。
        await pumped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
