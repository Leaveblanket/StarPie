using System.Collections.Generic;
using System.Windows.Media;
using WinPieGestures.Models;
using Color = System.Windows.Media.Color;

namespace WinPieGestures.Tests;

/// <summary>
/// 渲染器画刷消费覆盖 (#52)：渲染器不再内联方案 hex 表/预设匹配，只把解析结果
/// （<see cref="WheelPalette"/>）构造成画刷；迁移前后观感等价由这些画刷色值钉住。
/// </summary>
public sealed class BaseStyleRendererTests
{
    private static Color ColorOf(string hex)
    {
        RgbColor.TryParseHex(hex, out var color);
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static IRadialStyleRenderer CreateRenderer(string style)
        => StyleRendererFactory.CreateRenderer(style);

    private static void AssertSolidBrush(Brush? brush, string expectedHex)
    {
        var solid = Assert.IsType<SolidColorBrush>(brush);
        Assert.Equal(ColorOf(expectedHex), solid.Color);
    }

    [Fact]
    public void Initialize_ClassicRingDark_BuildsStyleDefaultBrushes()
    {
        var renderer = CreateRenderer("ClassicRing");

        renderer.Initialize("Dark", new AppConfig(), windowsInDarkMode: true);

        AssertSolidBrush(renderer.DefaultSectorBrush, "#F018181B");
        AssertSolidBrush(renderer.SectorBorderBrush, "#40FFFFFF");
        AssertSolidBrush(renderer.HighlightSectorBrush, "#FF2563EB");
        AssertSolidBrush(renderer.HighlightBorderBrush, "#FF93C5FD");
        AssertSolidBrush(renderer.TextColorBrush, "#FFF8FAFC");
        AssertSolidBrush(renderer.CoreBgBrush, "#F018181B");
        AssertSolidBrush(renderer.CoreBorderBrush, "#40FFFFFF");
    }

    [Fact]
    public void Initialize_Light_ClassicRing_UsesStandardLightBrushes()
    {
        var renderer = CreateRenderer("ClassicRing");

        renderer.Initialize("Light", new AppConfig(), windowsInDarkMode: false);

        AssertSolidBrush(renderer.DefaultSectorBrush, "#F0F8FAFC");
        AssertSolidBrush(renderer.HighlightSectorBrush, "#FF2563EB");
        AssertSolidBrush(renderer.TextColorBrush, "#FF0F172A");
    }

    [Fact]
    public void Initialize_FixedScheme_OverridesCatPawPastelWithSchemeBrushes()
    {
        var renderer = CreateRenderer("CatPaw");

        renderer.Initialize("MatchaForest", new AppConfig(), windowsInDarkMode: false);

        AssertSolidBrush(renderer.DefaultSectorBrush, "#E6142E1F");
        AssertSolidBrush(renderer.HighlightSectorBrush, "#FF10B981");
        AssertSolidBrush(renderer.TextColorBrush, "#FFF0FDF4");
    }

    [Fact]
    public void Initialize_CustomPreset_BuildsPresetBrushes()
    {
        var preset = new CustomColorPreset
        {
            Id = "p1",
            SectorBg = "#11223344", SectorBorder = "#22334455",
            HighlightBg = "#33445566", HighlightBorder = "#44556677", TextColor = "#55667788"
        };
        var config = new AppConfig { CustomColorPresets = new List<CustomColorPreset> { preset } };
        var renderer = CreateRenderer("ClassicRing");

        renderer.Initialize("CustomPreset_p1", config, windowsInDarkMode: false);

        AssertSolidBrush(renderer.DefaultSectorBrush, "#11223344");
        AssertSolidBrush(renderer.SectorBorderBrush, "#22334455");
        AssertSolidBrush(renderer.HighlightSectorBrush, "#33445566");
        AssertSolidBrush(renderer.HighlightBorderBrush, "#44556677");
        AssertSolidBrush(renderer.TextColorBrush, "#55667788");
    }

    [Fact]
    public void Initialize_Custom_AppliesConfiguredBrushColors()
    {
        var config = new AppConfig
        {
            CustomSectorBg = "#10203040", CustomSectorBorder = "#20304050",
            CustomHighlightBg = "#30405060", CustomHighlightBorder = "#40506070", CustomText = "#50607080"
        };
        var renderer = CreateRenderer("CleanSectors");

        renderer.Initialize("Custom", config, windowsInDarkMode: false);

        AssertSolidBrush(renderer.DefaultSectorBrush, "#10203040");
        AssertSolidBrush(renderer.SectorBorderBrush, "#20304050");
        AssertSolidBrush(renderer.HighlightSectorBrush, "#30405060");
        AssertSolidBrush(renderer.HighlightBorderBrush, "#40506070");
        AssertSolidBrush(renderer.TextColorBrush, "#50607080");
    }

    [Fact]
    public void Initialize_InvalidPresetValue_FallsBackToEmergencyBrushes()
    {
        var preset = new CustomColorPreset { Id = "p1", SectorBg = "bad-value" };
        var config = new AppConfig { CustomColorPresets = new List<CustomColorPreset> { preset } };
        var renderer = CreateRenderer("ClassicRing");

        renderer.Initialize("CustomPreset_p1", config, windowsInDarkMode: false);

        AssertSolidBrush(renderer.DefaultSectorBrush, "#E618181B");
        AssertSolidBrush(renderer.SectorBorderBrush, "#35FFFFFF");
        AssertSolidBrush(renderer.HighlightSectorBrush, "#FF3B82F6");
        AssertSolidBrush(renderer.HighlightBorderBrush, "#A0FFFFFF");
        AssertSolidBrush(renderer.TextColorBrush, "#FFF8FAFC");
        AssertSolidBrush(renderer.CoreBgBrush, "#F018181B");
        AssertSolidBrush(renderer.CoreBorderBrush, "#30FFFFFF");
    }
}
