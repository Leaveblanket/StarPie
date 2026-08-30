using System;
using System.Diagnostics;

namespace WinPieGestures
{
    /// <summary>UI-framework-free screen point flowing through the gesture pipeline.</summary>
    public readonly struct GesturePoint
    {
        public double X { get; }
        public double Y { get; }

        public GesturePoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>States of the gesture state machine: idle → press and wait for threshold → drag-select.</summary>
    public enum GestureState
    {
        Idle,
        WaitingThreshold,
        Active,
    }

    /// <summary>What the app side should do with the trigger release, decided by the engine.</summary>
    public readonly struct GestureReleaseResult
    {
        /// <summary>True when the release belongs to gesture handling and must not reach the app below.</summary>
        public bool Handled { get; }

        /// <summary>True when a click was suppressed pre-threshold and must be replayed verbatim.</summary>
        public bool ShouldReplayClick { get; }

        /// <summary>The action bound to the released sector, or null when the gesture ended as a cancel.</summary>
        public ActionItem? ActionToExecute { get; }

        private GestureReleaseResult(bool handled, bool shouldReplayClick, ActionItem? actionToExecute)
        {
            Handled = handled;
            ShouldReplayClick = shouldReplayClick;
            ActionToExecute = actionToExecute;
        }

        public static GestureReleaseResult PassThrough() => new(false, false, null);
        public static GestureReleaseResult ReplayClick() => new(true, true, null);
        public static GestureReleaseResult Execute(ActionItem action) => new(true, false, action);
        public static GestureReleaseResult Cancel() => new(true, false, null);
    }

    /// <summary>
    /// Pure gesture decision state machine (no WPF/Win32 references): press waits for
    /// the drag threshold → activates the wheel view-model via <see cref="IWheelFactory"/> →
    /// drag-selects with center-deadzone cancel and outer-escape cancel → release
    /// executes the selected sector's action, replays the suppressed click, or cancels.
    /// Isolation (blacklist / modifiers / full-screen) and profile lookup are decided
    /// through <see cref="IWindowContext"/> and <see cref="IConfigService"/>.
    /// Config is consulted live on every decision so settings apply mid-gesture.
    /// </summary>
    public class GestureEngine
    {
        private const double FallbackWheelRadius = 138.0;
        // Feel constants carried over from the pre-refactor controller: center
        // deadzone radius as a fraction of the drag threshold, and outer-escape
        // distance as a multiple of the wheel radius.
        private const double CenterDeadzoneFractionOfThreshold = 0.6;
        private const double OuterEscapeFractionOfRadius = 1.50;

        private readonly IConfigService _config;
        private readonly IWindowContext _windowContext;
        private readonly IWheelFactory _wheelFactory;

        private GesturePoint _startPoint;
        private WheelProfile? _activeProfile;
        private int _selectedSectorIndex = -1;
        private IWheelViewModel? _wheel;

        public GestureState State { get; private set; } = GestureState.Idle;

        public GestureEngine(IConfigService config, IWindowContext windowContext, IWheelFactory wheelFactory)
        {
            _config = config;
            _windowContext = windowContext;
            _wheelFactory = wheelFactory;
        }

        /// <summary>Trigger button pressed. Returns true to block the event (a suppressed
        /// click is replayed on release when no gesture started); false passes it through.</summary>
        public bool OnTriggerDown(GesturePoint position)
        {
            if (IsGestureIsolated())
            {
                State = GestureState.Idle;
                return false;
            }

            _startPoint = position;
            _activeProfile = null;
            _selectedSectorIndex = -1;
            State = GestureState.WaitingThreshold;
            Debug.WriteLine($"RightMouseDown at {position.X}, {position.Y}. Waiting for threshold.");
            return true;
        }

        /// <summary>Pointer moved. Never blocks the event; activates the wheel once the
        /// drag threshold is crossed, then updates the selected sector per move.</summary>
        public void OnTriggerMove(GesturePoint position)
        {
            if (State == GestureState.WaitingThreshold)
            {
                if (Distance(position, _startPoint) < _config.Current.DragThreshold)
                {
                    return;
                }

                State = GestureState.Active;

                string processName = _windowContext.GetForegroundProcessName();
                _activeProfile = _config.GetProfileForProcess(processName);
                Debug.WriteLine($"Gesture activated. Process: {processName}, Profile: {_activeProfile.ProcessName}, Sectors: {_activeProfile.SectorCount}");

                _wheel?.Close();
                _wheel = _wheelFactory.Create(_startPoint, _activeProfile);
                _wheel.Show();
                UpdateSelection(position);
            }
            else if (State == GestureState.Active)
            {
                UpdateSelection(position);
            }
        }

        /// <summary>Trigger button released. Returns the decision for the app side:
        /// replay the suppressed click, execute the selected action, or cancel.</summary>
        public GestureReleaseResult OnTriggerUp(GesturePoint position)
        {
            if (State == GestureState.WaitingThreshold)
            {
                State = GestureState.Idle;
                Debug.WriteLine("Normal click detected. Replaying right click.");
                return GestureReleaseResult.ReplayClick();
            }

            if (State == GestureState.Active)
            {
                Debug.WriteLine($"Gesture completed. Selected sector: {_selectedSectorIndex}");
                ActionItem? action = null;
                if (_activeProfile != null && _selectedSectorIndex >= 0 && _selectedSectorIndex < _activeProfile.Actions.Count)
                {
                    var candidate = _activeProfile.Actions[_selectedSectorIndex];
                    if (candidate != null && !string.IsNullOrEmpty(candidate.Type))
                    {
                        action = candidate;
                    }
                }

                CloseWheel();
                State = GestureState.Idle;
                return action != null ? GestureReleaseResult.Execute(action) : GestureReleaseResult.Cancel();
            }

            return GestureReleaseResult.PassThrough();
        }

        /// <summary>False when the current foreground process, held modifiers, or a
        /// full-screen foreground window isolate the gesture (right click passes through).</summary>
        private bool IsGestureIsolated()
        {
            string processName = _windowContext.GetForegroundProcessName();

            bool isBlacklisted = false;
            var blacklistedProcesses = _config.Current.BlacklistedProcesses;
            if (blacklistedProcesses != null)
            {
                string normProc = processName.Trim().ToLower();
                foreach (var blacklisted in blacklistedProcesses)
                {
                    if (blacklisted.Trim().ToLower() == normProc)
                    {
                        isBlacklisted = true;
                        break;
                    }
                }
            }

            GestureModifierKeys modifiers = _windowContext.GetActiveModifierKeys();
            bool isModifierPressed =
                (_config.Current.DisableOnCtrl && (modifiers & GestureModifierKeys.Control) != 0) ||
                (_config.Current.DisableOnShift && (modifiers & GestureModifierKeys.Shift) != 0) ||
                (_config.Current.DisableOnAlt && (modifiers & GestureModifierKeys.Alt) != 0);

            bool isFullScreen = _config.Current.DisableOnFullScreen && _windowContext.IsForegroundFullScreen();

            if (isBlacklisted || isModifierPressed || isFullScreen)
            {
                Debug.WriteLine($"Gesture trigger isolated. Process: {processName}, Blacklisted: {isBlacklisted}, Modifier: {isModifierPressed}, FullScreen: {isFullScreen}. Passing right click through.");
                return true;
            }

            return false;
        }

        private void UpdateSelection(GesturePoint currentPoint)
        {
            if (_wheel == null || _activeProfile == null) return;

            double dx = currentPoint.X - _startPoint.X;
            double dy = currentPoint.Y - _startPoint.Y;
            double distance = Distance(currentPoint, _startPoint);

            // 1. Center deadzone cancel (拖回中心核圆取消)
            if (distance < _config.Current.DragThreshold * CenterDeadzoneFractionOfThreshold)
            {
                _selectedSectorIndex = -1;
                _wheel.HighlightSector(-1);
                _wheel.SetOuterEscapeState(false);
                return;
            }

            // 2. Outer escape cancel (顺势外甩脱离取消)
            bool enableOuterEscape = _config.Current.EnableOuterEscapeCancel;
            double outerRadius = _config.Current.WheelRadius > 0 ? _config.Current.WheelRadius : FallbackWheelRadius;
            double escapeThreshold = _config.Current.OuterEscapeDistance > 0
                ? _config.Current.OuterEscapeDistance
                : outerRadius * OuterEscapeFractionOfRadius;

            if (enableOuterEscape && distance > escapeThreshold)
            {
                _selectedSectorIndex = -1;
                _wheel.HighlightSector(-1);
                _wheel.SetOuterEscapeState(true);
                return;
            }

            _wheel.SetOuterEscapeState(false);

            // Angle in degrees from [0, 360); Math.Round aligns 0 degrees (right) as
            // the center of sector 0 — keep the same rounding for identical feel.
            double radians = Math.Atan2(dy, dx);
            double degrees = radians * (180.0 / Math.PI);
            if (degrees < 0)
            {
                degrees += 360.0;
            }

            int n = _activeProfile.SectorCount;
            double sectorSize = 360.0 / n;

            int index = (int)Math.Round(degrees / sectorSize) % n;
            _selectedSectorIndex = index;
            _wheel.HighlightSector(index);
        }

        private void CloseWheel()
        {
            if (_wheel != null)
            {
                _wheel.Close();
                _wheel = null;
            }
        }

        private static double Distance(GesturePoint a, GesturePoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
