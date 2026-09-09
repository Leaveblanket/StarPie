namespace StarPie.Tests;

/// <summary>
/// 导航运行时/目录契约归属与命名空间收口（ADR-0021/#92）：导航运行时主体
/// （<see cref="NavigationStore"/>、<see cref="NavigationExecutor"/>（含
/// <see cref="INavigationExecutor"/>）、<see cref="MainViewModel"/>、
/// <see cref="NavigationItemViewModel"/>）物理归宿主程序集 <c>StarPie</c>（Host/exe），
/// 命名空间不变；共享内核 <c>StarPie.Core</c> 仅留目录/槽位契约（<see cref="NavigationCatalog"/>/
/// <see cref="NavigationSlot"/>/<see cref="NavigationSlots"/>/
/// <see cref="NavigationPageRegistration"/>）。
/// </summary>
public sealed class NavigationAssemblyPlacementTests
{
    [Fact]
    public void 导航运行时四类_归属宿主StarPie_命名空间不变()
    {
        Assert.Equal("StarPie", typeof(NavigationStore).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(NavigationExecutor).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(INavigationExecutor).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(MainViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(NavigationItemViewModel).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Navigation", typeof(NavigationStore).Namespace);
        Assert.Equal("StarPie.Services.Navigation", typeof(NavigationExecutor).Namespace);
        Assert.Equal("StarPie.Services.Navigation", typeof(INavigationExecutor).Namespace);
        Assert.Equal("StarPie.ViewModels.Navigation", typeof(MainViewModel).Namespace);
        Assert.Equal("StarPie.ViewModels.Navigation", typeof(NavigationItemViewModel).Namespace);
    }

    [Fact]
    public void 目录契约四件_归属共享内核Core_命名空间不变()
    {
        Assert.Equal("StarPie.Core", typeof(NavigationCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(NavigationSlot).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(NavigationSlots).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(NavigationPageRegistration).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Navigation", typeof(NavigationCatalog).Namespace);
        Assert.Equal("StarPie.Services.Navigation", typeof(NavigationSlot).Namespace);
        Assert.Equal("StarPie.Services.Navigation", typeof(NavigationSlots).Namespace);
        Assert.Equal("StarPie.Services.Navigation", typeof(NavigationPageRegistration).Namespace);
    }
}
