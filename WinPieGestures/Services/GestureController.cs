using System;
using Application = System.Windows.Application;

namespace WinPieGestures
{
    /// <summary>
    /// App-side adapter around the pure <see cref="GestureEngine"/> (ADR-0002):
    /// feeds hook events in, then carries out the engine's decisions — replaying a
    /// suppressed click and executing the chosen action — on the UI thread.
    /// All gesture decisions live in the engine; this class performs side effects.
    /// </summary>
    public class GestureController
    {
        private readonly MouseHook _mouseHook;
        private readonly GestureEngine _engine;

        public GestureController(MouseHook mouseHook, GestureEngine engine)
        {
            _mouseHook = mouseHook;
            _engine = engine;
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
                Application.Current.Dispatcher.Invoke(() => ActionExecutor.Execute(action));
            }
        }
    }
}
