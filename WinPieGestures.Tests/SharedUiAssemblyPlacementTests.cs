using System.Collections;
using System.Linq;
using System.Resources;
using StarPie.Views.Converters;

namespace StarPie.Tests;

/// <summary>
/// 共享 UI 基建跨程序集归属与依赖收口：通用共享转换器
/// （HexToBrush/StringToGeometry/IntEquals/FilePathToImage）、共享自定义控件
/// <see cref="HotkeyRecorderBox"/> 与全局控件样式字典 <c>ModernControls.xaml</c> 位于
/// 共享内核 <c>StarPie.Core</c>；App.xaml 经跨程序集 pack URI 合并该字典。轮盘核图标
/// 预览转换器（CoreIconGeometry/Name）位于 <c>StarPie.Wheel</c>（见
/// <see cref="WheelAssemblyPlacementTests"/>）；取色对话框行为
/// （SpectrumCanvasBehavior，依赖宿主 VM 的 SpectrumPoint）仍驻宿主，
/// Core 不反向依赖宿主。
/// </summary>
public sealed class SharedUiAssemblyPlacementTests
{
    [Fact]
    public void 共享UI基建件_归属共享内核Core_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Core", typeof(HexToBrushConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(StringToGeometryConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(IntEqualsConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(FilePathToImageConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(HotkeyRecorderBox).Assembly.GetName().Name);

        Assert.Equal("StarPie.Views.Converters", typeof(HexToBrushConverter).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(IntEqualsConverter).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(StringToGeometryConverter).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(FilePathToImageConverter).Namespace);
        Assert.Equal("StarPie.Views.Controls", typeof(HotkeyRecorderBox).Namespace);
    }

    [Fact]
    public void 共享内核UI件_不引用宿主exe与业务模块程序集()
    {
        string?[] referenced = typeof(HexToBrushConverter).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
    }

    [Fact]
    public void ModernControls全局样式字典_BAML已编入共享内核Core()
    {
        var assembly = typeof(HotkeyRecorderBox).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();
        Assert.Contains(entries, name => name.Contains("moderncontrols", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void S6取色对话框专用UI件_维持Host待后续批次()
    {
        // CoreIconGeometryConverter/CoreIconNameConverter（轮盘核图标预览配套）位于
        // StarPie.Wheel（归属裁决见 WheelAssemblyPlacementTests）；本测试只收口仍驻宿主的
        // 取色对话框行为：SpectrumCanvasBehavior 依赖宿主 ColorPickerViewModel.SpectrumPoint
        // （对话框实现留宿主），避免 Core 反向依赖宿主。
        Assert.Equal("StarPie", typeof(SpectrumCanvasBehavior).Assembly.GetName().Name);

        Assert.Equal("StarPie.Views.Controls", typeof(SpectrumCanvasBehavior).Namespace);
    }
}
