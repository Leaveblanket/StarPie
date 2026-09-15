using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace StarPie.Services.Actions
{
    /// <summary>
    /// 动作执行服务实现：进程启动、文件夹存在性探测、SendInput 键注入、
    /// LockWorkStation、错误弹窗等系统调用层，全部经构造注入的接缝执行
    /// （生产用默认实现；测试注入假体后路由决策即可全量验证）。
    /// </summary>
    public sealed class ActionExecutorService : IActionExecutorService
    {
        private readonly Action<ProcessStartInfo> _startProcess;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, bool> _fileExists;
        private readonly Action _lockWorkStation;
        private readonly Action<IReadOnlyList<KeyStroke>> _sendKeyStrokes;
        private readonly Action<string> _showActionError;
        private readonly Action<string> _showFolderError;
        private readonly Func<bool> _isElevated;
        private readonly Func<string, string, string, bool> _shellExecute;

        public ActionExecutorService(
            Action<ProcessStartInfo>? startProcess = null,
            Func<string, bool>? directoryExists = null,
            Func<string, bool>? fileExists = null,
            Action? lockWorkStation = null,
            Action<IReadOnlyList<KeyStroke>>? sendKeyStrokes = null,
            Action<string>? showActionError = null,
            Action<string>? showFolderError = null,
            Func<bool>? isElevated = null,
            Func<string, string, string, bool>? shellExecute = null)
        {
            _startProcess = startProcess ?? (startInfo => Process.Start(startInfo));
            _directoryExists = directoryExists ?? Directory.Exists;
            _fileExists = fileExists ?? File.Exists;
            _lockWorkStation = lockWorkStation ?? LockWorkStationViaInterop;
            _sendKeyStrokes = sendKeyStrokes ?? SendKeyStrokes;
            _showActionError = showActionError ?? (message => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            _showFolderError = showFolderError ?? (message => MessageBox.Show(message, "StarPie", MessageBoxButton.OK, MessageBoxImage.Warning));
            _isElevated = isElevated ?? ProcessElevation.IsRunningAsAdministrator;
            _shellExecute = shellExecute ?? ExplorerShellLaunch.TryShellExecute;
        }

        /// <summary>执行一个动作。类型路由大小写敏感；未知类型静默忽略。</summary>
        public void Execute(ActionItem action)
        {
            if (action == null) return;

            try
            {
                switch (ActionRouting.ResolveRoute(action.Type))
                {
                    case ActionRoute.Launch:
                        // 空路径静默返回。
                        if (string.IsNullOrEmpty(action.Parameter)) return;
                        ExecuteLaunch(action);
                        break;
                    case ActionRoute.Folder:
                        ExecuteFolder(action.Parameter);
                        break;
                    case ActionRoute.Hotkey:
                        SendHotkey(action.Parameter);
                        break;
                    case ActionRoute.System:
                        ExecuteSystem(action.Parameter);
                        break;
                    case ActionRoute.Unknown:
                    default:
                        Debug.WriteLine($"Unknown action type: {action.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _showActionError($"Failed to execute action '{action.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// 启动动作落地：形态由 <see cref="ActionRouting.ResolveLaunchMode"/> 决策。
        /// 提权态默认经 Explorer 中介降权（子进程不继承管理员令牌）；中介不可用时回退直接启动
        /// ——降权是尽力而为，不能变成"启动不了"。工作目录与直接启动同口径（子进程继承调用方目录）。
        /// </summary>
        private void ExecuteLaunch(ActionItem action)
        {
            switch (ActionRouting.ResolveLaunchMode(_isElevated(), action.RunAsAdmin))
            {
                case LaunchMode.ShellMediated:
                    if (_shellExecute(action.Parameter, action.Arguments ?? string.Empty, Environment.CurrentDirectory))
                    {
                        return;
                    }

                    _startProcess(ActionRouting.BuildLaunchStartInfo(action.Parameter, action.Arguments));
                    break;
                case LaunchMode.Elevated:
                    _startProcess(ActionRouting.BuildLaunchStartInfo(action.Parameter, action.Arguments, runAsAdmin: true));
                    break;
                case LaunchMode.Direct:
                default:
                    _startProcess(ActionRouting.BuildLaunchStartInfo(action.Parameter, action.Arguments));
                    break;
            }
        }

        private void ExecuteFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            try
            {
                string expandedPath = ActionRouting.ExpandFolderPath(folderPath);
                bool isDirectory = _directoryExists(expandedPath);
                bool isFile = !isDirectory && _fileExists(expandedPath);
                _startProcess(ActionRouting.BuildFolderStartInfo(folderPath, isDirectory, isFile));
            }
            catch (Exception ex)
            {
                _showFolderError($"无法打开文件夹 '{folderPath}':\n{ex.Message}");
            }
        }

        private void SendHotkey(string? hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString)) return;

            var strokes = ActionRouting.BuildKeySequence(hotkeyString);
            if (strokes.Count == 0) return;
            _sendKeyStrokes(strokes);
        }

        private void ExecuteSystem(string? presetName)
        {
            switch (ActionRouting.ResolveSystemCommand(presetName))
            {
                case ActionRouting.SystemCommand.SendHotkey sendHotkey:
                    SendHotkey(sendHotkey.Hotkey);
                    break;
                case ActionRouting.SystemCommand.SendKey sendKey:
                    _sendKeyStrokes(ActionRouting.BuildSingleKeyStrokes(sendKey.VirtualKey));
                    break;
                case ActionRouting.SystemCommand.LockWorkstation:
                    _lockWorkStation();
                    break;
                case ActionRouting.SystemCommand.StartProcess startProcess:
                    try
                    {
                        _startProcess(new ProcessStartInfo
                        {
                            FileName = startProcess.FileName,
                            Arguments = startProcess.Arguments,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // 工具类启动失败降级发热键（如 taskmanager → Ctrl+Shift+Esc）；
                        // 电源类静默失败，无降级、无提示。
                        if (startProcess.FallbackHotkey != null)
                        {
                            SendHotkey(startProcess.FallbackHotkey);
                        }
                    }
                    break;
                case ActionRouting.SystemCommand.Noop:
                default:
                    break;
            }
        }

        private static void SendKeyStrokes(IReadOnlyList<KeyStroke> strokes)
        {
            var inputs = new INPUT[strokes.Count];
            for (int i = 0; i < strokes.Count; i++)
            {
                inputs[i] = CreateInput(strokes[i]);
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static INPUT CreateInput(KeyStroke stroke)
        {
            var input = new INPUT { type = INPUT_KEYBOARD };
            input.U.ki = new KEYBDINPUT
            {
                wVk = stroke.VirtualKey,
                wScan = 0,
                dwFlags = (uint)(stroke.KeyDown ? 0 : KEYEVENTF_KEYUP),
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            if (stroke.Extended)
            {
                input.U.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
            }
            return input;
        }

        // --- Win32 键注入与锁屏互操作 ---

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        private static void LockWorkStationViaInterop() => LockWorkStation();

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
    }
}
