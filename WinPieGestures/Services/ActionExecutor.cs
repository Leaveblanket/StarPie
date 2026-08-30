using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace WinPieGestures
{
    public static class ActionExecutor
    {
        // P/Invoke for executing applications/commands
        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        // P/Invoke for key simulation
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // Virtual Key Codes
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

        public static void Execute(ActionItem action)
        {
            if (action == null) return;

            try
            {
                switch (action.Type.Trim())
                {
                    case "Launch":
                        ExecuteLaunch(action.Parameter, action.Arguments);
                        break;
                    case "Folder":
                    case "OpenFolder":
                        ExecuteFolder(action.Parameter);
                        break;
                    case "Hotkey":
                        ExecuteHotkey(action.Parameter);
                        break;
                    case "System":
                        ExecuteSystem(action.Parameter);
                        break;
                    default:
                        Debug.WriteLine($"Unknown action type: {action.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to execute action '{action.Name}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ExecuteFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            try
            {
                string expandedPath = Environment.ExpandEnvironmentVariables(folderPath.Trim().Trim('"'));
                if (System.IO.Directory.Exists(expandedPath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{expandedPath}\"",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                else if (System.IO.File.Exists(expandedPath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{expandedPath}\"",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                else
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = expandedPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"无法打开文件夹 '{folderPath}':\n{ex.Message}", "StarPie", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void ExecuteLaunch(string path, string arguments)
        {
            if (string.IsNullOrEmpty(path)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private static void ExecuteHotkey(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString)) return;

            var keys = ParseHotkey(hotkeyString);
            if (keys.Modifiers.Count == 0 && keys.MainKey == 0) return;

            // Generate inputs: modifiers down, key down, key up, modifiers up
            var inputs = new List<INPUT>();

            // 1. Modifiers down
            foreach (var vk in keys.Modifiers)
            {
                inputs.Add(CreateKeyInput(vk, down: true));
            }

            // 2. Main key down
            if (keys.MainKey != 0)
            {
                inputs.Add(CreateKeyInput(keys.MainKey, down: true));
            }

            // 3. Main key up
            if (keys.MainKey != 0)
            {
                inputs.Add(CreateKeyInput(keys.MainKey, down: false));
            }

            // 4. Modifiers up (in reverse order)
            for (int i = keys.Modifiers.Count - 1; i >= 0; i--)
            {
                inputs.Add(CreateKeyInput(keys.Modifiers[i], down: false));
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        private static void ExecuteSystem(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLower())
            {
                // Window Management
                case "closewindow":
                    ExecuteHotkey("Alt+F4");
                    break;
                case "minimize":
                    ExecuteHotkey("Win+Down");
                    break;
                case "maximize":
                    ExecuteHotkey("Win+Up");
                    break;
                case "snapleft":
                    ExecuteHotkey("Win+Left");
                    break;
                case "snapright":
                    ExecuteHotkey("Win+Right");
                    break;
                case "taskview":
                    ExecuteHotkey("Win+Tab");
                    break;
                case "prevdesktop":
                    ExecuteHotkey("Win+Ctrl+Left");
                    break;
                case "nextdesktop":
                    ExecuteHotkey("Win+Ctrl+Right");
                    break;
                case "showdesktop":
                    ExecuteHotkey("Win+D");
                    break;
                case "fullscreen":
                    ExecuteHotkey("F11");
                    break;
                case "screenshot":
                    ExecuteHotkey("Win+Shift+S");
                    break;

                // System & Utilities
                case "taskmanager":
                    try { Process.Start(new ProcessStartInfo { FileName = "taskmgr.exe", UseShellExecute = true }); }
                    catch { ExecuteHotkey("Ctrl+Shift+Esc"); }
                    break;
                case "explorer":
                    try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true }); }
                    catch { ExecuteHotkey("Win+E"); }
                    break;
                case "settings":
                    try { Process.Start(new ProcessStartInfo { FileName = "ms-settings:", UseShellExecute = true }); }
                    catch { ExecuteHotkey("Win+I"); }
                    break;
                case "calculator":
                    try { Process.Start(new ProcessStartInfo { FileName = "calc.exe", UseShellExecute = true }); }
                    catch { ExecuteHotkey("Win+R"); }
                    break;
                case "rundialog":
                    ExecuteHotkey("Win+R");
                    break;
                case "windowssearch":
                    ExecuteHotkey("Win+S");
                    break;
                case "clipboardhistory":
                    ExecuteHotkey("Win+V");
                    break;
                case "lock":
                    LockWorkStation();
                    break;

                // Media & Volume
                case "volumeup":
                    SimulateSingleKey(VK_VOLUME_UP);
                    break;
                case "volumedown":
                    SimulateSingleKey(VK_VOLUME_DOWN);
                    break;
                case "volumemute":
                    SimulateSingleKey(VK_VOLUME_MUTE);
                    break;
                case "playpause":
                    SimulateSingleKey(0xB3); // VK_MEDIA_PLAY_PAUSE
                    break;
                case "nexttrack":
                    SimulateSingleKey(0xB0); // VK_MEDIA_NEXT_TRACK
                    break;
                case "prevtrack":
                    SimulateSingleKey(0xB1); // VK_MEDIA_PREV_TRACK
                    break;
                case "stopmedia":
                    SimulateSingleKey(0xB2); // VK_MEDIA_STOP
                    break;

                // Browser Navigation
                case "newtab":
                    ExecuteHotkey("Ctrl+T");
                    break;
                case "closetab":
                    ExecuteHotkey("Ctrl+W");
                    break;
                case "reopentab":
                    ExecuteHotkey("Ctrl+Shift+T");
                    break;
                case "refresh":
                    ExecuteHotkey("F5");
                    break;
                case "hardrefresh":
                    ExecuteHotkey("Ctrl+F5");
                    break;
                case "zoomin":
                    ExecuteHotkey("Ctrl+Plus");
                    break;
                case "zoomout":
                    ExecuteHotkey("Ctrl+Minus");
                    break;
                case "zoomreset":
                    ExecuteHotkey("Ctrl+0");
                    break;

                // Power Management
                case "sleep":
                    try { Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "powrprof.dll,SetSuspendState 0,1,0", UseShellExecute = true }); }
                    catch { }
                    break;
                case "restart":
                    try { Process.Start(new ProcessStartInfo { FileName = "shutdown.exe", Arguments = "/r /t 0", UseShellExecute = true }); }
                    catch { }
                    break;
                case "shutdown":
                    try { Process.Start(new ProcessStartInfo { FileName = "shutdown.exe", Arguments = "/s /t 0", UseShellExecute = true }); }
                    catch { }
                    break;

                default:
                    Debug.WriteLine($"Unknown system preset: {presetName}");
                    break;
            }
        }

        private static void SimulateSingleKey(ushort vk)
        {
            var inputs = new INPUT[]
            {
                CreateKeyInput(vk, down: true),
                CreateKeyInput(vk, down: false)
            };
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static INPUT CreateKeyInput(ushort vk, bool down)
        {
            var input = new INPUT { type = INPUT_KEYBOARD };
            input.U.ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = (uint)(down ? 0 : KEYEVENTF_KEYUP),
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };

            // Set extended key flag for media keys, arrow keys, and navigation keys
            if (vk >= 0x21 && vk <= 0x2F || vk >= 0x5B && vk <= 0x5C || vk >= 0xAD && vk <= 0xB3 || vk >= 0xA6 && vk <= 0xAC)
            {
                input.U.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
            }

            return input;
        }

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
