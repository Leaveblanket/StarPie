using System;
using System.IO;
using System.Linq;
using StarPie.Compatibility;
using StarPie.Services.Icons;
using StarPie.Services.Programs;
using StarPie.Services.Shell;

namespace StarPie.Tests;

/// <summary>
/// SDK 收口·WPF 面基线（P1.4/#113；ADR-0027/0028 / plugins.md §2/§5.1/§11）：StarPie.Sdk.Wpf
/// 的导出面与「P1.4 迁入清单」双向相等（additive-only 的机械审查点——删除或改名既有导出
/// 类型即失败，增量新增须同步白名单），ABI/装载政策骨架（UiSdkAbi/DefaultAlcPolicy）与源码树
/// 形态可断言。与 <see cref="SdkBoundaryTests"/>（headless 面）、<see cref="FourSetBoundaryTests"/>
/// （工程面）、<see cref="RuntimeNoCrossReferenceTests"/>（引用面）互补。
/// </summary>
public sealed class SdkWpfBoundaryTests
{
    /// <summary>P1.4/#113 迁入 StarPie.Sdk.Wpf 的全部导出类型（Sdk.Wpf 导出面 = 恰为该清单）。</summary>
    private static readonly Type[] MigratedTypes =
    {
        // Services/Icons/（原 StarPie.Icons.Contracts）
        typeof(IIconAssetService), typeof(IconCatalog), typeof(CustomIconItem), typeof(VectorIconItem),
        // Services/Icons/IShortcutTargetResolver.cs（原 StarPie.Programs.Contracts 的 SPI）
        typeof(IShortcutTargetResolver),
        // Services/Programs/（原 StarPie.Programs.Contracts）
        typeof(IProgramScanner), typeof(ProgramEntry), typeof(ProgramCatalog),
        // Services/Shell/（原 StarPie.Theme.Contracts）
        typeof(IThemeService),
        // Compatibility/（#113 新增 ABI/装载政策骨架）
        typeof(UiSdkAbi), typeof(DefaultAlcPolicy),
    };

    [Fact]
    public void 迁入类型_全部由StarPieSdkWpf定义()
    {
        Assert.All(MigratedTypes, type => Assert.Equal("StarPie.Sdk.Wpf", type.Assembly.GetName().Name));
    }

    [Fact]
    public void SdkWpf导出面_恰为迁入清单_零额外类型()
    {
        string[] expected = MigratedTypes
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, FourSetBoundaryProbe.ExportedTypeNames(typeof(UiSdkAbi).Assembly));
    }

    [Fact]
    public void SdkWpf源码树_镜像旧相对路径_无清单外落点()
    {
        string sdkWpfRoot = Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Sdk.Wpf");
        string[] allowed = { "bin", "obj", "StarPie.Sdk.Wpf.csproj", "Services", "Compatibility" };

        string[] unexpected = Directory.EnumerateFileSystemEntries(sdkWpfRoot)
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.Empty(unexpected);
    }

    [Fact]
    public void UiSdkAbi_兼容判定_同主且次不高于宿主()
    {
        Assert.True(UiSdkAbi.IsCompatible(1, 0, 1, 0));
        Assert.True(UiSdkAbi.IsCompatible(1, 0, 1, 2));
        Assert.False(UiSdkAbi.IsCompatible(1, 3, 1, 2));   // 次版本高于宿主
        Assert.False(UiSdkAbi.IsCompatible(2, 0, 1, 9));   // 主版本不同

        // 政策常量即当前宿主版本：按常量判定自身必兼容，次版本 +1 必不兼容。
        Assert.True(UiSdkAbi.IsCompatibleWithCurrentHost(UiSdkAbi.MajorVersion, UiSdkAbi.MinorVersion));
        Assert.False(UiSdkAbi.IsCompatibleWithCurrentHost(UiSdkAbi.MajorVersion, UiSdkAbi.MinorVersion + 1));
    }

    [Fact]
    public void UiSdkAbi_版本串_规范形态为主次且可解析()
    {
        Assert.Equal($"{UiSdkAbi.MajorVersion}.{UiSdkAbi.MinorVersion}", UiSdkAbi.Version);

        Assert.True(UiSdkAbi.TryParseVersion(UiSdkAbi.Version, out int major, out int minor));
        Assert.Equal(UiSdkAbi.MajorVersion, major);
        Assert.Equal(UiSdkAbi.MinorVersion, minor);

        foreach (string? invalid in new[] { null, "", "   ", "1", "1.0.0", "a.b", "-1.0" })
        {
            Assert.False(UiSdkAbi.TryParseVersion(invalid, out int failedMajor, out int failedMinor));
            Assert.Equal(0, failedMajor);
            Assert.Equal(0, failedMinor);
        }
    }

    [Fact]
    public void DefaultAlcPolicy_共享契约集_必须回退默认ALC()
    {
        Assert.Equal(
            new[] { "StarPie.Sdk", "StarPie.Sdk.Wpf" },
            DefaultAlcPolicy.SharedContractAssemblyNames);

        Assert.True(DefaultAlcPolicy.MustResolveFromDefaultAlc(DefaultAlcPolicy.SdkAssemblyName));
        Assert.True(DefaultAlcPolicy.MustResolveFromDefaultAlc(DefaultAlcPolicy.SdkWpfAssemblyName));
        Assert.True(DefaultAlcPolicy.MustResolveFromDefaultAlc(typeof(UiSdkAbi).Assembly.GetName().Name!));
        Assert.False(DefaultAlcPolicy.MustResolveFromDefaultAlc("StarPie.Plugin.Sample"));
        Assert.False(DefaultAlcPolicy.MustResolveFromDefaultAlc("StarPie.Host"));
    }
}
