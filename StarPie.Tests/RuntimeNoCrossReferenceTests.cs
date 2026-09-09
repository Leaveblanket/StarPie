using System.Linq;

namespace StarPie.Tests;

/// <summary>
/// 模块 runtime 互不引用收口（ADR-0023/#97）：模块 runtime（M1 Gestures/M2 Wheel/
/// M3 Programs/M4 Theme/M5 Shell/S6 Dialogs/S1 Icons）之间零 ProjectReference——跨模块
/// 只经各 *.Contracts 契约程序集与共享内核 Core 通信；Host（StarPie）作为组合根是唯一
/// 引用全部 runtime 的例外。本文件以反射引用面逐 runtime 断言「不引用其它 runtime」、
/// 「引用自身契约」，并反向断言契约程序集不引用所属 runtime/Host。
/// </summary>
public sealed class RuntimeNoCrossReferenceTests
{
    /// <summary>各模块 runtime 的锚点类型、程序集名与自身契约程序集名（M5 Shell 暂无出口契约集）。</summary>
    private static readonly (System.Type Anchor, string Runtime, string? Contract)[] Modules =
    {
        (typeof(GestureEngine), "StarPie.Gestures", "StarPie.Gestures.Contracts"),
        (typeof(WheelViewModel), "StarPie.Wheel", "StarPie.Wheel.Contracts"),
        (typeof(ProgramScanner), "StarPie.Programs", "StarPie.Programs.Contracts"),
        (typeof(ThemeService), "StarPie.Theme", "StarPie.Theme.Contracts"),
        (typeof(GeneralSettingsViewModel), "StarPie.Shell", null),
        (typeof(DialogService), "StarPie.Dialogs", "StarPie.Dialogs.Contracts"),
        (typeof(IconAssetService), "StarPie.Icons", "StarPie.Icons.Contracts"),
    };

    /// <summary>各契约程序集的锚点类型与其所属 runtime（契约不引用 runtime 的依赖级收口）。</summary>
    private static readonly (System.Type Anchor, string Runtime)[] Contracts =
    {
        (typeof(IProfilePreviewSource), "StarPie.Gestures"),
        (typeof(IWheelFactory), "StarPie.Wheel"),
        (typeof(IThemeService), "StarPie.Theme"),
        (typeof(IProgramScanner), "StarPie.Programs"),
        (typeof(IDialogService), "StarPie.Dialogs"),
        (typeof(IIconAssetService), "StarPie.Icons"),
    };

    private static readonly string[] RuntimeNames = Modules.Select(m => m.Runtime).ToArray();

    [Fact]
    public void 模块runtime之间_零互相引用_仅Host组合根引用全部runtime()
    {
        foreach (var (anchor, runtime, _) in Modules)
        {
            string[] referenced = GetReferences(anchor);
            foreach (string other in RuntimeNames)
            {
                if (other == runtime) continue;
                Assert.DoesNotContain(other, referenced);
            }
        }

        // Host（StarPie）作为组合根显式引用全部模块 runtime（注册器调用/装配面编排）。
        string[] hostReferences = GetReferences(typeof(AppearanceSettingsViewModel));
        foreach (string runtime in RuntimeNames)
        {
            Assert.Contains(runtime, hostReferences);
        }
    }

    [Fact]
    public void 各模块runtime_引用自身契约程序集()
    {
        foreach (var (anchor, _, contract) in Modules)
        {
            if (contract == null) continue; // M5 Shell 暂无出口契约集（ADR-0023 未列）
            Assert.Contains(contract, GetReferences(anchor));
        }
    }

    [Fact]
    public void 契约程序集_不引用所属runtime与其他模块runtime及Host()
    {
        foreach (var (anchor, _) in Contracts)
        {
            string[] referenced = GetReferences(anchor);
            foreach (string runtime in RuntimeNames)
            {
                Assert.DoesNotContain(runtime, referenced);
            }
            Assert.DoesNotContain("StarPie", referenced); // Host 程序集名（组合根例外不适用于契约）
        }
    }

    private static string[] GetReferences(System.Type anchor)
        => anchor.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
}
