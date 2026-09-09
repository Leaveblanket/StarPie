using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services.Icons;
using StarPie.ViewModels.Pages;

namespace StarPie.Tests;

/// <summary>
/// 图标模块（S1，ADR-0023/#95）跨程序集归属、依赖与注册收口：契约四件
/// （<see cref="IIconAssetService"/>/<see cref="IconCatalog"/>/<see cref="CustomIconItem"/>/
/// <see cref="VectorIconItem"/>）独立成集驻 <c>StarPie.Icons.Contracts</c>；实现
/// （<see cref="IconAssetService"/>）与注册器 <see cref="IconsModuleRegistrar"/> 驻
/// <c>StarPie.Icons</c> runtime。依赖方向：Icons → Icons.Contracts + Programs.Contracts
/// （ADR-0023/#96：SPI <see cref="IShortcutTargetResolver"/> 随 M3 下沉，.lnk 解析经契约边）
/// + Core（S2 AppDataPaths 共享基建）；无 Core → Icons 反向；Icons runtime 只被
/// Host/注册器/测试引用，业务模块零引用（模块 runtime 互引清零仍成立）。
/// </summary>
public sealed class IconsAssemblyPlacementTests
{
    [Fact]
    public void S1出口_契约驻Contracts_实现与注册器驻Iconsruntime_命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Icons.Contracts", typeof(IIconAssetService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Icons.Contracts", typeof(IconCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Icons.Contracts", typeof(CustomIconItem).Assembly.GetName().Name);
        Assert.Equal("StarPie.Icons.Contracts", typeof(VectorIconItem).Assembly.GetName().Name);

        Assert.Equal("StarPie.Icons", typeof(IconAssetService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Icons", typeof(IconsModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Icons", typeof(IIconAssetService).Namespace);
        Assert.Equal("StarPie.Services.Icons", typeof(IconCatalog).Namespace);
        Assert.Equal("StarPie.Services.Icons", typeof(IconAssetService).Namespace);
        Assert.Equal("StarPie.Modules", typeof(IconsModuleRegistrar).Namespace);

        Assert.True(typeof(IIconAssetService).IsAssignableFrom(typeof(IconAssetService)));
    }

    [Fact]
    public void S1SPI_IShortcutTargetResolver随M3下沉ProgramsContracts_命名空间不变()
    {
        // ADR-0023/#96：SPI 随实现方 M3 下沉 Programs.Contracts（自 Core 迁出，
        // Icons runtime 改经契约边消费，契约脱离 Core）。
        Assert.Equal("StarPie.Programs.Contracts", typeof(IShortcutTargetResolver).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Icons", typeof(IShortcutTargetResolver).Namespace);
    }

    [Fact]
    public void S1契约程序集_零程序集依赖_不引用Core与实现runtime()
    {
        string?[] referenced = typeof(IIconAssetService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("StarPie.Core", referenced);
        Assert.DoesNotContain("StarPie.Icons", referenced);
        Assert.DoesNotContain("StarPie", referenced);
    }

    [Fact]
    public void S1实现程序集_单向依赖ContractsProgramsContracts与Core_不引用Host与其他业务模块runtime()
    {
        string?[] referenced = typeof(IconAssetService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Icons.Contracts", referenced);
        // ADR-0023/#96：IconAssetService .lnk 解析依赖的 SPI 经 Programs.Contracts 契约边，
        // 不引用 Programs runtime。
        Assert.Contains("StarPie.Programs.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        // S2 AppDataPaths 共享基建（IconAssetService 默认数据目录提供）。
        Assert.Contains("StarPie.Core", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Dialogs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Theme", referenced);
        Assert.DoesNotContain("StarPie.Wheel", referenced);
        Assert.DoesNotContain("StarPie.Gestures", referenced);
    }

    [Fact]
    public void S1实现runtime_只被Host与测试引用_业务模块零引用()
    {
        // 业务模块（Dialogs/Wheel/Gestures/Programs）只引用 S1 契约，不引用 Icons runtime。
        Assert.DoesNotContain("StarPie.Icons", GetReferences(typeof(DialogService)));
        Assert.DoesNotContain("StarPie.Icons", GetReferences(typeof(WheelViewModel)));
        Assert.DoesNotContain("StarPie.Icons", GetReferences(typeof(GestureEngine)));
        Assert.DoesNotContain("StarPie.Icons", GetReferences(typeof(ProgramScanner)));

        // 组合根（Host）显式引用 Icons runtime 调 IconsModuleRegistrar（注册/测试引用例外）。
        Assert.Contains("StarPie.Icons", GetReferences(typeof(AppearanceSettingsViewModel)));
        Assert.Contains("StarPie.Icons.Contracts", GetReferences(typeof(AppearanceSettingsViewModel)));

        static string[] GetReferences(System.Type type)
            => type.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
    }

    [Fact]
    public void S1注册器_RegisterServices_可经微型容器解析图标资产服务()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IShortcutTargetResolver>(new FakeShortcutResolver());

        IconsModuleRegistrar.RegisterServices(services);
        using var provider = services.BuildServiceProvider();

        var iconAssets = provider.GetRequiredService<IIconAssetService>();

        Assert.IsType<IconAssetService>(iconAssets);
        Assert.Equal("StarPie.Icons", iconAssets.GetType().Assembly.GetName().Name);
        Assert.Same(iconAssets, provider.GetRequiredService<IIconAssetService>());
    }

    /// <summary>.lnk 解析契约替身（不触 COM/Win32）。</summary>
    private sealed class FakeShortcutResolver : IShortcutTargetResolver
    {
        public bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex)
        {
            targetPath = "";
            iconPath = "";
            iconIndex = 0;
            return false;
        }
    }
}
