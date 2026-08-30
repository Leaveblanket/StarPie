using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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

    public static class ConfigManager
    {
        private static readonly string AppDataFolder = GetAppDataFolder();
        private static readonly string ConfigPath = Path.Combine(AppDataFolder, "config.json");

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

        public static AppConfig CurrentConfig { get; private set; }

        static ConfigManager()
        {
            LoadConfig();
        }

        public static void LoadConfig()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    CurrentConfig = JsonSerializer.Deserialize<AppConfig>(json, options) ?? CreateDefaultConfig();
                }
                else
                {
                    CurrentConfig = CreateDefaultConfig();
                    SaveConfig();
                }

                // Initialize internationalization language
                I18n.SetLanguage(CurrentConfig.Language);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
                CurrentConfig = CreateDefaultConfig();
                I18n.SetLanguage(CurrentConfig.Language);
            }
        }

        public static void SaveConfig()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CurrentConfig, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        public static WheelProfile GetProfileForProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                return GetGlobalProfile();
            }

            string lowerProc = processName.ToLower();
            var profile = CurrentConfig.Profiles.Find(p => p.ProcessName.ToLower() == lowerProc);
            return profile ?? GetGlobalProfile();
        }

        public static WheelProfile GetGlobalProfile()
        {
            var global = CurrentConfig.Profiles.Find(p => p.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase));
            if (global == null)
            {
                global = new WheelProfile { ProcessName = "Global", SectorCount = 8, Actions = new List<ActionItem>() };
                CurrentConfig.Profiles.Insert(0, global);
            }
            return global;
        }

        private static AppConfig CreateDefaultConfig()
        {
            var config = new AppConfig { DragThreshold = 25.0 };

            // Create Global default profile with 8 keys
            var globalProfile = new WheelProfile
            {
                ProcessName = "Global",
                SectorCount = 8,
                Actions = new List<ActionItem>
                {
                    new ActionItem { Type = "Hotkey", Name = "复制 (Copy)", Parameter = "Ctrl+C", IconKey = "Copy" },           // Index 0: Right (E)
                    new ActionItem { Type = "System", Name = "锁定电脑 (Lock)", Parameter = "Lock", IconKey = "Lock" },        // Index 1: Down-Right (SE)
                    new ActionItem { Type = "System", Name = "显示桌面 (Desktop)", Parameter = "ShowDesktop", IconKey = "ShowDesktop" }, // Index 2: Down (S)
                    new ActionItem { Type = "System", Name = "屏幕截图 (Capture)", Parameter = "Screenshot", IconKey = "Screenshot" }, // Index 3: Down-Left (SW)
                    new ActionItem { Type = "Hotkey", Name = "粘贴 (Paste)", Parameter = "Ctrl+V", IconKey = "Paste" },          // Index 4: Left (W)
                    new ActionItem { Type = "System", Name = "音量减 (Vol Down)", Parameter = "VolumeDown", IconKey = "VolumeDown" },  // Index 5: Up-Left (NW)
                    new ActionItem { Type = "Launch", Name = "记事本 (Notepad)", Parameter = "notepad.exe", IconKey = "Code" },   // Index 6: Up (N)
                    new ActionItem { Type = "System", Name = "音量增 (Vol Up)", Parameter = "VolumeUp", IconKey = "VolumeUp" }       // Index 7: Up-Right (NE)
                }
            };

            // Create Chrome specific profile with 4 keys for demo
            var chromeProfile = new WheelProfile
            {
                ProcessName = "chrome.exe",
                SectorCount = 4,
                Actions = new List<ActionItem>
                {
                    new ActionItem { Type = "Hotkey", Name = "关闭标签 (Close Tab)", Parameter = "Ctrl+W", IconKey = "CloseTab" },
                    new ActionItem { Type = "Hotkey", Name = "后退 (Back)", Parameter = "Alt+Left", IconKey = "Back" },
                    new ActionItem { Type = "Hotkey", Name = "新建标签 (New Tab)", Parameter = "Ctrl+T", IconKey = "NewTab" },
                    new ActionItem { Type = "Hotkey", Name = "刷新 (Refresh)", Parameter = "F5", IconKey = "Refresh" }
                }
            };

            // Create Visual Studio / VS Code profile
            var codeProfile = new WheelProfile
            {
                ProcessName = "code.exe",
                SectorCount = 8,
                Actions = new List<ActionItem>
                {
                    new ActionItem { Type = "Hotkey", Name = "定义跳转 (F12)", Parameter = "F12", IconKey = "Code" },
                    new ActionItem { Type = "Hotkey", Name = "格式化 (Format)", Parameter = "Shift+Alt+F", IconKey = "Edit" },
                    new ActionItem { Type = "Hotkey", Name = "控制台 (Terminal)", Parameter = "Ctrl+`", IconKey = "Terminal" },
                    new ActionItem { Type = "Hotkey", Name = "查找文件 (Quick Open)", Parameter = "Ctrl+P", IconKey = "Search" },
                    new ActionItem { Type = "Hotkey", Name = "保存全部 (Save All)", Parameter = "Ctrl+K,S", IconKey = "Save" },
                    new ActionItem { Type = "Hotkey", Name = "全局搜索 (Find in Files)", Parameter = "Ctrl+Shift+F", IconKey = "Search" },
                    new ActionItem { Type = "Hotkey", Name = "撤销 (Undo)", Parameter = "Ctrl+Z", IconKey = "Undo" },
                    new ActionItem { Type = "Hotkey", Name = "重做 (Redo)", Parameter = "Ctrl+Y", IconKey = "Redo" }
                }
            };

            config.Profiles.Add(globalProfile);
            config.Profiles.Add(chromeProfile);
            config.Profiles.Add(codeProfile);

            return config;
        }

        public static bool ExportConfig(string targetFilePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CurrentConfig, options);
                File.WriteAllText(targetFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to export config: {ex.Message}");
                return false;
            }
        }

        public static bool ImportConfig(string sourceFilePath)
        {
            try
            {
                if (!File.Exists(sourceFilePath)) return false;
                string json = File.ReadAllText(sourceFilePath);
                var imported = JsonSerializer.Deserialize<AppConfig>(json);
                if (imported != null)
                {
                    CurrentConfig = imported;
                    SaveConfig();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to import config: {ex.Message}");
            }
            return false;
        }

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
