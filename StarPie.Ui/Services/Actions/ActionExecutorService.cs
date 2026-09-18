using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace StarPie.Ui.Services.Actions
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

        /// <summary>启动动作落地：权限沿用调用方进程的令牌（子进程继承 StarPie 自身的权限级别）。</summary>
        private void ExecuteLaunch(ActionItem action)
        {
            _startProcess(ActionRouting.BuildLaunchStartInfo(action.Parameter, action.Arguments));
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
            _ = PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());
        }

        private static INPUT CreateInput(KeyStroke stroke)
        {
            INPUT input = default;
            input.type = INPUT_TYPE.INPUT_KEYBOARD;
            input.ki = new KEYBDINPUT
            {
                wVk = (VIRTUAL_KEY)stroke.VirtualKey,
                wScan = 0,
                dwFlags = stroke.KeyDown ? 0 : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = 0
            };
            if (stroke.Extended)
            {
                input.ki.dwFlags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;
            }
            return input;
        }

        // --- Win32 键注入与锁屏互操作（声明来自 CsWin32 源生成，ADR-0051） ---

        private static void LockWorkStationViaInterop() => PInvoke.LockWorkStation();
    }
}
