using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinPieGestures
{
    /// <summary>
    /// JSON-file implementation of IConfigService: owns reading and writing
    /// config.json (same format and location as before), with the path injected
    /// via the constructor (production path computed by AppDataPaths in the
    /// composition root, tests
    /// inject a temp path). Load semantics match the previous static code: a
    /// missing file is seeded with the default config, corrupt JSON falls back
    /// to defaults without touching the file, and hand-edited files are
    /// tolerated (case-insensitive, comments and trailing commas allowed).
    /// </summary>
    public sealed class JsonConfigService : IConfigService
    {
        private readonly string _configPath;
        private AppConfig _config;

        public JsonConfigService(string configPath)
        {
            _configPath = configPath;
            _config = CreateDefaultConfig();
        }

        public AppConfig Current => _config;

        public void Load()
        {
            try
            {
                EnsureConfigDirectory();

                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    _config = JsonSerializer.Deserialize<AppConfig>(json, options) ?? CreateDefaultConfig();
                }
                else
                {
                    _config = CreateDefaultConfig();
                    Save();
                }

                // Initialize internationalization language
                I18n.SetLanguage(_config.Language);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
                _config = CreateDefaultConfig();
                I18n.SetLanguage(_config.Language);
            }
        }

        public void Save()
        {
            try
            {
                EnsureConfigDirectory();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        public WheelProfile GetProfileForProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                return GetGlobalProfile();
            }

            string lowerProc = processName.ToLower();
            var profile = _config.Profiles.Find(p => p.ProcessName.ToLower() == lowerProc);
            return profile ?? GetGlobalProfile();
        }

        public WheelProfile GetGlobalProfile()
        {
            var global = _config.Profiles.Find(p => p.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase));
            if (global == null)
            {
                global = new WheelProfile { ProcessName = "Global", SectorCount = 8, Actions = new List<ActionItem>() };
                _config.Profiles.Insert(0, global);
            }
            return global;
        }

        public bool Export(string targetFilePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(targetFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to export config: {ex.Message}");
                return false;
            }
        }

        public bool Import(string sourceFilePath)
        {
            try
            {
                if (!File.Exists(sourceFilePath)) return false;
                string json = File.ReadAllText(sourceFilePath);
                var imported = JsonSerializer.Deserialize<AppConfig>(json);
                if (imported != null)
                {
                    _config = imported;
                    Save();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to import config: {ex.Message}");
            }
            return false;
        }

        private void EnsureConfigDirectory()
        {
            string? directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
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
    }
}
