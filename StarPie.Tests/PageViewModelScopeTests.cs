using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Ui.Modules;
using StarPie.Sdk.Services;
using StarPie.Ui.ViewModels.Navigation;

namespace StarPie.Tests;

/// <summary>
/// 页面 VM 作用域的 as-built 清点（ADR-0039 决策 2/4）：哪个页面随设置台会话销毁、
/// 哪个页面暂留常驻，由贡献者的注册生命周期表达——本类直接读内置贡献者写入的服务描述符，
/// 不靠人工核对。会话行为（保留实例/整批释放/只读别名）由 <see cref="ConsolePageSessionTests"/> 锁。
/// 高级页在常驻壳层接管托盘气泡与提权重启后随会话：唯一的常驻页面是插件管理页。
/// </summary>
public sealed class PageViewModelScopeTests
{
    private static ServiceCollection CollectDescriptors()
    {
        var services = new ServiceCollection();
        foreach (ICompositionContributor contributor in BuiltInContributors.CreateAll(new AppHostDelegates()))
        {
            contributor.RegisterServices(services);
        }

        return services;
    }

    private static ServiceDescriptor? Find(ServiceCollection services, Type serviceType)
        => services.SingleOrDefault(descriptor => descriptor.ServiceType == serviceType);

    /// <summary>设置台会话作用域的页面/设置子 VM：随设置台开关成批创建与释放。</summary>
    public static TheoryData<Type> SessionScopedPageViewModels => new()
    {
        typeof(BehaviorSettingsViewModel),
        typeof(ProfileListViewModel),
        typeof(IProfilePreviewSource),
        typeof(AppearanceSettingsViewModel),
        typeof(InterfaceThemeSettingsViewModel),
        typeof(WheelAppearanceSettingsViewModel),
        typeof(GeneralSettingsViewModel),
    };

    /// <summary>暂留常驻的页面 VM：插件范围跨设置台开关（插件卸载/再启用与设置台无关）。</summary>
    public static TheoryData<Type> ResidentPageViewModels => new()
    {
        typeof(PluginManagerViewModel),
    };

    [Theory]
    [MemberData(nameof(SessionScopedPageViewModels))]
    public void 会话作用域页面VM_注册为Scoped(Type viewModelType)
    {
        ServiceDescriptor? descriptor = Find(CollectDescriptors(), viewModelType);

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Theory]
    [MemberData(nameof(ResidentPageViewModels))]
    public void 暂留常驻页面VM_注册为Singleton(Type viewModelType)
    {
        ServiceDescriptor? descriptor = Find(CollectDescriptors(), viewModelType);

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void 导航区与窗口外框VM_不进容器_由组合根的设置台会话工厂构造()
    {
        // 二者随设置台开关生灭且持常驻事件源订阅：注册进容器就会被容器长期持有，
        // 故不注册，由组合根在设置台工厂里构造（解析点仍在组合根）。
        ServiceCollection services = CollectDescriptors();

        Assert.Null(Find(services, typeof(NavigationViewModel)));
        Assert.Null(Find(services, typeof(WindowChromeViewModel)));
    }

    [Fact]
    public void 会话缓存_以单例注册_由导航执行缝构造注入()
    {
        ServiceDescriptor? descriptor = Find(CollectDescriptors(), typeof(ConsolePageSession));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void 页面VM解析缝_只持会话缓存_不持容器直取()
    {
        // 不新增解析点：导航执行缝从会话缓存取页面 VM，不注入 IServiceProvider 自行解析。
        var constructor = Assert.Single(typeof(NavigationExecutor).GetConstructors());

        Assert.Contains(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(ConsolePageSession));
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(IServiceProvider));
    }
}
