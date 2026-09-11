using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StarPie.Tests;

/// <summary>
/// SDK 收口·headless 面基线（#112；ADR-0027 / plugins.md §2）：StarPie.Sdk 只含纯托管
/// 契约/模型/DTO——SDK 导出面与「P1.3 迁入清单」双向相等（少一个或多一个都失败），
/// 全仓程序集导出类型唯一（不出现同类型双份定义），被掏空的三个纯托管契约工程暂留为空壳，
/// 源码树保持「镜像旧相对路径」的过渡形态。与 <see cref="FourSetBoundaryTests"/>（工程面）、
/// <see cref="RuntimeNoCrossReferenceTests"/>（引用面）互补。
/// </summary>
public sealed class SdkBoundaryTests
{
    /// <summary>P1.3/#112 迁入 StarPie.Sdk 的全部导出类型（SDK 导出面 = 恰为该清单）。</summary>
    private static readonly Type[] MigratedTypes =
    {
        // Models/
        typeof(AppConfig), typeof(WheelProfile), typeof(ActionItem), typeof(CustomColorPreset),
        typeof(RgbColor), typeof(ColorMath), typeof(GesturePoint),
        // Services/AppHostDelegates.cs
        typeof(StarPie.Services.AppHostDelegates),
        // Services/Messages/
        typeof(ImmediateSaveRequestedMessage), typeof(DebouncedSaveRequestedMessage),
        typeof(ConfigImportedMessage), typeof(MinimizedToTrayMessage), typeof(AppThemeChangedMessage),
        typeof(AppearancePreviewInvalidatedMessage), typeof(PageConfigReloadedMessage),
        typeof(BlacklistEntryAddedMessage), typeof(GeneralNoticeRequestedMessage),
        typeof(NoticeKind), typeof(NoticeRequest),
        // Services/Navigation/
        typeof(NavigationSlot), typeof(NavigationSlots), typeof(NavigationPageRegistration),
        typeof(NavigationCatalog),
        // Services/Dialogs/
        typeof(IDialogService), typeof(ProgramPickResult), typeof(InputDialogResult),
        typeof(IconPickResult), typeof(ColorPickResult), typeof(EyedropResult), typeof(FilePickResult),
        // Services/Wheel/
        typeof(IWheelFactory),
        // ViewModels/Wheel/
        typeof(IWheelViewModel), typeof(IWheelAppearanceState),
        // ViewModels/Pages/
        typeof(IProfilePreviewSource),
    };

    [Fact]
    public void 迁入类型_全部由StarPieSdk定义()
    {
        Assert.All(MigratedTypes, type => Assert.Equal("StarPie.Sdk", type.Assembly.GetName().Name));
    }

    [Fact]
    public void Sdk导出面_恰为迁入清单_零额外类型()
    {
        string[] expected = MigratedTypes
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, FourSetBoundaryProbe.ExportedTypeNames(typeof(AppConfig).Assembly));
    }

    [Fact]
    public void 全仓程序集_导出类型唯一_无同类型双份定义()
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] assemblies = FourSetBoundaryProbe.FourSetAssemblyNames
            .Concat(FourSetBoundaryProbe.LegacyAssemblyNames)
            .ToArray();

        foreach (string name in assemblies)
        {
            Assembly assembly = FourSetBoundaryProbe.LoadAppAssembly(name);
            foreach (string typeName in FourSetBoundaryProbe.ExportedTypeNames(assembly))
            {
                Assert.False(
                    owners.TryGetValue(typeName, out string? owner),
                    $"{typeName} 由 {owner} 与 {name} 双份定义（编译期唯一性被破坏）");
                owners[typeName] = name;
            }
        }

        // 四集与旧集都被真实扫描过（避免筛选写错导致空扫描假绿）。
        Assert.Contains("StarPie.Sdk", owners.Values);
        Assert.Contains("StarPie", owners.Values);
        Assert.Contains("StarPie.Core", owners.Values);
    }

    [Fact]
    public void 三个纯托管契约工程_暂留为空壳()
    {
        string[] shells =
        {
            "StarPie.Dialogs.Contracts", "StarPie.Gestures.Contracts", "StarPie.Wheel.Contracts",
        };

        foreach (string name in shells)
        {
            Assert.Empty(FourSetBoundaryProbe.LoadAppAssembly(name).GetExportedTypes());
        }
    }

    [Fact]
    public void Sdk源码树_镜像旧相对路径_无清单外落点()
    {
        string sdkRoot = Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Sdk");
        string[] allowed = { "bin", "obj", "StarPie.Sdk.csproj", "Models", "Services", "ViewModels" };

        string[] unexpected = Directory.EnumerateFileSystemEntries(sdkRoot)
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.Empty(unexpected);
    }
}
