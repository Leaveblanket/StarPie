using System;
using System.Collections.Generic;

namespace WinPieGestures.Tests;

/// <summary>
/// 轮盘配色解析层覆盖 (#52, ADR-0014 决策 3/10)：System 随 OS 深浅、固定方案表、
/// 各风格默认深浅观感、自定义预设（id/name/CustomPreset_ 前缀）匹配、Custom 微调
/// 与坏值/空值回落，逐项等价于渲染器迁移前的内联行为。
/// </summary>
public sealed class WheelPaletteParserTests
{
    private static WheelPalette Resolve(string theme, AppConfig? config = null, bool windowsInDarkMode = false, string style = "ClassicRing")
        => WheelPaletteParser.Resolve(theme, config ?? new AppConfig(), windowsInDarkMode, style);

    private static void AssertPalette(WheelPalette palette,
        string sectorBg, string sectorBorder, string highlightBg, string highlightBorder, string text)
    {
        Assert.Equal(sectorBg, palette.SectorBg.ToHex());
        Assert.Equal(sectorBorder, palette.SectorBorder.ToHex());
        Assert.Equal(highlightBg, palette.HighlightBg.ToHex());
        Assert.Equal(highlightBorder, palette.HighlightBorder.ToHex());
        Assert.Equal(text, palette.TextColor.ToHex());
        // 常规流核色与扇区同源（紧急回落除外）。
        Assert.Equal(palette.SectorBg, palette.CoreBg);
        Assert.Equal(palette.SectorBorder, palette.CoreBorder);
    }

    private static void AssertEmergency(WheelPalette palette)
    {
        Assert.Equal("#E618181B", palette.SectorBg.ToHex());
        Assert.Equal("#35FFFFFF", palette.SectorBorder.ToHex());
        Assert.Equal("#FF3B82F6", palette.HighlightBg.ToHex());
        Assert.Equal("#A0FFFFFF", palette.HighlightBorder.ToHex());
        Assert.Equal("#FFF8FAFC", palette.TextColor.ToHex());
        // 紧急表核色与扇区不同源（沿用迁移前 catch 分支）。
        Assert.Equal("#F018181B", palette.CoreBg.ToHex());
        Assert.Equal("#30FFFFFF", palette.CoreBorder.ToHex());
    }

    // --- System ↔ OS 深浅色 ----------------------------------------------------

    [Theory]
    [InlineData("System", true, "Dark")]
    [InlineData("System", false, "Light")]
    [InlineData("system", true, "Dark")] // 兼容旧配置小写值
    [InlineData("", true, "Dark")]
    [InlineData(null, false, "Light")]
    [InlineData("Dark", true, "Dark")]
    [InlineData("CustomPreset_p1", false, "CustomPreset_p1")]
    public void ResolveEffectiveTheme_SystemOrEmpty_FollowsOsDarkness(string? theme, bool windowsInDarkMode, string expected)
    {
        Assert.Equal(expected, WheelPaletteParser.ResolveEffectiveTheme(theme!, windowsInDarkMode));
    }

    [Theory]
    [InlineData("ClassicRing", true, "#F018181B", "#40FFFFFF", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC")]
    [InlineData("ClassicRing", false, "#F5F8FAFC", "#3564748B", "#FF2563EB", "#FF93C5FD", "#FF0F172A")]
    [InlineData("CleanSectors", true, "#F20F172A", "#35334155", "#FF10B981", "#FF6EE7B7", "#FFF8FAFC")]
    [InlineData("CleanSectors", false, "#F8FFFFFF", "#35CBD5E1", "#FF059669", "#FF10B981", "#FF0F172A")]
    [InlineData("Glassmorphism", true, "#40181E32", "#50E2E8F0", "#D07C3AED", "#FFF5F3FF", "#FFF8FAFC")]
    [InlineData("Glassmorphism", false, "#45FFFFFF", "#85FFFFFF", "#D86366F1", "#FFFFFFFF", "#FF0F172A")]
    [InlineData("CatPaw", true, "#FFFFF7F9", "#FFF472B6", "#FFFB7185", "#FFFFE4E6", "#FF881337")]
    [InlineData("CatPaw", false, "#FFFFF7F9", "#FFF472B6", "#FFFB7185", "#FFFFE4E6", "#FF881337")]
    public void Resolve_System_KeepsPerStyleLightDarkLook(
        string style, bool windowsInDarkMode,
        string sectorBg, string sectorBorder, string highlightBg, string highlightBorder, string text)
    {
        AssertPalette(Resolve("System", style: style, windowsInDarkMode: windowsInDarkMode),
            sectorBg, sectorBorder, highlightBg, highlightBorder, text);
    }

    // --- 固定方案表（各风格一致） ------------------------------------------------

    [Theory]
    [InlineData("MatchaForest", "#E6142E1F", "#4034D399", "#FF10B981", "#FF6EE7B7", "#FFF0FDF4")]
    [InlineData("GlacialIce", "#E0E0F2FE", "#6038BDF8", "#FF0284C7", "#FFBAE6FD", "#FF0C4A6E")]
    [InlineData("MorandiMuted", "#E62C302E", "#409CA3AF", "#FF78716C", "#FFD6D3D1", "#FFF5F5F4")]
    public void Resolve_FixedScheme_AppliesSameSystemPresetAcrossStyles(
        string theme, string sectorBg, string sectorBorder, string highlightBg, string highlightBorder, string text)
    {
        foreach (string style in new[] { "ClassicRing", "CleanSectors", "Glassmorphism", "CatPaw" })
        {
            AssertPalette(Resolve(theme, style: style), sectorBg, sectorBorder, highlightBg, highlightBorder, text);
        }
    }

    [Fact]
    public void Resolve_Light_ClassicRing_UsesStandardLightTable()
    {
        AssertPalette(Resolve("Light"), "#F0F8FAFC", "#3064748B", "#FF2563EB", "#FF60A5FA", "#FF0F172A");
    }

    [Fact]
    public void Resolve_Light_CatPaw_KeepsPastelLook()
    {
        AssertPalette(Resolve("Light", style: "CatPaw"), "#FFFFF7F9", "#FFF472B6", "#FFFB7185", "#FFFFE4E6", "#FF881337");
    }

    [Fact]
    public void Resolve_Dark_ClassicRing_UsesStyleDarkDefault()
    {
        AssertPalette(Resolve("Dark", windowsInDarkMode: true), "#F018181B", "#40FFFFFF", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC");
    }

    // --- 自定义预设匹配（id / name / CustomPreset_ 前缀） -------------------------

    [Fact]
    public void Resolve_CustomPresetPrefix_AppliesPresetColors()
    {
        var preset = new CustomColorPreset
        {
            Id = "p1",
            SectorBg = "#AA111111", SectorBorder = "#BB222222",
            HighlightBg = "#CC333333", HighlightBorder = "#DD444444", TextColor = "#EE555555"
        };
        var config = new AppConfig { CustomColorPresets = new List<CustomColorPreset> { preset } };

        AssertPalette(Resolve("CustomPreset_p1", config),
            "#AA111111", "#BB222222", "#CC333333", "#DD444444", "#EE555555");
    }

    [Fact]
    public void Resolve_ThemeEqualsPresetName_AppliesPresetColors()
    {
        var preset = new CustomColorPreset
        {
            Id = "p1", Name = "我的配色",
            SectorBg = "#11111111", SectorBorder = "#22222222",
            HighlightBg = "#33333333", HighlightBorder = "#44444444", TextColor = "#55555555"
        };
        var config = new AppConfig { CustomColorPresets = new List<CustomColorPreset> { preset } };

        AssertPalette(Resolve("我的配色", config),
            "#11111111", "#22222222", "#33333333", "#44444444", "#55555555");
    }

    [Fact]
    public void Resolve_ThemeEqualsPresetId_AppliesPresetColors()
    {
        var preset = new CustomColorPreset
        {
            Id = "legacy-id",
            SectorBg = "#11223344", SectorBorder = "#22334455",
            HighlightBg = "#33445566", HighlightBorder = "#44556677", TextColor = "#55667788"
        };
        var config = new AppConfig { CustomColorPresets = new List<CustomColorPreset> { preset } };

        AssertPalette(Resolve("legacy-id", config),
            "#11223344", "#22334455", "#33445566", "#44556677", "#55667788");
    }

    [Fact]
    public void Resolve_CustomPresetPrefix_NoMatchingPreset_KeepsStyleDefault()
    {
        AssertPalette(Resolve("CustomPreset_missing", new AppConfig { CustomColorPresets = new List<CustomColorPreset>() }),
            "#F018181B", "#40FFFFFF", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC");
    }

    [Fact]
    public void Resolve_CustomPresetPrefix_NullPresetList_KeepsStyleDefault()
    {
        AssertPalette(Resolve("CustomPreset_p1", new AppConfig { CustomColorPresets = null! }),
            "#F018181B", "#40FFFFFF", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC");
    }

    [Fact]
    public void Resolve_UnknownScheme_KeepsStyleDefault()
    {
        AssertPalette(Resolve("UnknownScheme"),
            "#F018181B", "#40FFFFFF", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC");
    }

    // --- Custom 微调与逐字段回落 ------------------------------------------------

    [Fact]
    public void Resolve_Custom_AppliesConfiguredTweaks()
    {
        var config = new AppConfig
        {
            CustomSectorBg = "#10203040", CustomSectorBorder = "#20304050",
            CustomHighlightBg = "#30405060", CustomHighlightBorder = "#40506070", CustomText = "#50607080"
        };

        AssertPalette(Resolve("Custom", config),
            "#10203040", "#20304050", "#30405060", "#40506070", "#50607080");
    }

    [Fact]
    public void Resolve_Custom_NullFields_FallBackToStyleDefaultPerField()
    {
        var config = new AppConfig
        {
            CustomSectorBg = null!, CustomSectorBorder = "#0A0B0C0D",
            CustomHighlightBg = null!, CustomHighlightBorder = null!, CustomText = null!
        };

        AssertPalette(Resolve("Custom", config),
            "#F018181B", "#0A0B0C0D", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC");
    }

    [Fact]
    public void Resolve_Custom_NullFields_ClassicRingDarkDefaultsUsed()
    {
        var config = new AppConfig
        {
            CustomSectorBg = null!, CustomSectorBorder = null!,
            CustomHighlightBg = null!, CustomHighlightBorder = null!, CustomText = null!
        };

        AssertPalette(Resolve("Custom", config),
            "#F018181B", "#40FFFFFF", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC");
    }

    [Fact]
    public void Resolve_Custom_NullFields_CatPawFallsBackToNeutralDark()
    {
        // CatPaw 的 Custom 基底与粉彩观感不同：原实现经 base.GetDefaultColors 回落中性深色。
        var config = new AppConfig
        {
            CustomSectorBg = null!, CustomSectorBorder = null!,
            CustomHighlightBg = null!, CustomHighlightBorder = null!, CustomText = null!
        };

        AssertPalette(Resolve("Custom", config, style: "CatPaw"),
            "#EB18181B", "#30FFFFFF", "#FF2563EB", "#FF60A5FA", "#FFF8FAFC");
    }

    // --- 坏值/空值回落（整组紧急色） ---------------------------------------------

    [Fact]
    public void Resolve_Custom_InvalidField_FallsBackToEmergencyPalette()
    {
        var config = new AppConfig { CustomSectorBg = "not-a-color" };
        AssertEmergency(Resolve("Custom", config));
    }

    [Fact]
    public void Resolve_Custom_EmptyField_FallsBackToEmergencyPalette()
    {
        var config = new AppConfig { CustomText = "" };
        AssertEmergency(Resolve("Custom", config));
    }

    [Fact]
    public void Resolve_PresetFoundWithInvalidField_FallsBackToEmergencyPalette()
    {
        var preset = new CustomColorPreset { Id = "p1", SectorBg = "#GGGGGG" };
        var config = new AppConfig { CustomColorPresets = new List<CustomColorPreset> { preset } };

        AssertEmergency(Resolve("CustomPreset_p1", config));
    }

    [Fact]
    public void Resolve_PresetFoundWithNullField_FallsBackToEmergencyPalette()
    {
        // 预设命中后字段为 null 与非法等价：原实现整组走画刷 catch（不做逐字段回落）。
        var preset = new CustomColorPreset { Id = "p1", SectorBg = null! };
        var config = new AppConfig { CustomColorPresets = new List<CustomColorPreset> { preset } };

        AssertEmergency(Resolve("CustomPreset_p1", config));
    }

    [Fact]
    public void Resolve_InvalidHexText_IsRejected_UnlikeWpfNamedColors()
    {
        // 仓库色值规范为 hex（RgbColor.TryParseHex，HexToBrushConverter 同判据）：
        // 非 hex 一律按坏值回落，不沿 WPF ColorConverter 命名色宽松语法。
        var config = new AppConfig { CustomSectorBg = "Red" };
        AssertEmergency(Resolve("Custom", config));
    }
}
