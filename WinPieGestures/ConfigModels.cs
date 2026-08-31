using System;
using System.Collections.Generic;

namespace WinPieGestures
{
    // T16：Model 层四个 POCO 自静态配置门面文件原样提取（ADR-0001：Model 保持纯 POCO，
    // config.json 格式向后兼容是硬约束），字段与默认值一字未动。

    public class ActionItem
    {
        public string Type { get; set; } = "Hotkey"; // "Launch", "Hotkey", "System"
        public string Name { get; set; } = "快捷动作"; // Name to show on the wheel sector
        public string Parameter { get; set; } = ""; // Executable path, hotkey string, or system preset
        public string Arguments { get; set; } = ""; // Optional arguments for launching
        public string IconKey { get; set; } = ""; // Vector icon key or emoji or empty
        public string CustomIconSvg { get; set; } = ""; // Custom SVG path geometry

        public override string ToString() => Name;
    }

    public class WheelProfile
    {
        public string ProcessName { get; set; } = "Global"; // e.g. "chrome.exe", "Global", or custom name
        public int SectorCount { get; set; } = 8; // 4, 8, or 12
        public List<ActionItem> Actions { get; set; } = new List<ActionItem>();

        public override string ToString() => ProcessName;
    }

    public class CustomColorPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "我的配色";
        public string SectorBg { get; set; } = "#9016161A";
        public string SectorBorder { get; set; } = "#35FFFFFF";
        public string HighlightBg { get; set; } = "#E06C4DFF";
        public string HighlightBorder { get; set; } = "#A0FFFFFF";
        public string TextColor { get; set; } = "#E0FFFFFF";

        public override string ToString() => Name;
    }

    public class AppConfig
    {
        public string Language { get; set; } = "Auto"; // "Auto", "zh-CN", "zh-TW", "en", "ja"
        public double DragThreshold { get; set; } = 25.0;
        public bool EnableOuterEscapeCancel { get; set; } = true;
        public double OuterEscapeDistance { get; set; } = 186.0; // Distance in pixels to trigger radial menu
        public string AppTheme { get; set; } = "System"; // "System", "Light", "Dark", "MidnightNavy", "RoyalViolet", "TitaniumGray"
        public string Theme { get; set; } = "System"; // Radial Wheel Color Theme: "System", "Dark", "Light", "MatchaForest", "GlacialIce", "MorandiMuted", "Custom"
        public string UiStyle { get; set; } = "ClassicRing"; // "ClassicRing", "CleanSectors", "Glassmorphism", "CatPaw"

        public bool ShowText { get; set; } = true;
        public double WheelRadius { get; set; } = 138.0;
        public double InnerRadius { get; set; } = 52.0;
        public double CoreRadius { get; set; } = 50.0;
        public string Shape { get; set; } = "Original"; // "Original", "Circle", "RoundedRect", "FloatingCapsules", "HexagonHive"
        public double SectorGap { get; set; } = 2.0; // Optical Gap between sectors: 0 ~ 12px
        public double SectorCornerRadius { get; set; } = 4.0; // Smooth Corner/Fillet: 0 ~ 16px
        public string IconLayoutMode { get; set; } = "IconAndText"; // "IconAndText", "IconOnly", "TextOnly"
        public double SectorIconSize { get; set; } = 20.0; // 14.0 ~ 36.0 px
        public double SectorFontSize { get; set; } = 10.5; // 8.0 ~ 18.0 px

        // Center Core Icon & Pattern Customization
        public bool ShowCoreIcon { get; set; } = true;
        public string CoreIconType { get; set; } = "Exit"; // "Exit", "Crosshair", "Windows", "Dot", "Home", "Power", "Compass", "CatPaw", "Custom", "Image"
        public string CoreCustomIconKey { get; set; } = "";
        public string CoreCustomIconSvg { get; set; } = "";
        public string CoreCustomImagePath { get; set; } = "";
        public string CoreCustomImageStretch { get; set; } = "UniformToFill";

        // Highlight Glow Customization (高亮边缘光晕自定义)
        public string HighlightGlowPreset { get; set; } = "Auto"; // "Auto", "Lilac", "Blue", "Emerald", "Rose", "Amber", "Red", "White", "Custom"
        public string HighlightGlowColor { get; set; } = ""; // Hex code like "#FFA855F7", or empty for auto
        public double HighlightGlowRadius { get; set; } = 24.0; // 8.0 ~ 48.0 px
        public double HighlightGlowOpacity { get; set; } = 0.85; // 0.0 ~ 1.0

        public string CustomSectorBg { get; set; } = "#9016161A";
        public string CustomSectorBorder { get; set; } = "#35FFFFFF";
        public string CustomHighlightBg { get; set; } = "#E06C4DFF";
        public string CustomHighlightBorder { get; set; } = "#A0FFFFFF";
        public string CustomText { get; set; } = "#E0FFFFFF";

        public List<CustomColorPreset> CustomColorPresets { get; set; } = new List<CustomColorPreset>();

        // Background Image & Texture Customization
        public string WheelBgImagePath { get; set; } = "";
        public double WheelBgOpacity { get; set; } = 0.8;
        public string WheelBgStretch { get; set; } = "UniformToFill"; // "UniformToFill", "Uniform", "Fill", "None"
        public string CoreBgImagePath { get; set; } = "";
        public double CoreBgOpacity { get; set; } = 1.0;
        public string CoreBgStretch { get; set; } = "UniformToFill";
        public string HighlightTexturePath { get; set; } = "";
        public double HighlightTextureOpacity { get; set; } = 0.7;

        public List<WheelProfile> Profiles { get; set; } = new List<WheelProfile>();

        // Scene Isolation Settings
        public List<string> BlacklistedProcesses { get; set; } = new List<string> { "mstsc.exe", "paint.exe" };
        public bool DisableOnCtrl { get; set; } = false;
        public bool DisableOnShift { get; set; } = false;
        public bool DisableOnAlt { get; set; } = false;
        public bool DisableOnFullScreen { get; set; } = true;
    }
}
