using System;
using Application = System.Windows.Application;

namespace StarPie.Services.Gestures
{
    /// <summary>
    /// 纯 <see cref="GestureEngine"/> 的应用侧适配器：把钩子事件喂入引擎，再在 UI 线程
    /// 执行引擎的决策——补发被抑制的点击与执行所选动作。手势决策全部在引擎内，
    /// 本类只做副作用。
    /// </summary>
    public class GestureController
    {
        private readonly MouseHook _mouseHook;
        private readonly GestureEngine _engine;
        private readonly IActionExecutorService _actionExecutor;

        public GestureController(MouseHook mouseHook, GestureEngine engine, IActionExecutorService actionExecutor)
        {
            _mouseHook = mouseHook;
            _engine = engine;
            _actionExecutor = actionExecutor;
            _mouseHook.OnRightButtonDown += Hook_OnRightButtonDown;
            _mouseHook.OnRightButtonUp += Hook_OnRightButtonUp;
            _mouseHook.OnMouseMove += Hook_OnMouseMove;
        }

        private void Hook_OnRightButtonDown(object? sender, MouseHookEventArgs e)
        {
            e.Handled = _engine.OnTriggerDown(e.Position);
        }

        private void Hook_OnMouseMove(object? sender, MouseHookEventArgs e)
        {
            _engine.OnTriggerMove(e.Position);
        }

        private void Hook_OnRightButtonUp(object? sender, MouseHookEventArgs e)
        {
            GestureReleaseResult result = _engine.OnTriggerUp(e.Position);
            e.Handled = result.Handled;
            if (!result.Handled)
            {
                return;
            }

            if (result.ShouldReplayClick)
            {
                // Replay off the hook callback so the click is not sent while the hook blocks.
                Application.Current.Dispatcher.BeginInvoke(new Action(_mouseHook.ReplayRightClick));
            }
            else if (result.ActionToExecute != null)
            {
                ActionItem action = result.ActionToExecute;
                Application.Current.Dispatcher.Invoke(() => _actionExecutor.Execute(action));
            }
        }
    }
}
