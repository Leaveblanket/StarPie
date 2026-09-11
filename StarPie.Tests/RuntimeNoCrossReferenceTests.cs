using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace StarPie.Tests;

/// <summary>
/// 四集运行期引用面基线：与 <see cref="FourSetBoundaryTests"/>（csproj 级）互补，在编译产物/
/// 元数据级收口——StarPie.Sdk 只含平台程序集引用（零第三方包、零 WPF、非 windows TFM）；
/// StarPie.Host 零 WPF（无 WPF 程序集、无 windows 平台投影）；StarPie.Sdk.Wpf 带 windows
/// 平台投影、引用面只含 Sdk 与平台/WPF 程序集，且不反向引用 Host/Ui/旧集；四集唯一入口与
/// 唯一 XAML 均在 Ui 集（Core 只余设计期投影字典，见 <see cref="HostBoundaryTests"/>）；
/// 三集不得引用旧 15 集 runtime（跨集只经 SDK/Sdk.Wpf 契约面与 Host 内核，
/// 旧集只被 Ui 组合根与测试引用）。
/// </summary>
public sealed class RuntimeNoCrossReferenceTests
{
    [Fact]
    public void Sdk引用面_只含平台程序集_零第三方包零WPF()
    {
        Assembly sdk = FourSetBoundaryProbe.LoadAppAssembly("StarPie.Sdk");
        string[] referenced = FourSetBoundaryProbe.ReferencedNames(sdk);

        Assert.All(FourSetBoundaryProbe.WpfAssemblyNames, wpf => Assert.DoesNotContain(wpf, referenced));
        Assert.All(referenced, name => Assert.True(
            FourSetBoundaryProbe.IsPlatformAssemblyName(name),
            $"StarPie.Sdk 引用了非平台（第三方）程序集: {name}"));

        // net10.0 非 windows TFM：无 TargetPlatform 投影（故意把 TFM 改回 windows 即被测出）。
        Assert.Empty(sdk.GetCustomAttributes<TargetPlatformAttribute>());
    }

    [Fact]
    public void Host引用面_零WPF_不引用UiSdkWpf与旧集runtime()
    {
        Assembly host = FourSetBoundaryProbe.LoadAppAssembly("StarPie.Host");
        string[] referenced = FourSetBoundaryProbe.ReferencedNames(host);

        Assert.All(FourSetBoundaryProbe.WpfAssemblyNames, wpf => Assert.DoesNotContain(wpf, referenced));
        Assert.DoesNotContain("StarPie", referenced);          // Ui 集程序集名（Host ↛ Ui）
        Assert.DoesNotContain("StarPie.Sdk.Wpf", referenced);  // plugins.md §5.1 约束 7
        Assert.All(FourSetBoundaryProbe.LegacyAssemblyNames, legacy => Assert.DoesNotContain(legacy, referenced));

        // Host 零 WPF 的产物级证据：无 windows 平台投影。
        Assert.Empty(host.GetCustomAttributes<TargetPlatformAttribute>());
    }

    [Fact]
    public void SdkWpf_带Windows平台投影_不反向引用HostUi与旧集()
    {
        Assembly sdkWpf = FourSetBoundaryProbe.LoadAppAssembly("StarPie.Sdk.Wpf");

        // WPF 契约面的产物级证据：windows 平台投影（由 net10.0-windows TFM 产生）。
        Assert.NotEmpty(sdkWpf.GetCustomAttributes<TargetPlatformAttribute>());

        string[] referenced = FourSetBoundaryProbe.ReferencedNames(sdkWpf);
        Assert.DoesNotContain("StarPie", referenced);        // Ui 集程序集名
        Assert.DoesNotContain("StarPie.Host", referenced);
        Assert.All(FourSetBoundaryProbe.LegacyAssemblyNames, legacy => Assert.DoesNotContain(legacy, referenced));
    }

    [Fact]
    public void SdkWpf引用面_只含Sdk与平台WPF程序集()
    {
        // Sdk.Wpf 是共享契约面——产物级引用闭包只许是平台/WPF 程序集与 StarPie.Sdk（Sdk 的
        // ProjectReference 在 csproj 面由 FourSetBoundaryTests 断言；此处按编译产物实际引用面
        // 拦截第三方包）。
        Assembly sdkWpf = FourSetBoundaryProbe.LoadAppAssembly("StarPie.Sdk.Wpf");
        string[] referenced = FourSetBoundaryProbe.ReferencedNames(sdkWpf);

        Assert.NotEmpty(referenced);
        Assert.All(referenced, name => Assert.True(
            name == "StarPie.Sdk"
                || FourSetBoundaryProbe.IsPlatformAssemblyName(name)
                || FourSetBoundaryProbe.WpfAssemblyNames.Contains(name),
            $"StarPie.Sdk.Wpf 引用了契约面之外的第三方程序集: {name}"));
    }

    [Fact]
    public void 四集中仅Ui是入口_且仅Ui含XAML()
    {
        Assembly ui = typeof(App).Assembly;
        Assert.NotNull(ui.EntryPoint);
        Assert.NotEmpty(FourSetBoundaryProbe.BamlEntries(ui));

        foreach (string set in new[] { "StarPie.Sdk", "StarPie.Sdk.Wpf", "StarPie.Host" })
        {
            Assembly assembly = FourSetBoundaryProbe.LoadAppAssembly(set);
            Assert.Null(assembly.EntryPoint);
            Assert.Empty(FourSetBoundaryProbe.BamlEntries(assembly));
        }
    }
}
