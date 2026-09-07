using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StarPie.Services.Localization;

namespace StarPie.Services.Configuration
{
    /// <summary>
    /// <see cref="IConfigService"/> 的 JSON 文件实现：独占 config.json 的读写。
    /// </summary>
    /// <remarks>
    /// 读写格式与位置向后兼容既有版本。配置文件路径经构造函数注入——生产路径由组合根经
    /// <see cref="AppDataPaths"/> 计算，测试注入临时路径。加载语义：文件缺失时播种默认配置，
    /// JSON 损坏时回退默认值（不触碰文件），并容忍手工编辑（大小写不敏感、允许注释与尾随逗号）。
    /// </remarks>
    public sealed class JsonConfigService : IConfigService
    {
        private readonly string _configPath;
        private readonly ILocalizationService _localization;
        private AppConfig _config;

        public JsonConfigService(string configPath, ILocalizationService localization)
        {
            _configPath = configPath;
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _config = CreateDefaultConfig();
        }

        /// <summary>当前运行态配置（构造函数先落默认值，Load 后替换为磁盘内容）。</summary>
        public AppConfig Current => _config;

        /// <summary>
        /// 从磁盘加载配置。文件存在时以宽松选项反序列化（大小写不敏感、允许注释与尾随逗号）；
        /// 缺失时播种默认配置并立即落盘；随后按配置语言初始化本地化服务。
        /// 任一步骤失败均回退默认配置，不向调用方抛异常。
        /// </summary>
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

                // 按配置的语言设置初始化本地化服务（语言状态单一来源）。
                _localization.SetLanguage(_config.Language);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
                _config = CreateDefaultConfig();
                _localization.SetLanguage(_config.Language);
            }
        }

        /// <summary>把当前配置以缩进 JSON 写回磁盘；目录不存在时先创建，失败仅输出 Debug 日志。</summary>
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

        /// <summary>
        /// 按进程名查找专属方案（忽略大小写）；进程名为空或未找到时回退
        /// <see cref="GetGlobalProfile"/>（保证永远返回可用方案）。
        /// </summary>
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

        /// <summary>取 Global 方案；缺失时在列表头部插入一个空的 Global 方案并返回。</summary>
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

        /// <summary>把当前配置导出到指定文件；成功返回 true，失败仅输出 Debug 日志并返回 false。</summary>
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

        /// <summary>
        /// 从指定文件导入配置：反序列化成功后替换当前配置并立即落盘。
        /// 源文件缺失、JSON 非法或反序列化失败均返回 false（不影响现有配置）。
        /// </summary>
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

        /// <summary>确保配置文件所在目录存在（不存在则创建）。</summary>
        private void EnsureConfigDirectory()
        {
            string? directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>构建默认配置：Global（8 键）+ Chrome（4 键）+ VS Code（8 键）三个示例方案。</summary>
        private static AppConfig CreateDefaultConfig()
        {
            var config = new AppConfig { DragThreshold = 25.0 };

            // 创建 Global 全局默认方案（8 个动作；注释中的方位为轮盘上动作的落位）。
            var globalProfile = new WheelProfile
            {
                ProcessName = "Global",
                SectorCount = 8,
                Actions = new List<ActionItem>
                {
                    new ActionItem { Type = "Hotkey", Name = "复制 (Copy)", Parameter = "Ctrl+C", IconKey = "Copy" },           // 索引 0：右 (E)
                    new ActionItem { Type = "System", Name = "锁定电脑 (Lock)", Parameter = "Lock", IconKey = "Lock" },        // 索引 1：右下 (SE)
                    new ActionItem { Type = "System", Name = "显示桌面 (Desktop)", Parameter = "ShowDesktop", IconKey = "ShowDesktop" }, // 索引 2：下 (S)
                    new ActionItem { Type = "System", Name = "屏幕截图 (Capture)", Parameter = "Screenshot", IconKey = "Screenshot" }, // 索引 3：左下 (SW)
                    new ActionItem { Type = "Hotkey", Name = "粘贴 (Paste)", Parameter = "Ctrl+V", IconKey = "Paste" },          // 索引 4：左 (W)
                    new ActionItem { Type = "System", Name = "音量减 (Vol Down)", Parameter = "VolumeDown", IconKey = "VolumeDown" },  // 索引 5：左上 (NW)
                    new ActionItem { Type = "Launch", Name = "记事本 (Notepad)", Parameter = "notepad.exe", IconKey = "Code" },   // 索引 6：上 (N)
                    new ActionItem { Type = "System", Name = "音量增 (Vol Up)", Parameter = "VolumeUp", IconKey = "VolumeUp" }       // 索引 7：右上 (NE)
                }
            };

            // 创建 Chrome 专用示例方案（4 个动作）。
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

            // 创建 VS Code（code.exe）专属方案。
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
