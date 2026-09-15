using System.Collections.Generic;
using StarPie.Services.Wheel;
using StarPie.ViewModels.Wheel;
using StarPie.Wheel;

namespace StarPie.Tests;

/// <summary>
/// 轮盘瞬态视图投影覆盖：只含渲染所需字段的取值映射、缺省回落，
/// 以及「投影是快照不是引用」——配置在投影组装后变更不回流到已构造的实例。
/// </summary>
public sealed class WheelViewDataTests
{
    private static AppConfig Config() => new AppConfig
    {
        WheelStyle = "Glassmorphism",
        WheelPalette = "MatchaForest",
        WheelRadius = 160,
        InnerRadius = 60,
        CoreRadius = 55,
        Shape = "Circle",
        SectorGap = 3,
        SectorCornerRadius = 6,
        IconLayoutMode = "TextOnly",
        ShowText = false,
        SectorIconSize = 28,
        SectorFontSize = 12.5,
        ShowCoreIcon = false,
        CoreIconType = "Crosshair",
        CoreCustomIconKey = "custom:star",
        CoreCustomIconSvg = "M0,0L1,1",
        CoreCustomImagePath = @"C:\core.png",
        CoreBgImagePath = @"C:\bg.png",
        CoreBgStretch = "Fill",
        CoreBgOpacity = 0.4,
        CustomText = "#FF123456",
        HighlightGlowColor = "#FFA855F7",
        HighlightGlowRadius = 30,
        HighlightGlowOpacity = 0.5,
        CustomColorPresets = new List<CustomColorPreset>
        {
            new CustomColorPreset { Id = "p1", Name = "我的配色" }
        }
    };

    // --- 取值映射 ------------------------------------------------------

    [Fact]
    public void FromConfig_MapsEveryRenderField()
    {
        var data = WheelViewData.FromConfig(Config());

        Assert.Equal("Glassmorphism", data.WheelStyle);
        Assert.Equal("MatchaForest", data.WheelPalette);
        Assert.Equal(160, data.WheelRadius);
        Assert.Equal(60, data.InnerRadius);
        Assert.Equal(55, data.CoreRadius);
        Assert.Equal("Circle", data.Shape);
        Assert.Equal(3, data.SectorGap);
        Assert.Equal(6, data.SectorCornerRadius);
        Assert.Equal("TextOnly", data.IconLayoutMode);
        Assert.False(data.ShowText);
        Assert.Equal(28, data.SectorIconSize);
        Assert.Equal(12.5, data.SectorFontSize);
        Assert.False(data.ShowCoreIcon);
        Assert.Equal("Crosshair", data.CoreIconType);
        Assert.Equal("custom:star", data.CoreCustomIconKey);
        Assert.Equal("M0,0L1,1", data.CoreCustomIconSvg);
        Assert.Equal(@"C:\core.png", data.CoreCustomImagePath);
        Assert.Equal(@"C:\bg.png", data.CoreBgImagePath);
        Assert.Equal("Fill", data.CoreBgStretch);
        Assert.Equal(0.4, data.CoreBgOpacity);
    }

    [Fact]
    public void FromConfig_CarriesNarrowPaletteInput()
    {
        var data = WheelViewData.FromConfig(Config());

        Assert.Equal("#FF123456", data.PaletteInput.CustomText);
        Assert.Equal("#FFA855F7", data.PaletteInput.HighlightGlowColor);
        Assert.Equal(30, data.PaletteInput.HighlightGlowRadius);
        Assert.Equal(0.5, data.PaletteInput.HighlightGlowOpacity);
        Assert.Single(data.PaletteInput.CustomPresets);
        Assert.Equal("p1", data.PaletteInput.CustomPresets[0].Id);
    }

    // --- 缺省回落 ------------------------------------------------------

    [Fact]
    public void FromConfig_Null_UsesModelDefaults()
    {
        var data = WheelViewData.FromConfig(null);

        Assert.Equal(WheelStyleNames.ClassicRing, data.WheelStyle);
        Assert.Equal(WheelPaletteNames.System, data.WheelPalette);
        Assert.Equal("Original", data.Shape);
        Assert.Equal("IconAndText", data.IconLayoutMode);
        Assert.Equal("Exit", data.CoreIconType);
        Assert.Equal("UniformToFill", data.CoreBgStretch);
        Assert.Equal("", data.CoreCustomImagePath);
        Assert.Equal("", data.CoreBgImagePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromConfig_BlankStyleAndPalette_FallBackToDefaults(string? blank)
    {
        var config = new AppConfig { WheelStyle = blank!, WheelPalette = blank! };

        var data = WheelViewData.FromConfig(config);

        Assert.Equal(WheelStyleNames.Default, data.WheelStyle);
        Assert.Equal(WheelPaletteNames.System, data.WheelPalette);
    }

    // --- 快照语义（不引入变更传播） --------------------------------------

    [Fact]
    public void FromConfig_IsSnapshot_ConfigMutationDoesNotFlowBack()
    {
        var config = Config();
        var data = WheelViewData.FromConfig(config);

        config.WheelStyle = "CatPaw";
        config.WheelPalette = "Dark";
        config.WheelRadius = 300;
        config.InnerRadius = 120;
        config.ShowText = true;
        config.CustomText = "#FF000000";
        config.HighlightGlowRadius = 44;
        config.CoreBgImagePath = @"D:\other.png";

        Assert.Equal("Glassmorphism", data.WheelStyle);
        Assert.Equal("MatchaForest", data.WheelPalette);
        Assert.Equal(160, data.WheelRadius);
        Assert.Equal(60, data.InnerRadius);
        Assert.False(data.ShowText);
        Assert.Equal("#FF123456", data.PaletteInput.CustomText);
        Assert.Equal(30, data.PaletteInput.HighlightGlowRadius);
        Assert.Equal(@"C:\bg.png", data.CoreBgImagePath);
    }

    [Fact]
    public void FromConfig_PresetListIsCopied_NotAliased()
    {
        var config = Config();
        var data = WheelViewData.FromConfig(config);

        // 列表整表复制：增删预设不改变已组装的快照。
        config.CustomColorPresets.Clear();
        config.CustomColorPresets.Add(new CustomColorPreset { Id = "p2" });

        Assert.Single(data.PaletteInput.CustomPresets);
        Assert.Equal("p1", data.PaletteInput.CustomPresets[0].Id);
    }

    [Fact]
    public void FromConfig_NullPresetList_BecomesEmptyList()
    {
        var config = new AppConfig { CustomColorPresets = null! };

        var data = WheelViewData.FromConfig(config);

        Assert.NotNull(data.PaletteInput.CustomPresets);
        Assert.Empty(data.PaletteInput.CustomPresets);
    }

    // --- 窄配色输入：与解析器同源 ----------------------------------------

    [Fact]
    public void PaletteInput_FromConfig_DrivesParserSameAsBefore()
    {
        var config = Config();
        config.CustomSectorBg = "#9016161A";
        config.CustomSectorBorder = "#35FFFFFF";
        config.CustomHighlightBg = "#E06C4DFF";
        config.CustomHighlightBorder = "#A0FFFFFF";
        config.CustomText = "#FF123456";

        var fromProjection = WheelPaletteParser.Resolve(
            WheelPaletteNames.Custom, WheelViewData.FromConfig(config).PaletteInput, windowsInDarkMode: false, WheelStyleNames.ClassicRing);

        // 同一配置经投影喂解析器，结果与直接取配置字段一致（同源组装，无预览侧对应物）。
        Assert.Equal("#9016161A", fromProjection.SectorBg.ToHex());
        Assert.Equal("#35FFFFFF", fromProjection.SectorBorder.ToHex());
        Assert.Equal("#E06C4DFF", fromProjection.HighlightBg.ToHex());
        Assert.Equal("#A0FFFFFF", fromProjection.HighlightBorder.ToHex());
        Assert.Equal("#FF123456", fromProjection.TextColor.ToHex());
    }
}
