using System;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Sdk.Services.Programs;

namespace StarPie.Tests;

/// <summary>
/// 设置台会话作用域的页面 VM 缓存（ADR-0039 决策 2/4/9）：
/// 同一会话内保留实例（来回切页不丢状态）、重复取同一类型返回同一实例、未开会话取实例抛、
/// 会话结束整批释放（弱引用判定）、重开会话重建实例（不携带上次会话状态）、
/// 会话内只读别名（<c>IProfilePreviewSource</c>）与实现 VM 指向同一实例。
/// </summary>
public sealed class ConsolePageSessionTests
{
    private static readonly LocalizationService Localization = new();

    /// <summary>会话作用域内的页面 VM 替身：释放次数记在静态计数上（不靠实例强引用观察释放）。</summary>
    private sealed class ScopedPageViewModel : IDisposable
    {
        public static int DisposeCount;

        public void Dispose() => DisposeCount++;
    }

    /// <summary>常驻型页面 VM 替身（注册 singleton，不随会话释放）。</summary>
    private sealed class ResidentPageViewModel : IDisposable
    {
        public static int DisposeCount;

        public void Dispose() => DisposeCount++;
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return services.BuildServiceProvider();
    }

    private static ConsolePageSession NewSession(IServiceProvider provider, bool begin = true)
    {
        var session = new ConsolePageSession(() => provider.CreateScope());
        if (begin)
        {
            session.Begin();
        }

        return session;
    }

    // --- 会话内保活 / 幂等 ---------------------------------------------------------------

    [Fact]
    public void 同一会话内重复取同一类型_返回同一实例()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddScoped<ScopedPageViewModel>());
        var session = NewSession(provider);

        object first = session.Resolve(typeof(ScopedPageViewModel));
        object second = session.Resolve(typeof(ScopedPageViewModel));

        Assert.Same(first, second);
        session.End();
    }

    [Fact]
    public void 重开会话_页面VM重建_不携带上次会话状态()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddScoped<ScopedPageViewModel>());
        var session = NewSession(provider);
        object first = session.Resolve(typeof(ScopedPageViewModel));

        session.Begin();
        object second = session.Resolve(typeof(ScopedPageViewModel));

        Assert.NotSame(first, second);
        session.End();
    }

    [Fact]
    public void 未开会话_取实例抛_不静默回落常驻解析()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddScoped<ScopedPageViewModel>());
        var session = new ConsolePageSession(() => provider.CreateScope());

        Assert.False(session.IsOpen);
        Assert.Throws<InvalidOperationException>(() => session.Resolve(typeof(ScopedPageViewModel)));
    }

    // --- 会话结束整批释放 ----------------------------------------------------------------

    [Fact]
    public void 会话结束_scoped页面VM随作用域释放并死亡_常驻页面VM不受影响()
    {
        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<ScopedPageViewModel>();
            services.AddSingleton<ResidentPageViewModel>();
        });
        var session = NewSession(provider);
        ScopedPageViewModel.DisposeCount = 0;
        ResidentPageViewModel.DisposeCount = 0;

        // 弱引用断言必须与局部变量解耦：解析/释放放在独立方法里，方法返回后栈上的强引用才消失
        //（否则被 JIT 保活的局部变量会让断言假红）。
        (WeakReference Scoped, WeakReference Resident) captured = ResolveThenEnd(session);

        Assert.Equal(1, ScopedPageViewModel.DisposeCount);
        Assert.Equal(0, ResidentPageViewModel.DisposeCount);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(captured.Scoped.IsAlive, "会话结束后的页面 VM 仍被强引用");
        Assert.True(captured.Resident.IsAlive, "常驻型页面 VM 不应被会话释放");
    }

    private static (WeakReference Scoped, WeakReference Resident) ResolveThenEnd(ConsolePageSession session)
    {
        var scoped = (ScopedPageViewModel)session.Resolve(typeof(ScopedPageViewModel));
        var resident = (ResidentPageViewModel)session.Resolve(typeof(ResidentPageViewModel));
        var references = (new WeakReference(scoped), new WeakReference(resident));
        session.End();
        return references;
    }

    // --- 会话内只读别名指向实现 VM --------------------------------------------------------

    [Fact]
    public void 只读预览源别名_与方案列表VM在同一会话内指向同一实例()
    {
        // 与 WheelGestureContributor 的注册同形：实现 VM 与只读契约别名同在会话作用域。
        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton<IMessenger>(TestHub.NewMessenger());
            services.AddSingleton<ILocalizationService>(Localization);
            services.AddSingleton<IDialogService>(new TestDialogService());
            services.AddSingleton<IActionExecutorService>(new TestActionExecutor());
            services.AddSingleton<IIconAssetService>(new TestIconAssetService());
            services.AddScoped(sp => new ProfileListViewModel(
                new AppConfig().Profiles,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IActionExecutorService>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<IIconAssetService>()));
            services.AddScoped<IProfilePreviewSource>(sp => sp.GetRequiredService<ProfileListViewModel>());
        });
        var session = NewSession(provider);

        object alias = session.Resolve(typeof(IProfilePreviewSource));
        object implementation = session.Resolve(typeof(ProfileListViewModel));

        Assert.Same(implementation, alias);
        ((IDisposable)implementation).Dispose();
        session.End();
    }

    [Fact]
    public void 只读预览源别名_跨会话随实现VM一同重建()
    {
        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton<IMessenger>(TestHub.NewMessenger());
            services.AddSingleton<ILocalizationService>(Localization);
            services.AddSingleton<IDialogService>(new TestDialogService());
            services.AddSingleton<IActionExecutorService>(new TestActionExecutor());
            services.AddSingleton<IIconAssetService>(new TestIconAssetService());
            services.AddScoped(sp => new ProfileListViewModel(
                new AppConfig().Profiles,
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IActionExecutorService>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<IIconAssetService>()));
            services.AddScoped<IProfilePreviewSource>(sp => sp.GetRequiredService<ProfileListViewModel>());
        });
        var session = NewSession(provider);
        object firstAlias = session.Resolve(typeof(IProfilePreviewSource));

        session.Begin();
        object secondAlias = session.Resolve(typeof(IProfilePreviewSource));

        Assert.NotSame(firstAlias, secondAlias);
        Assert.Same(session.Resolve(typeof(ProfileListViewModel)), secondAlias);
        ((IDisposable)secondAlias).Dispose();
        session.End();
    }
}
