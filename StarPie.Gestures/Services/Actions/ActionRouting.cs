using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace WinPieGestures.Services.Actions
{
    /// <summary>动作执行的路由种类 (T15)。与迁移前 switch 一致：类型按原值大小写敏感匹配。</summary>
    public enum ActionRoute
    {
        Launch,
        Folder,
        Hotkey,
        System,
        Unknown,
    }

    /// <summary>
    /// 一次按键注入的纯数据描述 (T15)：虚拟键、按下/抬起，以及按迁移前 CreateKeyInput
    /// 规则推导的扩展键标志（方向/导航、Win、媒体、浏览器键段）。
    /// </summary>
    public readonly record struct KeyStroke
    {
        public ushort VirtualKey { get; }

        public bool KeyDown { get; }

        public bool Extended { get; }

        public KeyStroke(ushort virtualKey, bool down)
        {
            VirtualKey = virtualKey;
            KeyDown = down;
            Extended = IsExtendedKey(virtualKey);
        }

        /// <summary>迁移前 CreateKeyInput 的扩展键键段，逐段保留。</summary>
        public static bool IsExtendedKey(ushort vk)
            => vk is >= 0x21 and <= 0x2F or >= 0x5B and <= 0x5C or >= 0xAD and <= 0xB3 or >= 0xA6 and <= 0xAC;
    }

    /// <summary>
    /// 动作路由纯函数 (T15, ADR-0002)：迁移前静态 ActionExecutor 的全部决策逻辑——
    /// 动作类型路由、系统命令映射、启动/文件夹 StartInfo 构造、热键弦解析与键序生成——
    /// 提炼为无副作用的纯函数；进程启动/键注入等系统调用由 ActionExecutorService 注入。
    /// 大小写与文本语义逐字保留：类型路由大小写敏感，系统预设与键名大小写不敏感。
    /// </summary>
    public static class ActionRouting
    {
        /// <summary>动作类型路由（大小写敏感、去首尾空白——与迁移前 switch 一致；null 与迁移前一样抛出并由调用方兜底）。</summary>
        public static ActionRoute ResolveRoute(string type)
        {
            switch (type.Trim())
            {
                case "Launch": return ActionRoute.Launch;
                case "Folder":
                case "OpenFolder": return ActionRoute.Folder;
                case "Hotkey": return ActionRoute.Hotkey;
                case "System": return ActionRoute.System;
                default: return ActionRoute.Unknown;
            }
        }

        /// <summary>系统命令的判定结果：无副作用、发键序、发单键、锁屏或启动进程。
        /// 全部为 record——测试按值断言路由结果。</summary>
        public abstract record SystemCommand
        {
            private protected SystemCommand() { }

            /// <summary>空/未知预设：不产生任何动作；未知时携带原名（迁移前仅 Debug 输出）。</summary>
            public sealed record Noop(string? UnknownPreset) : SystemCommand
            {
                public static Noop Instance { get; } = new Noop(UnknownPreset: null);
            }

            /// <summary>发送一条热键弦（如 "Win+Left"）。</summary>
            public sealed record SendHotkey(string Hotkey) : SystemCommand;

            /// <summary>发送单个媒体/音量键。</summary>
            public sealed record SendKey(ushort VirtualKey) : SystemCommand
            {
                public static SendKey VolumeUp { get; } = new(0xAF);
                public static SendKey VolumeDown { get; } = new(0xAE);
                public static SendKey VolumeMute { get; } = new(0xAD);
            }

            /// <summary>锁定工作站（LockWorkStation P/Invoke）。</summary>
            public sealed record LockWorkstation : SystemCommand
            {
                public static LockWorkstation Instance { get; } = new();

                private LockWorkstation() { }
            }

            /// <summary>启动进程；<paramref name="FallbackHotkey"/> 非空时启动失败降级发键，
            /// <paramref name="silent"/> 为 true 时启动失败静默（电源类命令，迁移前语义）。</summary>
            public sealed record StartProcess(string FileName, string Arguments, string? FallbackHotkey, bool Silent) : SystemCommand;
        }

        /// <summary>系统预设映射（大小写不敏感、去空白——迁移前 ToLower 语义）。空/未知 → Noop。</summary>
        public static SystemCommand ResolveSystemCommand(string? presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return SystemCommand.Noop.Instance;

            switch (presetName.Trim().ToLower())
            {
                // Window Management
                case "closewindow": return new SystemCommand.SendHotkey("Alt+F4");
                case "minimize": return new SystemCommand.SendHotkey("Win+Down");
                case "maximize": return new SystemCommand.SendHotkey("Win+Up");
                case "snapleft": return new SystemCommand.SendHotkey("Win+Left");
                case "snapright": return new SystemCommand.SendHotkey("Win+Right");
                case "taskview": return new SystemCommand.SendHotkey("Win+Tab");
                case "prevdesktop": return new SystemCommand.SendHotkey("Win+Ctrl+Left");
                case "nextdesktop": return new SystemCommand.SendHotkey("Win+Ctrl+Right");
                case "showdesktop": return new SystemCommand.SendHotkey("Win+D");
                case "fullscreen": return new SystemCommand.SendHotkey("F11");
                case "screenshot": return new SystemCommand.SendHotkey("Win+Shift+S");

                // System & Utilities
                case "taskmanager": return new SystemCommand.StartProcess("taskmgr.exe", "", "Ctrl+Shift+Esc", Silent: false);
                case "explorer": return new SystemCommand.StartProcess("explorer.exe", "", "Win+E", Silent: false);
                case "settings": return new SystemCommand.StartProcess("ms-settings:", "", "Win+I", Silent: false);
                case "calculator": return new SystemCommand.StartProcess("calc.exe", "", "Win+R", Silent: false);
                case "rundialog": return new SystemCommand.SendHotkey("Win+R");
                case "windowssearch": return new SystemCommand.SendHotkey("Win+S");
                case "clipboardhistory": return new SystemCommand.SendHotkey("Win+V");
                case "lock": return SystemCommand.LockWorkstation.Instance;

                // Media & Volume
                case "volumeup": return SystemCommand.SendKey.VolumeUp;
                case "volumedown": return SystemCommand.SendKey.VolumeDown;
                case "volumemute": return SystemCommand.SendKey.VolumeMute;
                case "playpause": return new SystemCommand.SendKey(0xB3); // VK_MEDIA_PLAY_PAUSE
                case "nexttrack": return new SystemCommand.SendKey(0xB0); // VK_MEDIA_NEXT_TRACK
                case "prevtrack": return new SystemCommand.SendKey(0xB1); // VK_MEDIA_PREV_TRACK
                case "stopmedia": return new SystemCommand.SendKey(0xB2); // VK_MEDIA_STOP

                // Browser Navigation
                case "newtab": return new SystemCommand.SendHotkey("Ctrl+T");
                case "closetab": return new SystemCommand.SendHotkey("Ctrl+W");
                case "reopentab": return new SystemCommand.SendHotkey("Ctrl+Shift+T");
                case "refresh": return new SystemCommand.SendHotkey("F5");
                case "hardrefresh": return new SystemCommand.SendHotkey("Ctrl+F5");
                case "zoomin": return new SystemCommand.SendHotkey("Ctrl+Plus");
                case "zoomout": return new SystemCommand.SendHotkey("Ctrl+Minus");
                case "zoomreset": return new SystemCommand.SendHotkey("Ctrl+0");

                // Power Management
                case "sleep": return new SystemCommand.StartProcess("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0", null, Silent: true);
                case "restart": return new SystemCommand.StartProcess("shutdown.exe", "/r /t 0", null, Silent: true);
                case "shutdown": return new SystemCommand.StartProcess("shutdown.exe", "/s /t 0", null, Silent: true);

                default:
                    Debug.WriteLine($"Unknown system preset: {presetName}");
                    return new SystemCommand.Noop(presetName);
            }
        }

        /// <summary>启动程序 StartInfo：Arguments 空时归一为空串，WorkingDirectory 保持未设（迁移前语义：子进程继承调用方目录）。</summary>
        public static ProcessStartInfo BuildLaunchStartInfo(string path, string? arguments)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Launch path is empty", nameof(path));

            return new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            };
        }

        /// <summary>打开文件夹/文件的 StartInfo（三条分支：开目录、选中文件、直接 Shell 执行）。
        /// 迁移前的环境变量展开与引号修剪在存在性检查之前进行。</summary>
        public static ProcessStartInfo BuildFolderStartInfo(string folderPath, bool isDirectory, bool isFile)
        {
            string expandedPath = Environment.ExpandEnvironmentVariables(folderPath.Trim().Trim('"'));
            if (isDirectory)
            {
                return new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{expandedPath}\"",
                    UseShellExecute = true
                };
            }

            if (isFile)
            {
                return new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{expandedPath}\"",
                    UseShellExecute = true
                };
            }

            return new ProcessStartInfo
            {
                FileName = expandedPath,
                UseShellExecute = true
            };
        }

        /// <summary>展开引号内的环境变量（错误提示文案使用迁移前的原始输入）。</summary>
        public static string ExpandFolderPath(string folderPath)
            => Environment.ExpandEnvironmentVariables(folderPath.Trim().Trim('"'));

        /// <summary>
        /// 热键弦 → 键序：修饰键按下（按弦内出现顺序、去重）→ 主键按下 → 主键抬起 →
        /// 修饰键逆序抬起。空/不可解析弦产出空序列；纯修饰键弦按下并抬起该修饰键。
        /// </summary>
        public static IReadOnlyList<KeyStroke> BuildKeySequence(string? hotkeyString)
        {
            var strokes = new List<KeyStroke>();
            if (string.IsNullOrEmpty(hotkeyString)) return strokes;

            var keys = ParseHotkey(hotkeyString);
            if (keys.Modifiers.Count == 0 && keys.MainKey == 0) return strokes;

            foreach (var vk in keys.Modifiers)
            {
                strokes.Add(new KeyStroke(vk, down: true));
            }

            if (keys.MainKey != 0)
            {
                strokes.Add(new KeyStroke(keys.MainKey, down: true));
                strokes.Add(new KeyStroke(keys.MainKey, down: false));
            }

            for (int i = keys.Modifiers.Count - 1; i >= 0; i--)
            {
                strokes.Add(new KeyStroke(keys.Modifiers[i], down: false));
            }

            return strokes;
        }

        /// <summary>单个媒体/音量键的按下+抬起键序。</summary>
        public static IReadOnlyList<KeyStroke> BuildSingleKeyStrokes(ushort vk)
            => new[] { new KeyStroke(vk, down: true), new KeyStroke(vk, down: false) };

        private class HotkeyDetails
        {
            public List<ushort> Modifiers { get; } = new List<ushort>();
            public ushort MainKey { get; set; } = 0;
        }

        private static HotkeyDetails ParseHotkey(string hotkeyString)
        {
            var details = new HotkeyDetails();
            var parts = hotkeyString.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string token = part.Trim().ToLower();
                if (token == "ctrl" || token == "control" || token == "lctrl" || token == "rctrl")
                {
                    if (!details.Modifiers.Contains(VK_LCONTROL)) details.Modifiers.Add(VK_LCONTROL);
                }
                else if (token == "shift" || token == "lshift" || token == "rshift")
                {
                    if (!details.Modifiers.Contains(VK_LSHIFT)) details.Modifiers.Add(VK_LSHIFT);
                }
                else if (token == "alt" || token == "menu" || token == "lalt" || token == "ralt")
                {
                    if (!details.Modifiers.Contains(VK_LMENU)) details.Modifiers.Add(VK_LMENU);
                }
                else if (token == "win" || token == "lwin" || token == "rwin" || token == "windows")
                {
                    if (!details.Modifiers.Contains(VK_LWIN)) details.Modifiers.Add(VK_LWIN);
                }
                else
                {
                    ushort vk = MapKeyStringToVk(token);
                    if (vk != 0) details.MainKey = vk;
                }
            }

            return details;
        }

        private const ushort VK_LCONTROL = 0xA2;
        private const ushort VK_LSHIFT = 0xA0;
        private const ushort VK_LMENU = 0xA4; // Alt
        private const ushort VK_LWIN = 0x5B;

        private const ushort VK_VOLUME_MUTE = 0xAD;
        private const ushort VK_VOLUME_DOWN = 0xAE;
        private const ushort VK_VOLUME_UP = 0xAF;

        private const ushort VK_LEFT = 0x25;
        private const ushort VK_UP = 0x26;
        private const ushort VK_RIGHT = 0x27;
        private const ushort VK_DOWN = 0x28;
        private const ushort VK_ESCAPE = 0x1B;
        private const ushort VK_RETURN = 0x0D;
        private const ushort VK_TAB = 0x09;
        private const ushort VK_SPACE = 0x20;

        private static ushort MapKeyStringToVk(string keyToken)
        {
            if (string.IsNullOrEmpty(keyToken)) return 0;
            string token = keyToken.ToLower().Trim();

            // Single Character A-Z, 0-9
            if (token.Length == 1)
            {
                char c = token[0];
                if (c >= 'a' && c <= 'z') return (ushort)('A' + (c - 'a'));
                if (c >= '0' && c <= '9') return (ushort)c;

                switch (c)
                {
                    case ';': return 0xBA;
                    case '=':
                    case '+': return 0xBB;
                    case ',': return 0xBC;
                    case '-': return 0xBD;
                    case '.': return 0xBE;
                    case '/': return 0xBF;
                    case '`': return 0xC0;
                    case '[': return 0xDB;
                    case '\\': return 0xDC;
                    case ']': return 0xDD;
                    case '\'': return 0xDE;
                }
            }

            // Function Keys F1 ~ F24
            if (token.StartsWith("f") && int.TryParse(token.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
            {
                return (ushort)(0x70 + (fNum - 1));
            }

            // Numpad Keys
            if (token.StartsWith("num") || token.StartsWith("numpad"))
            {
                string suffix = token.Replace("numpad", "").Replace("num", "");
                if (int.TryParse(suffix, out int numVal) && numVal >= 0 && numVal <= 9) return (ushort)(0x60 + numVal);
                if (suffix == "add" || suffix == "plus" || suffix == "+") return 0x6B;
                if (suffix == "subtract" || suffix == "minus" || suffix == "-") return 0x6D;
                if (suffix == "multiply" || suffix == "star" || suffix == "*") return 0x6A;
                if (suffix == "divide" || suffix == "slash" || suffix == "/") return 0x6F;
                if (suffix == "decimal" || suffix == "dot" || suffix == ".") return 0x6E;
            }

            switch (token)
            {
                // Navigation & Editing
                case "left": return VK_LEFT;
                case "up": return VK_UP;
                case "right": return VK_RIGHT;
                case "down": return VK_DOWN;
                case "home": return 0x24;
                case "end": return 0x23;
                case "pageup":
                case "pgup":
                case "prior": return 0x21;
                case "pagedown":
                case "pgdn":
                case "next": return 0x22;
                case "insert":
                case "ins": return 0x2D;
                case "delete":
                case "del": return 0x2E;
                case "backspace":
                case "back": return 0x08;
                case "tab": return VK_TAB;
                case "enter":
                case "return": return VK_RETURN;
                case "escape":
                case "esc": return VK_ESCAPE;
                case "space":
                case "spacebar": return VK_SPACE;
                case "printscreen":
                case "prtscn":
                case "snapshot": return 0x2C;
                case "pause": return 0x13;
                case "capslock": return 0x14;
                case "scrolllock": return 0x91;
                case "numlock": return 0x90;

                // Symbols by name
                case "plus": return 0xBB;
                case "minus": return 0xBD;
                case "comma": return 0xBC;
                case "period":
                case "dot": return 0xBE;
                case "slash": return 0xBF;
                case "backslash": return 0xDC;
                case "semicolon": return 0xBA;
                case "quote": return 0xDE;
                case "bracketleft":
                case "openbracket": return 0xDB;
                case "bracketright":
                case "closebracket": return 0xDD;
                case "backquote":
                case "tilde": return 0xC0;

                // Media & Browser Keys
                case "volumeup": return VK_VOLUME_UP;
                case "volumedown": return VK_VOLUME_DOWN;
                case "volumemute":
                case "mute": return VK_VOLUME_MUTE;
                case "playpause":
                case "mediaplaypause": return 0xB3;
                case "nexttrack":
                case "medianext": return 0xB0;
                case "prevtrack":
                case "mediaprev": return 0xB1;
                case "stopmedia":
                case "mediastop": return 0xB2;
                case "browserback": return 0xA6;
                case "browserforward": return 0xA7;
                case "browserrefresh": return 0xA8;
                case "browserstop": return 0xA9;
                case "browsersearch": return 0xAA;
                case "browserfavorites": return 0xAB;
                case "browserhome": return 0xAC;

                default:
                    Debug.WriteLine($"Unrecognized key: {keyToken}");
                    return 0;
            }
        }
    }
}
