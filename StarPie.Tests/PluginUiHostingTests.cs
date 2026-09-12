using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarPie.Abstractions.Ui;
using StarPie.Events;
using StarPie.PluginHosting;
using StarPie.PluginRuntime.Ui;

namespace StarPie.Tests;

/// <summary>
/// 插件 UI 托管基础层：按插件 id 的资产登记表、每插件资源根、UI 线程清理编排与泄漏验证。
/// </summary>
public sealed class PluginUiHostingTests
{
    private const string PluginId = "com.example.ui-hosting";

    [Fact]
    public async Task 释放后_资产登记表清零()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.Attach();
            host.RegisterPage(new PluginPageDescriptor("main", "PageMain", "M0 0", () => new object()));
            host.RegisterCommand(new PluginCommandDescriptor("refresh", "CommandRefresh", () => { }));
            host.CreateTimer(TimeSpan.FromMilliseconds(20), () => { });
            // 资源根 + 导航页 + 命令 + 定时器
            Assert.Equal(4, host.Assets.CountFor(PluginId));

            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
            Assert.Equal(0, host.Assets.CountFor(PluginId));
            Assert.Empty(coordinator.Assets.SnapshotAll());
        });
    }

    [Fact]
    public async Task 动画_只停止不清零_摘除后才清零()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            var target = new Border();
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(60),
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new System.Windows.PropertyPath("Opacity"));
            storyboard.Children.Add(animation);
            IDisposable handle = host.CreateAnimation(target, storyboard);
            Assert.Equal(1, host.Assets.CountFor(PluginId));

            // 只 Stop：时钟与时间线仍挂在元素上，登记表不得出账。
            storyboard.Stop(target);
            Assert.Equal(1, host.Assets.CountFor(PluginId));

            // Remove 才是真摘除：出账并清零。
            handle.Dispose();
            Assert.Equal(0, host.Assets.CountFor(PluginId));
        });
    }

    [Fact]
    public async Task 视图_释放后清出容器并断开数据上下文()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            var container = new ContentControl();
            var view = new Border { DataContext = new object() };
            host.AttachView(container, view);
            Assert.Same(view, container.Content);

            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
            Assert.Null(container.Content);
            Assert.Null(view.DataContext);
            Assert.Equal(0, host.Assets.CountFor(PluginId));
        });
    }

    [Fact]
    public async Task 窗口实例_释放后摘除并清引用()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            var window = new System.Windows.Window { DataContext = new object() };
            host.TrackWindow(window);
            Assert.Equal(1, host.Assets.CountFor(PluginId));

            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
            Assert.Null(window.DataContext);
            Assert.Null(window.Owner);
            Assert.Equal(0, host.Assets.CountFor(PluginId));
        });
    }

    [Fact]
    public async Task 附加资源根_重复调用幂等()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            host.Attach();
            host.Attach();

            Assert.Equal(1, host.Assets.CountFor(PluginId));
            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);
            Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
        });
    }

    [Fact]
    public async Task 视图_摘除后对象可回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);

            WeakReference probe = AttachAndReleaseView(host);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(probe.IsAlive, "摘除后视图对象必须可回收");
            Assert.Equal(0, host.Assets.CountFor(PluginId));
        });
    }

    [Fact]
    public async Task 摘除抛异常_残留诊断带异常信息()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.Assets.Track(
                PluginId,
                PluginUiAssetKind.Timer,
                "坏定时器",
                () => throw new InvalidOperationException("夹具摘除爆炸"));

            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains(
                result.Residuals,
                line => line.Contains("InvalidOperationException") && line.Contains("夹具摘除爆炸"));
        });
    }

    [Fact]
    public async Task 窗口工厂_宿主创建并登记实例()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            var descriptor = new PluginWindowDescriptor(
                "about",
                "WindowAbout",
                () => new System.Windows.Window());

            System.Windows.Window window = host.CreateWindow(descriptor);

            Assert.Equal(1, host.Assets.CountFor(PluginId));
            Assert.NotNull(window);
            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);
            Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
        });
    }

    [Fact]
    public async Task 释放未收敛_保留上下文_待下一次安全点续收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            PluginUiAsset stubborn = host.Assets.Track(
                PluginId,
                PluginUiAssetKind.Command,
                "顽固命令",
                () => false);

            PluginUiReleaseResult first = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.False(first.Succeeded);
            Assert.True(coordinator.HasHost(PluginId), "清理未收敛必须保留下一次续收的上下文");

            // 现场修复后（例如宿主补了一次显式摘除）再走安全点：清零并移除上下文。
            host.Assets.Forget(stubborn);
            PluginUiReleaseResult second = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.True(second.Succeeded, string.Join(" | ", second.Residuals));
            Assert.False(coordinator.HasHost(PluginId));
        });
    }

    [Fact]
    public async Task 清理编排_从后台线程发起仍在UI线程执行()
    {
        PluginUiCoordinator coordinator = CreateCoordinator();
        PluginUiHost host = StaTestHarness.Run(() => coordinator.GetOrCreateHost(PluginId));
        StaTestHarness.Run(() => host.CreateTimer(TimeSpan.FromMilliseconds(20), () => { }));

        // 从线程池发起：实现必须封送到 UI 线程；若在后台线程执行，Release 的 UI 亲缘校验会抛异常。
        PluginUiReleaseResult result = await Task.Run(
            () => coordinator.ReleaseAsync(PluginId, CancellationToken.None));

        Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
        Assert.Equal(0, coordinator.Assets.CountFor(PluginId));
    }

    [Fact]
    public async Task 非UI线程注册_被拒绝()
    {
        PluginUiCoordinator coordinator = CreateCoordinator();
        PluginUiHost host = StaTestHarness.Run(() => coordinator.GetOrCreateHost(PluginId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(
            () => host.RegisterPage(
                new PluginPageDescriptor("main", "PageMain", "M0 0", () => new object()))));
    }

    [Fact]
    public async Task 资源根_并入宿主资源并在释放时整根摘除()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.Attach();
            Assert.Contains(host.ResourceRoot.Dictionary, StaTestHarness.Application.Resources.MergedDictionaries);

            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
            Assert.DoesNotContain(host.ResourceRoot.Dictionary, StaTestHarness.Application.Resources.MergedDictionaries);
            Assert.Equal(0, host.ResourceRoot.MergedCount);
        });
    }

    [Fact]
    public async Task 摘除失败的资产_作为可定位残留返回()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            PluginUiCoordinator coordinator = CreateCoordinator();
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.Assets.Track(PluginId, PluginUiAssetKind.Command, "顽固命令", () => false);

            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Residuals, line => line.Contains("顽固命令"));
            Assert.Equal(1, coordinator.Assets.CountFor(PluginId));
        });
    }

    [Fact]
    public async Task 订阅_随释放断开()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            var events = new StubPluginEvents();
            PluginUiCoordinator coordinator = CreateCoordinator(events);
            PluginUiHost host = coordinator.GetOrCreateHost(PluginId);
            host.Subscribe<object>(_ => { });
            Assert.Equal(1, host.Assets.CountFor(PluginId));

            PluginUiReleaseResult result = await coordinator.ReleaseAsync(PluginId, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join(" | ", result.Residuals));
            Assert.Equal(1, events.Disposed);
        });
    }

    [Fact]
    public async Task 未注册UI的插件_释放直接成功()
    {
        PluginUiCoordinator coordinator = CreateCoordinator();

        PluginUiReleaseResult result = await coordinator.ReleaseAsync(
            "com.example.headless",
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Residuals);
    }

    private static PluginUiCoordinator CreateCoordinator(IPluginEvents? events = null)
        => new(StaTestHarness.Application, StaTestHarness.Dispatcher, events);

    /// <summary>独立帧内挂载并摘除视图：帧退出后不残留任何局部引用，探针判定才准。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndReleaseView(PluginUiHost host)
    {
        var container = new ContentControl();
        var view = new Border();
        var probe = new WeakReference(view);
        host.AttachView(container, view);
        host.Release();
        return probe;
    }

    /// <summary>事件中介替身：只记录订阅是否被断开。</summary>
    private sealed class StubPluginEvents : IPluginEvents
    {
        public int Disposed { get; private set; }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
            => new Handle(this);

        private sealed class Handle : IDisposable
        {
            private readonly StubPluginEvents _owner;
            private bool _disposed;

            internal Handle(StubPluginEvents owner) => _owner = owner;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.Disposed++;
            }
        }
    }
}
