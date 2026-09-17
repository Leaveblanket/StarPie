using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace StarPie.Services.Gestures
{
    /// <summary>
    /// 手势触发的 Win32 适配器：低级鼠标钩子把右键按下 / 移动 / 抬起喂入
    /// <see cref="GestureEngine"/>，按引擎决策拦截事件，并执行引擎交付的副作用——
    /// 补发被抑制的点击与执行所选动作（两者都调度到 UI 线程落地，不在钩子回调栈内执行）。
    /// 手势决策全部在引擎内，本类只做事件拦截与副作用。
    /// Win32 声明来自 CsWin32 源生成（清单为项目根 NativeMethods.txt，ADR-0051），
    /// 本文件不再持有手写 P/Invoke；消息号与枚举同样取生成面（PInvoke.WM_*）。
    /// 不加 [SupportedOSPlatform]：本集 TFM 已是 windows10.0.19041，
    /// 版本号更低的注解会把调用点声明降到生成 API 的 windows5.0 之下而触发 CA1416。
    /// </summary>
    public sealed class MouseHook : IDisposable
    {

        // 钩子回调线程读、UI 线程写（托盘暂停/恢复），须 volatile 保可见性。
        private volatile bool _isPaused;

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        // HOOKPROC 实例须保活到 Unhook 之后：封送为函数指针后若被 GC 回收，
        // 系统回调会落到已失效的 thunk 上。
        private readonly HOOKPROC _proc;
        private HHOOK _hookId;

        // 手势引擎（纯决策）与动作执行器（副作用落地）：钩子回调只把事件喂给引擎、
        // 把执行器调用调度到 UI 线程，二者都由组合根构造注入。
        private readonly GestureEngine _engine;
        private readonly IActionExecutorService _actionExecutor;

        // 调度接缝：健康检查重注册与松手副作用（补发点击 / 执行动作）都须回 UI 线程执行。
        // 接缝由组合根注入，本适配器因此不引用任何 UI 框架类型（与类型级声明一致）。
        private readonly Action<Action> _postToUiThread;

        // Flags to prevent recursive hook interception when we replay right click events
        private bool _ignoreNextRButtonDown = false;
        private bool _ignoreNextRButtonUp = false;

        // Hook stability and health check variables
        private Timer? _healthCheckTimer;
        private System.Drawing.Point _lastSystemCursorPos;
        private int _hookEventsCountSinceLastCheck = 0;

        public MouseHook(Action<Action> postToUiThread, GestureEngine engine, IActionExecutorService actionExecutor)
        {
            ArgumentNullException.ThrowIfNull(postToUiThread);
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(actionExecutor);
            _postToUiThread = postToUiThread;
            _engine = engine;
            _actionExecutor = actionExecutor;
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId.IsNull)
            {
                _hookId = SetHook(_proc);
                if (_hookId.IsNull)
                {
                    // 保留原失败面（启动期抛出、由壳层兜底记日志），补上 Win32 错误码。
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to set low-level mouse hook.");
                }

                // Initialize health check
                _hookEventsCountSinceLastCheck = 0;
                PInvoke.GetCursorPos(out _lastSystemCursorPos);
                _healthCheckTimer = new Timer(CheckHookHealth, null, 3000, 3000);
            }
        }

        public void Stop()
        {
            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;

            if (!_hookId.IsNull)
            {
                PInvoke.UnhookWindowsHookEx(_hookId);
                _hookId = HHOOK.Null;
            }
        }

        /// <summary>与 <see cref="Stop"/> 同义：卸钩子、停健康检查。</summary>
        public void Dispose() => Stop();

        private void CheckHookHealth(object? state)
        {
            if (_hookId.IsNull) return;

            if (PInvoke.GetCursorPos(out System.Drawing.Point currentPos))
            {
                bool mouseMoved = currentPos.X != _lastSystemCursorPos.X || currentPos.Y != _lastSystemCursorPos.Y;
                _lastSystemCursorPos = currentPos;

                if (mouseMoved)
                {
                    // If system mouse moved, but we received 0 hook events, hook is likely dead!
                    if (Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0) == 0)
                    {
                        _postToUiThread(() =>
                        {
                            Debug.WriteLine("Mouse hook health check failed. Re-registering hook...");
                            try
                            {
                                Stop();
                                Start();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to re-register hook: {ex.Message}");
                            }
                        });
                    }
                }
                else
                {
                    // Reset count if mouse did not move to avoid false positive
                    Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0);
                }
            }
        }

        // 低级钩子不做 DLL 注入，hMod 仅为占位：取主程序模块句柄（.NET 内置 API），
        // 不再走 Process/MainModule/GetModuleHandle 旧模板。
        private static HHOOK SetHook(HOOKPROC proc)
            => PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_MOUSE_LL,
                proc,
                new HINSTANCE(NativeLibrary.GetMainProgramHandle()),
                dwThreadId: 0);

        // CsWin32 的 HOOKPROC 形状：LRESULT (int, WPARAM, LPARAM)。
        private unsafe LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
        {
            Interlocked.Increment(ref _hookEventsCountSinceLastCheck);

            if (_isPaused)
            {
                return PInvoke.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (nCode >= 0)
            {
                uint message = (uint)wParam.Value;
                // lParam 在回调期间指向系统持有的 MSLLHOOKSTRUCT：直接解引用，
                // 免去原 Marshal.PtrToStructure 的逐事件封送。
                MSLLHOOKSTRUCT hookStruct = *(MSLLHOOKSTRUCT*)lParam.Value;
                System.Drawing.Point point = hookStruct.pt;

                if (message == PInvoke.WM_RBUTTONDOWN)
                {
                    if (_ignoreNextRButtonDown)
                    {
                        _ignoreNextRButtonDown = false;
                        return PInvoke.CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    // 引擎决定按下是否被手势接管（接管即拦截，未成手势时松手补发点击）。
                    if (_engine.OnTriggerDown(new GesturePoint(point.X, point.Y)))
                    {
                        return new LRESULT(1); // Block the event from propagating
                    }
                }
                else if (message == PInvoke.WM_RBUTTONUP)
                {
                    if (_ignoreNextRButtonUp)
                    {
                        _ignoreNextRButtonUp = false;
                        return PInvoke.CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    GestureReleaseResult result = _engine.OnTriggerUp(new GesturePoint(point.X, point.Y));
                    if (!result.Handled)
                    {
                        return PInvoke.CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    if (result.ShouldReplayClick)
                    {
                        // Replay off the hook callback so the click is not sent while the hook blocks.
                        _postToUiThread(ReplayRightClick);
                    }
                    else if (result.ActionToExecute != null)
                    {
                        ActionItem action = result.ActionToExecute;
                        _postToUiThread(() => _actionExecutor.Execute(action));
                    }

                    return new LRESULT(1); // Block the event from propagating
                }
                else if (message == PInvoke.WM_MOUSEMOVE)
                {
                    _engine.OnTriggerMove(new GesturePoint(point.X, point.Y));
                }
            }

            return PInvoke.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Replays a right mouse click at the current position.
        /// Temporarily ignores our own hook to avoid infinite loop.
        /// </summary>
        private void ReplayRightClick()
        {
            _ignoreNextRButtonDown = true;
            _ignoreNextRButtonUp = true;
            // 保留 mouse_event（已随 Windows 建议弃用，但与现状行为一致）；
            // 换 SendInput 属行为面等价替换，单独提交（ADR-0051）。
            PInvoke.mouse_event(MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            PInvoke.mouse_event(MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        }
    }
}
