using System;
using System.IO;
using System.Linq;
using StarPie.Abstractions.Ui;
using StarPie.Compatibility;
using StarPie.Services.Icons;
using StarPie.Services.Programs;
using StarPie.Services.Shell;

namespace StarPie.Tests;

/// <summary>
/// StarPie.Sdk.Wpf 边界基线：导出面与白名单双向相等（删除或改名既有导出类型即失败，
/// 增量新增须同步白名单——additive-only 的机械审查点），ABI 兼容判定、默认 ALC 装载政策与
/// 源码树形态可断言。与 <see cref="SdkBoundaryTests"/>（headless 面）、<see cref="FourSetBoundaryTests"/>
/// （工程面）、<see cref="RuntimeNoCrossReferenceTests"/>（引用面）互补。
/// </summary>
public sealed class SdkWpfBoundaryTests
{
    /// <summary>StarPie.Sdk.Wpf 的全部导出类型（导出面 = 恰为该清单）。</summary>
    private static readonly Type[] MigratedTypes =
    {
        // Services/Icons/
        typeof(IIconAssetService),
        // Services/Shell/
        typeof(IThemeService),
        // Compatibility/
        typeof(UiSdkAbi), typeof(DefaultAlcPolicy),
        // Abstractions/Ui/（插件 UI 契约：入口、上下文、调度器与注册描述符）
        typeof(IPluginUiModule), typeof(IUiDispatcher), typeof(IPluginUiContext),
        typeof(PluginPageDescriptor), typeof(PluginSettingsSectionDescriptor),
        typeof(PluginWindowDescriptor), typeof(PluginMenuItemDescriptor), typeof(PluginCommandDescriptor),
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
        string[] allowed =
        {
            "bin", "obj", "StarPie.Sdk.Wpf.csproj", "Services", "Compatibility", "Abstractions",
        };

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
