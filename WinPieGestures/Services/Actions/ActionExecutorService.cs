using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace WinPieGestures.Services.Actions
{
    /// <summary>
    /// 动作执行服务实现 (T15, ADR-0002)：迁移前静态 ActionExecutor 的系统调用层——
    /// 进程启动、文件夹存在性探测、SendInput 键注入、LockWorkStation、错误弹窗——
    /// 全部收编为构造注入的接缝（生产用默认实现，测试注入假体后路由决策即可全量验证）。
    /// 分支行为与错误文案逐字保留迁移前语义。
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

        public ActionExecutorService(
            Action<ProcessStartInfo>? startProcess = null,
            Func<string, bool>? directoryExists = null,
            Func<string, bool>? fileExists = null,
            Action? lockWorkStation = null,
            Action<IReadOnlyList<KeyStroke>>? sendKeyStrokes = null,
            Action<string>? showActionError = null,
            Action<string>? showFolderError = null)
        {
            _startProcess = startProcess ?? (startInfo => Process.Start(startInfo));
            _directoryExists = directoryExists ?? Directory.Exists;
            _fileExists = fileExists ?? File.Exists;
            _lockWorkStation = lockWorkStation ?? LockWorkStationViaInterop;
            _sendKeyStrokes = sendKeyStrokes ?? SendKeyStrokes;
            _showActionError = showActionError ?? (message => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            _showFolderError = showFolderError ?? (message => MessageBox.Show(message, "StarPie", MessageBoxButton.OK, MessageBoxImage.Warning));
        }

        /// <summary>执行一个动作。类型路由大小写敏感（迁移前 switch 语义）；未知类型静默忽略。</summary>
        public void Execute(ActionItem action)
        {
            if (action == null) return;

            try
            {
                switch (ActionRouting.ResolveRoute(action.Type))
                {
                    case ActionRoute.Launch:
                        // 空路径按迁移前语义静默返回。
                        if (string.IsNullOrEmpty(action.Parameter)) return;
                        _startProcess(ActionRouting.BuildLaunchStartInfo(action.Parameter, action.Arguments));
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
                        // 迁移前语义：工具类启动失败降级发热键（如 taskmanager → Ctrl+Shift+Esc）；
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

        // --- Win32 键注入与锁屏（迁移前 ActionExecutor 的互操作面） ---

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
