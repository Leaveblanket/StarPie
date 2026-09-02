using System.Collections.Generic;

namespace WinPieGestures.Models
{
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
