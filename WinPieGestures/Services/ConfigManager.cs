using System;
using System.Collections.Generic;
using System.IO;

namespace WinPieGestures
{
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

    /// <summary>
    /// Transitional facade for the expand phase (ADR-0002): config file I/O has
    /// moved to <see cref="JsonConfigService"/>; this class keeps only path
    /// computation (dev sandbox + legacy folder migration), the autostart
    /// registry entry, and static-call forwarding to the single service
    /// instance. Not-yet-migrated callers (settings window, radial window) keep
    /// working through it until they switch to injected IConfigService, then
    /// the whole facade is removed.
    /// </summary>
    public static class ConfigManager
    {
        private static readonly JsonConfigService Service = new(Path.Combine(GetAppDataFolder(), "config.json"));

        /// <summary>唯一的服务实例，供组合根取出并注入手势链路与应用侧。</summary>
        internal static IConfigService ConfigService => Service;

        internal static string GetAppDataFolder()
        {
            string baseFolder = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOCALAPPDATA"))
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetEnvironmentVariable("LOCALAPPDATA")!;

            if (DevInstance.IsActive)
            {
                // Dev instances sandbox into their own folder so the installed release's
                // config is never touched; seed it once from the real config if present.
                string devFolder = Path.Combine(baseFolder, DevInstance.FolderName);
                try
                {
                    string devConfig = Path.Combine(devFolder, "config.json");
                    string releaseConfig = Path.Combine(baseFolder, "StarPie", "config.json");
                    if (!File.Exists(devConfig) && File.Exists(releaseConfig))
                    {
                        Directory.CreateDirectory(devFolder);
                        File.Copy(releaseConfig, devConfig);
                    }
                }
                catch { }
                return devFolder;
            }

            string starPieFolder = Path.Combine(baseFolder, DevInstance.FolderName);
            string legacyFolder = Path.Combine(baseFolder, "WinPieGestures");
            
            // Auto migrate from legacy folder if needed
            if (!Directory.Exists(starPieFolder) && Directory.Exists(legacyFolder))
            {
                try
                {
                    Directory.CreateDirectory(starPieFolder);
                    string legacyConfig = Path.Combine(legacyFolder, "config.json");
                    string starPieConfig = Path.Combine(starPieFolder, "config.json");
                    if (File.Exists(legacyConfig) && !File.Exists(starPieConfig))
                    {
                        File.Copy(legacyConfig, starPieConfig);
                    }
                }
                catch { }
            }
            return starPieFolder;
        }

        public static AppConfig CurrentConfig => Service.Current;

        static ConfigManager()
        {
            LoadConfig();
        }

        public static void LoadConfig() => Service.Load();

        public static void SaveConfig() => Service.Save();

        public static WheelProfile GetProfileForProcess(string processName) => Service.GetProfileForProcess(processName);

        public static WheelProfile GetGlobalProfile() => Service.GetGlobalProfile();

        public static bool ExportConfig(string targetFilePath) => Service.Export(targetFilePath);

        public static bool ImportConfig(string sourceFilePath) => Service.Import(sourceFilePath);

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("StarPie") != null || key?.GetValue("WinPieGestures") != null;
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            // Dev instances must not repoint the real autostart entry at the dev executable
            if (DevInstance.IsActive) return;

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");
                    key.SetValue("StarPie", $"\"{exePath}\"");
                    // Clean up legacy key if present
                    try { key.DeleteValue("WinPieGestures", false); } catch { }
                }
                else
                {
                    key.DeleteValue("StarPie", false);
                    try { key.DeleteValue("WinPieGestures", false); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set autostart: {ex.Message}");
            }
        }
    }
}
