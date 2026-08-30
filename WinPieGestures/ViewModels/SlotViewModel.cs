using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinPieGestures
{
    /// <summary>
    /// 系统预设动作条目（T11 随槽位 ViewModel 一并自 SettingsWindow 迁入，数据一字未动）。
    /// </summary>
    public class SystemPresetItem
    {
        public string Key { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DefaultName { get; set; } = "";
        public string DefaultIconKey { get; set; } = "";
        public string FormattedDisplay => $"[{Category}] {DisplayName}";
    }

    /// <summary>
    /// 方向槽位 ViewModel (T11, ADR-0001)：自 SettingsWindow 的窗口内私有槽位 ViewModel 迁入
    /// 正式 ViewModel，包装扇区绑定的 <see cref="ActionItem"/> 提供槽位编辑绑定。
    /// 本票迁移列表侧职责（方向槽位集合与槽位名称编辑），名称编辑与迁移前一致——直写模型、
    /// 无额外验证；类型/参数/图标等动作编辑属性暂保持迁移前行为，其调用点归 T12 迁移。
    /// </summary>
    public class SlotViewModel : ObservableObject
    {
        public static readonly List<SystemPresetItem> SystemPresetList = new List<SystemPresetItem>
        {
            // 窗口与工作区
            new SystemPresetItem { Key = "CloseWindow", Category = "窗口管理", DisplayName = "关闭当前窗口 (Close / Alt+F4)", DefaultName = "关闭窗口", DefaultIconKey = "CloseWindow" },
            new SystemPresetItem { Key = "Minimize", Category = "窗口管理", DisplayName = "最小化窗口 (Minimize / Win+Down)", DefaultName = "最小化", DefaultIconKey = "Minimize" },
            new SystemPresetItem { Key = "Maximize", Category = "窗口管理", DisplayName = "最大化/还原 (Maximize / Win+Up)", DefaultName = "最大化", DefaultIconKey = "Maximize" },
            new SystemPresetItem { Key = "SnapLeft", Category = "窗口管理", DisplayName = "左半屏贴靠 (Snap Left / Win+Left)", DefaultName = "靠左分屏", DefaultIconKey = "SnapLeft" },
            new SystemPresetItem { Key = "SnapRight", Category = "窗口管理", DisplayName = "右半屏贴靠 (Snap Right / Win+Right)", DefaultName = "靠右分屏", DefaultIconKey = "SnapRight" },
            new SystemPresetItem { Key = "TaskView", Category = "窗口管理", DisplayName = "任务视图/多任务 (Task View / Win+Tab)", DefaultName = "任务视图", DefaultIconKey = "TaskView" },
            new SystemPresetItem { Key = "PrevDesktop", Category = "窗口管理", DisplayName = "上一虚拟桌面 (Prev Desktop)", DefaultName = "上一桌面", DefaultIconKey = "PrevDesktop" },
            new SystemPresetItem { Key = "NextDesktop", Category = "窗口管理", DisplayName = "下一虚拟桌面 (Next Desktop)", DefaultName = "下一桌面", DefaultIconKey = "NextDesktop" },
            new SystemPresetItem { Key = "ShowDesktop", Category = "窗口管理", DisplayName = "显示桌面 (Desktop / Win+D)", DefaultName = "显示桌面", DefaultIconKey = "ShowDesktop" },
            new SystemPresetItem { Key = "FullScreen", Category = "窗口管理", DisplayName = "全屏切换 (Full Screen / F11)", DefaultName = "全屏切换", DefaultIconKey = "FullScreen" },
            new SystemPresetItem { Key = "Screenshot", Category = "窗口管理", DisplayName = "屏幕截图 (Screenshot / Win+Shift+S)", DefaultName = "屏幕截图", DefaultIconKey = "Screenshot" },

            // 系统管理与实用工具
            new SystemPresetItem { Key = "TaskManager", Category = "系统工具", DisplayName = "任务管理器 (Task Manager / Ctrl+Shift+Esc)", DefaultName = "任务管理器", DefaultIconKey = "TaskManager" },
            new SystemPresetItem { Key = "Explorer", Category = "系统工具", DisplayName = "文件资源管理器 (Explorer / Win+E)", DefaultName = "资源管理器", DefaultIconKey = "Explorer" },
            new SystemPresetItem { Key = "Settings", Category = "系统工具", DisplayName = "Windows 设置 (Settings / Win+I)", DefaultName = "系统设置", DefaultIconKey = "Settings" },
            new SystemPresetItem { Key = "Calculator", Category = "系统工具", DisplayName = "计算器 (Calculator / calc.exe)", DefaultName = "计算器", DefaultIconKey = "Calculator" },
            new SystemPresetItem { Key = "RunDialog", Category = "系统工具", DisplayName = "运行窗口 (Run / Win+R)", DefaultName = "运行", DefaultIconKey = "RunDialog" },
            new SystemPresetItem { Key = "WindowsSearch", Category = "系统工具", DisplayName = "系统搜索 (Search / Win+S)", DefaultName = "搜索", DefaultIconKey = "WindowsSearch" },
            new SystemPresetItem { Key = "ClipboardHistory", Category = "系统工具", DisplayName = "剪贴板历史 (Clipboard / Win+V)", DefaultName = "剪贴板", DefaultIconKey = "ClipboardHistory" },
            new SystemPresetItem { Key = "Lock", Category = "系统工具", DisplayName = "锁定电脑 (Lock Workstation)", DefaultName = "锁定电脑", DefaultIconKey = "Lock" },

            // 多媒体与音量
            new SystemPresetItem { Key = "VolumeUp", Category = "媒体音效", DisplayName = "音量增加 (Volume Up)", DefaultName = "音量加", DefaultIconKey = "VolumeUp" },
            new SystemPresetItem { Key = "VolumeDown", Category = "媒体音效", DisplayName = "音量减小 (Volume Down)", DefaultName = "音量减", DefaultIconKey = "VolumeDown" },
            new SystemPresetItem { Key = "VolumeMute", Category = "媒体音效", DisplayName = "静音切换 (Mute)", DefaultName = "静音切换", DefaultIconKey = "VolumeMute" },
            new SystemPresetItem { Key = "PlayPause", Category = "媒体音效", DisplayName = "播放/暂停 (Play/Pause)", DefaultName = "播放/暂停", DefaultIconKey = "PlayPause" },
            new SystemPresetItem { Key = "NextTrack", Category = "媒体音效", DisplayName = "下一曲 (Next Track)", DefaultName = "下一曲", DefaultIconKey = "NextTrack" },
            new SystemPresetItem { Key = "PrevTrack", Category = "媒体音效", DisplayName = "上一曲 (Previous Track)", DefaultName = "上一曲", DefaultIconKey = "PrevTrack" },
            new SystemPresetItem { Key = "StopMedia", Category = "媒体音效", DisplayName = "停止播放 (Stop)", DefaultName = "停止", DefaultIconKey = "VolumeMute" },

            // 浏览器与文档
            new SystemPresetItem { Key = "NewTab", Category = "网页浏览", DisplayName = "新建标签页 (New Tab / Ctrl+T)", DefaultName = "新建标签", DefaultIconKey = "NewTab" },
            new SystemPresetItem { Key = "CloseTab", Category = "网页浏览", DisplayName = "关闭标签页 (Close Tab / Ctrl+W)", DefaultName = "关闭标签", DefaultIconKey = "CloseTab" },
            new SystemPresetItem { Key = "ReopenTab", Category = "网页浏览", DisplayName = "恢复关闭标签 (Reopen / Ctrl+Shift+T)", DefaultName = "恢复标签", DefaultIconKey = "ReopenTab" },
            new SystemPresetItem { Key = "Refresh", Category = "网页浏览", DisplayName = "刷新页面 (Refresh / F5)", DefaultName = "刷新", DefaultIconKey = "Refresh" },
            new SystemPresetItem { Key = "HardRefresh", Category = "网页浏览", DisplayName = "强制刷新 (Hard Refresh / Ctrl+F5)", DefaultName = "刷新", DefaultIconKey = "Refresh" },
            new SystemPresetItem { Key = "ZoomIn", Category = "网页浏览", DisplayName = "页面放大 (Zoom In / Ctrl++)", DefaultName = "放大", DefaultIconKey = "ZoomIn" },
            new SystemPresetItem { Key = "ZoomOut", Category = "网页浏览", DisplayName = "页面缩小 (Zoom Out / Ctrl+-)", DefaultName = "缩小", DefaultIconKey = "ZoomOut" },
            new SystemPresetItem { Key = "ZoomReset", Category = "网页浏览", DisplayName = "默认缩放 (Reset Zoom / Ctrl+0)", DefaultName = "默认缩放", DefaultIconKey = "ZoomReset" },

            // 电源管理
            new SystemPresetItem { Key = "Sleep", Category = "电源控制", DisplayName = "系统睡眠 (Sleep)", DefaultName = "睡眠", DefaultIconKey = "Sleep" },
            new SystemPresetItem { Key = "Restart", Category = "电源控制", DisplayName = "重启电脑 (Restart)", DefaultName = "重启", DefaultIconKey = "Restart" },
            new SystemPresetItem { Key = "Shutdown", Category = "电源控制", DisplayName = "关闭电脑 (Shutdown)", DefaultName = "关机", DefaultIconKey = "Shutdown" }
        };

        public static readonly Dictionary<string, string> SystemPresets = SystemPresetList.ToDictionary(x => x.Key, x => x.FormattedDisplay);

        public string DirectionLabel { get; }
        public ActionItem Action { get; }

        public string Name
        {
            get => Action.Name ?? "";
            set
            {
                if (Action.Name != value)
                {
                    Action.Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Type
        {
            get => string.IsNullOrEmpty(Action.Type) ? "Hotkey" : Action.Type;
            set
            {
                if (Action.Type != value && !string.IsNullOrEmpty(value))
                {
                    Action.Type = value;
                    if ((value == "Folder" || value == "OpenFolder") && string.IsNullOrEmpty(IconKey))
                    {
                        IconKey = "Folder";
                        if (string.IsNullOrEmpty(Name) || Name.StartsWith("快捷动作") || Name.StartsWith("动作"))
                        {
                            Name = I18n.T("ActionTypeFolderShort");
                        }
                    }
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(IsHotkeyType));
                    OnPropertyChanged(nameof(IsLaunchType));
                    OnPropertyChanged(nameof(IsFolderType));
                    OnPropertyChanged(nameof(IsSystemType));
                }
            }
        }

        public string Parameter
        {
            get => Action.Parameter ?? "";
            set
            {
                if (Action.Parameter != value)
                {
                    Action.Parameter = value;
                    OnPropertyChanged(nameof(Parameter));
                }
            }
        }

        public string Arguments
        {
            get => Action.Arguments ?? "";
            set
            {
                if (Action.Arguments != value)
                {
                    Action.Arguments = value;
                    OnPropertyChanged(nameof(Arguments));
                }
            }
        }

        public string IconKey
        {
            get => Action.IconKey ?? "";
            set
            {
                if (Action.IconKey != value)
                {
                    Action.IconKey = value;
                    OnPropertyChanged(nameof(IconKey));
                    OnPropertyChanged(nameof(IconDisplayText));
                    OnPropertyChanged(nameof(HasVectorIcon));
                    OnPropertyChanged(nameof(VectorIconData));
                }
            }
        }

        public string CustomIconSvg
        {
            get => Action.CustomIconSvg ?? "";
            set
            {
                if (Action.CustomIconSvg != value)
                {
                    Action.CustomIconSvg = value;
                    OnPropertyChanged(nameof(CustomIconSvg));
                    OnPropertyChanged(nameof(IconDisplayText));
                    OnPropertyChanged(nameof(HasVectorIcon));
                    OnPropertyChanged(nameof(VectorIconData));
                }
            }
        }

        public string IconDisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(IconKey)) return IconKey;
                if (!string.IsNullOrEmpty(CustomIconSvg)) return "自定义SVG";
                return "图标...";
            }
        }

        public bool HasVectorIcon => VectorIconData != null;

        public Geometry? VectorIconData
        {
            get
            {
                string? data = null;
                if (!string.IsNullOrEmpty(CustomIconSvg)) data = CustomIconSvg;
                else if (!string.IsNullOrEmpty(IconKey))
                {
                    if (IconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                    {
                        var custom = IconHelper.GetCustomIcons().FirstOrDefault(c => c.Key == IconKey);
                        if (custom != null && custom.IsSvg) data = custom.SvgData;
                    }
                    else
                    {
                        data = IconHelper.GetSvgPathByKey(IconKey);
                    }
                }

                if (!string.IsNullOrEmpty(data))
                {
                    try
                    {
                        return Geometry.Parse(data);
                    }
                    catch { }
                }
                return null;
            }
        }

        public string SelectedSystemPreset
        {
            get => Action.Type == "System" ? (Action.Parameter ?? "Lock") : "Lock";
            set
            {
                if (Action.Parameter != value && !string.IsNullOrEmpty(value))
                {
                    Action.Parameter = value;
                    OnPropertyChanged(nameof(SelectedSystemPreset));
                    OnPropertyChanged(nameof(Parameter));

                    // Auto associate default friendly name and icon if matching
                    var preset = SystemPresetList.FirstOrDefault(x => string.Equals(x.Key, value, StringComparison.OrdinalIgnoreCase));
                    if (preset != null)
                    {
                        if (string.IsNullOrEmpty(Name) || Name == "快捷动作" || SystemPresetList.Any(p => p.DefaultName == Name))
                        {
                            Name = preset.DefaultName;
                        }
                        if (string.IsNullOrEmpty(IconKey) || SystemPresetList.Any(p => p.DefaultIconKey == IconKey))
                        {
                            IconKey = preset.DefaultIconKey;
                        }
                    }
                }
            }
        }

        public bool IsHotkeyType => Type == "Hotkey";
        public bool IsLaunchType => Type == "Launch";
        public bool IsFolderType => Type == "Folder" || Type == "OpenFolder";
        public bool IsSystemType => Type == "System";

        public class ActionTypeOption
        {
            public string Tag { get; set; } = "";
            public string DisplayText { get; set; } = "";
        }

        public List<ActionTypeOption> ActionTypes => new List<ActionTypeOption>
        {
            new ActionTypeOption { Tag = "Hotkey", DisplayText = I18n.T("ActionTypeHotkeyShort") },
            new ActionTypeOption { Tag = "Launch", DisplayText = I18n.T("ActionTypeLaunchShort") },
            new ActionTypeOption { Tag = "Folder", DisplayText = I18n.T("ActionTypeFolderShort") },
            new ActionTypeOption { Tag = "System", DisplayText = I18n.T("ActionTypeSystemShort") }
        };

        public string TestButtonText => I18n.T("BtnTest");

        public SlotViewModel(string directionLabel, ActionItem action)
        {
            DirectionLabel = directionLabel;
            Action = action ?? new ActionItem { Type = "Hotkey", Name = "快捷动作", Parameter = "" };

            I18n.LanguageChanged += () =>
            {
                OnPropertyChanged(nameof(ActionTypes));
                OnPropertyChanged(nameof(TestButtonText));
                OnPropertyChanged(nameof(IconDisplayText));
            };
        }
    }
}
