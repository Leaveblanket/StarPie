using System.Collections;
using System.Linq;
using System.Resources;
using WinPieGestures.Views.Converters;

namespace WinPieGestures.Tests;

/// <summary>
/// B5/#78（模块化：共享 UI 基建迁共享内核 Core）跨集归属与依赖收口：
/// 通用共享转换器（HexToBrush/StringToGeometry/IntEquals/FilePathToImage）、共享自定义控件
/// <see cref="HotkeyRecorderBox"/> 与全局控件样式字典 <c>ModernControls.xaml</c> 迁入
/// <c>StarPie.Core</c>；App.xaml 经跨程序集 pack URI 合并该字典。命名空间维持
/// WinPieGestures.*（B10 才统一，ADR-0016 决策 12）。宿主驻留的 M2 轮盘核图标预览转换器
/// （CoreIconGeometry/Name，直连 WheelGeometry）与 S6 取色对话框行为（SpectrumCanvasBehavior，
/// 依赖 Host VM 的 SpectrumPoint）维持 Host 到对应批次，Core 不反向依赖宿主。
/// </summary>
public sealed class SharedUiAssemblyPlacementTests
{
    [Fact]
    public void 共享UI基建件_归属共享内核Core_且命名空间维持WinPieGestures()
    {
        Assert.Equal("StarPie.Core", typeof(HexToBrushConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(StringToGeometryConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(IntEqualsConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(FilePathToImageConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(HotkeyRecorderBox).Assembly.GetName().Name);

        Assert.Equal("WinPieGestures.Views.Converters", typeof(HexToBrushConverter).Namespace);
        Assert.Equal("WinPieGestures.Views.Converters", typeof(IntEqualsConverter).Namespace);
        Assert.Equal("WinPieGestures.Views.Converters", typeof(StringToGeometryConverter).Namespace);
        Assert.Equal("WinPieGestures.Views.Converters", typeof(FilePathToImageConverter).Namespace);
        Assert.Equal("WinPieGestures.Views.Controls", typeof(HotkeyRecorderBox).Namespace);
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
    public void M2轮盘预览与S6取色对话框专用UI件_维持Host待对应批次()
    {
        // CoreIconGeometryConverter 直连 M2 WheelGeometry（Host 驻留至 B8 收编）；CoreIconNameConverter
        // 与其同 MultiBinding 配对；SpectrumCanvasBehavior 依赖 Host ColorPickerViewModel.SpectrumPoint
        // （S6 对话框实现留 Host）。三者 B5 不迁，避免 Core 反向依赖宿主。
        Assert.Equal("StarPie", typeof(CoreIconGeometryConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(CoreIconNameConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(SpectrumCanvasBehavior).Assembly.GetName().Name);

        Assert.Equal("WinPieGestures.Views.Converters", typeof(CoreIconGeometryConverter).Namespace);
        Assert.Equal("WinPieGestures.Views.Converters", typeof(CoreIconNameConverter).Namespace);
        Assert.Equal("WinPieGestures.Views.Controls", typeof(SpectrumCanvasBehavior).Namespace);
    }
}
