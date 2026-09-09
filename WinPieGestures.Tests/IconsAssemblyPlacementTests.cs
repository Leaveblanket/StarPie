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
/// <c>StarPie.Icons</c> runtime。依赖方向：Icons → Contracts + Core（#95 中间态
/// <see cref="IShortcutTargetResolver"/> 暂留 Core，契约即 S1 的 SPI，.lnk 解析依赖；
/// #96 随 Programs.Contracts 迁出）；无 Core → Icons 反向；Icons runtime 只被
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
    public void S1中间态_IShortcutTargetResolver暂留共享内核Core()
    {
        // #95 中间态：SPI 暂留 Core（Icons runtime → Core 允许），#96 随 Programs.Contracts
        // 迁出后本断言随迁更新（契约脱离 Core，Icons runtime 改经 Contracts 边）。
        Assert.Equal("StarPie.Core", typeof(IShortcutTargetResolver).Assembly.GetName().Name);
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
    public void S1实现程序集_单向依赖Contracts与Core_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(IconAssetService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Icons.Contracts", referenced);
        // #95 中间态：IconAssetService .lnk 解析依赖的 SPI 暂留 Core。
        Assert.Contains("StarPie.Core", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Dialogs", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
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
