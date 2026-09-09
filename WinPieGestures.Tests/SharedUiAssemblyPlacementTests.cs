using System;
using System.Collections;
using System.Linq;
using System.Resources;
using StarPie.Views.Converters;
using StarPie.Views.Controls;

namespace StarPie.Tests;

/// <summary>
/// 共享 UI 基建去共享化（ADR-0022/#94）跨程序集归属与依赖收口：4 个通用共享转换器
/// （HexToBrush/StringToGeometry/IntEquals/FilePathToImage）与全局控件样式字典
/// <c>ModernControls.xaml</c> 迁入宿主 <c>StarPie</c>（App.xaml 本地单点合并/实例化，
/// 资源 key 不变、运行期消费方零改动）；共享自定义控件
/// <see cref="HotkeyRecorderBox"/> 下沉唯一编译期消费方 <c>StarPie.Gestures</c>
/// （样式字典同驻模块，App.xaml 经跨程序集 pack URI 合并）；共享页面基类
/// <c>SettingsPageBase</c> 删除与五页 XAML 根改 <c>UserControl</c> 由本文件五页断言与
/// ShellAssemblyPlacementTests/GesturesAssemblyPlacementTests 收口。
/// 命名空间统一为 StarPie.*（跨程序集共享命名空间树）。轮盘核图标预览转换器
/// （CoreIconGeometry/Name）位于 <c>StarPie.Wheel</c>（WheelAssemblyPlacementTests）；
/// 取色对话框行为（SpectrumCanvasBehavior）随 S6 实现迁入 <c>StarPie.Dialogs</c>
/// （DialogsAssemblyPlacementTests）。
/// </summary>
public sealed class SharedUiAssemblyPlacementTests
{
    [Fact]
    public void 通用转换器_迁入宿主StarPie_命名空间不变()
    {
        Assert.Equal("StarPie", typeof(HexToBrushConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(StringToGeometryConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(IntEqualsConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie", typeof(FilePathToImageConverter).Assembly.GetName().Name);

        Assert.Equal("StarPie.Views.Converters", typeof(HexToBrushConverter).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(IntEqualsConverter).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(StringToGeometryConverter).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(FilePathToImageConverter).Namespace);
    }

    [Fact]
    public void HotkeyRecorderBox控件与样式字典_下沉唯一消费方StarPie_Gestures()
    {
        Assert.Equal("StarPie.Gestures", typeof(HotkeyRecorderBox).Assembly.GetName().Name);
        Assert.Equal("StarPie.Views.Controls", typeof(HotkeyRecorderBox).Namespace);

        // 隐式默认样式字典随控件同驻模块程序集（App.xaml 经
        // /StarPie.Gestures;component/Views/Styles/HotkeyRecorderBox.xaml 合并）。
        var assembly = typeof(HotkeyRecorderBox).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();
        Assert.Contains(entries, name => name.Contains("hotkeyrecorderbox", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ModernControls全局样式字典_BAML已编入宿主StarPie()
    {
        var assembly = typeof(HexToBrushConverter).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();
        Assert.Contains(entries, name => name.Contains("moderncontrols", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void 共享内核Core_不再持有通用转换器与共享控件()
    {
        var coreAssembly = typeof(IDialogService).Assembly;
        Assert.Equal("StarPie.Core", coreAssembly.GetName().Name);

        Assert.DoesNotContain(coreAssembly.GetTypes(), t =>
            t.Name == nameof(HexToBrushConverter) ||
            t.Name == nameof(StringToGeometryConverter) ||
            t.Name == nameof(IntEqualsConverter) ||
            t.Name == nameof(FilePathToImageConverter) ||
            t.Name == nameof(HotkeyRecorderBox));
    }

    [Fact]
    public void 共享页面基类已删除_五设置页直承UserControl()
    {
        // SettingsPageBase 已随 ADR-0022/#94 删除：Core 无同名类型，五页根基类均不再
        // 指向跨程序集共享页面基类。
        Assert.DoesNotContain(typeof(IDialogService).Assembly.GetTypes(), t => t.Name == "SettingsPageBase");

        Assert.Equal("System.Windows.Controls.UserControl", typeof(TriggerSettingsPage).BaseType!.FullName);
        Assert.Equal("System.Windows.Controls.UserControl", typeof(GesturesSettingsPage).BaseType!.FullName);
        Assert.Equal("System.Windows.Controls.UserControl", typeof(AdvancedSettingsPage).BaseType!.FullName);
        Assert.Equal("System.Windows.Controls.UserControl", typeof(AboutSettingsPage).BaseType!.FullName);
        Assert.Equal("System.Windows.Controls.UserControl", typeof(AppearanceSettingsPage).BaseType!.FullName);
    }
}
