using System;
using System.Diagnostics;

namespace StarPie.Host.WheelGesture
{
    /// <summary>States of the wheel state machine: idle → press and wait for threshold → drag-select.</summary>
    public enum WheelGestureState
    {
        Idle,
        WaitingThreshold,
        Active,
    }

    /// <summary>What the app side should do with the trigger release, decided by the engine.</summary>
    public readonly struct WheelGestureReleaseResult
    {
        /// <summary>True when the release belongs to wheel handling and must not reach the app below.</summary>
        public bool Handled { get; }

        /// <summary>True when a click was suppressed pre-threshold and must be replayed verbatim.</summary>
        public bool ShouldReplayClick { get; }

        /// <summary>The action bound to the released sector, or null when the wheel ended as a cancel.</summary>
        public ActionItem? ActionToExecute { get; }

        private WheelGestureReleaseResult(bool handled, bool shouldReplayClick, ActionItem? actionToExecute)
        {
            Handled = handled;
            ShouldReplayClick = shouldReplayClick;
            ActionToExecute = actionToExecute;
        }

        public static WheelGestureReleaseResult PassThrough() => new(false, false, null);
        public static WheelGestureReleaseResult ReplayClick() => new(true, true, null);
        public static WheelGestureReleaseResult Execute(ActionItem action) => new(true, false, action);
        public static WheelGestureReleaseResult Cancel() => new(true, false, null);
    }

    /// <summary>
    /// Pure wheel decision state machine (no WPF/Win32 references): press waits for
    /// the drag threshold → activates the wheel view-model via <see cref="IWheelFactory"/> →
    /// drag-selects with center-deadzone cancel and outer-escape cancel → release
    /// executes the selected sector's action, replays the suppressed click, or cancels.
    /// Isolation (blacklist / modifiers / full-screen) and profile lookup are decided
    /// through <see cref="IWindowContext"/> and <see cref="IConfigService"/>.
    /// Config is consulted live on every decision so settings apply mid-wheel.
    /// </summary>
    public class WheelGestureEngine
    {
        // 运行期兜底：配置里的轮盘半径写成 0/负值时，外甩逃逸距离以此为基准换算。
        // 语义与外观默认值（WheelGeometryDefaults.Radius）不同——那是"新装观感"，这是"坏配置读数的
        // 安全基准"；两者取值当前相同纯属巧合，故各自独立、不互相引用。
        private const double FallbackWheelRadius = 138.0;
        // Feel constants carried over from the pre-refactor controller: center
        // deadzone radius as a fraction of the drag threshold, and outer-escape
        // distance as a multiple of the wheel radius.
        private const double CenterDeadzoneFractionOfThreshold = 0.6;
        private const double OuterEscapeFractionOfRadius = 1.50;

        private readonly IConfigService _config;
        private readonly IWindowContext _windowContext;
        private readonly IWheelFactory _wheelFactory;

        private ScreenPoint _startPoint;
        private WheelProfile? _activeProfile;
        private int _selectedSectorIndex = -1;
        private IWheelViewModel? _wheel;

        public WheelGestureState State { get; private set; } = WheelGestureState.Idle;

        public WheelGestureEngine(IConfigService config, IWindowContext windowContext, IWheelFactory wheelFactory)
        {
            _config = config;
            _windowContext = windowContext;
            _wheelFactory = wheelFactory;
        }

        /// <summary>Trigger button pressed. Returns true to block the event (a suppressed
        /// click is replayed on release when no wheel started); false passes it through.</summary>
        public bool OnTriggerDown(ScreenPoint position)
        {
            if (IsWheelGestureIsolated())
            {
                State = WheelGestureState.Idle;
                return false;
            }

            _startPoint = position;
            _activeProfile = null;
            _selectedSectorIndex = -1;
            State = WheelGestureState.WaitingThreshold;
            Debug.WriteLine($"RightMouseDown at {position.X}, {position.Y}. Waiting for threshold.");
            return true;
        }

        /// <summary>Pointer moved. Never blocks the event; activates the wheel once the
        /// drag threshold is crossed, then updates the selected sector per move.</summary>
        public void OnTriggerMove(ScreenPoint position)
        {
            if (State == WheelGestureState.WaitingThreshold)
            {
                if (Distance(position, _startPoint) < _config.Current.DragThreshold)
                {
                    return;
                }

                State = WheelGestureState.Active;

                string processName = _windowContext.GetForegroundProcessName();
                _activeProfile = _config.GetProfileForProcess(processName);
                Debug.WriteLine($"WheelGesture activated. Process: {processName}, Profile: {_activeProfile.ProcessName}, Sectors: {_activeProfile.SectorCount}");

                _wheel?.Close();
                _wheel = _wheelFactory.Create(_startPoint, _activeProfile);
                _wheel.Show();
                UpdateSelection(position);
            }
            else if (State == WheelGestureState.Active)
            {
                UpdateSelection(position);
            }
        }

        /// <summary>Trigger button released. Returns the decision for the app side:
        /// replay the suppressed click, execute the selected action, or cancel.</summary>
        public WheelGestureReleaseResult OnTriggerUp(ScreenPoint position)
        {
            if (State == WheelGestureState.WaitingThreshold)
            {
                State = WheelGestureState.Idle;
                Debug.WriteLine("Normal click detected. Replaying right click.");
                return WheelGestureReleaseResult.ReplayClick();
            }

            if (State == WheelGestureState.Active)
            {
                Debug.WriteLine($"WheelGesture completed. Selected sector: {_selectedSectorIndex}");
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
                State = WheelGestureState.Idle;
                return action != null ? WheelGestureReleaseResult.Execute(action) : WheelGestureReleaseResult.Cancel();
            }

            return WheelGestureReleaseResult.PassThrough();
        }

        /// <summary>False when the current foreground process, held modifiers, or a
        /// full-screen foreground window isolate the wheel (right click passes through).</summary>
        private bool IsWheelGestureIsolated()
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

            HeldModifierKeys modifiers = _windowContext.GetActiveModifierKeys();
            bool isModifierPressed =
                (_config.Current.DisableOnCtrl && (modifiers & HeldModifierKeys.Control) != 0) ||
                (_config.Current.DisableOnShift && (modifiers & HeldModifierKeys.Shift) != 0) ||
                (_config.Current.DisableOnAlt && (modifiers & HeldModifierKeys.Alt) != 0);

            bool isFullScreen = _config.Current.DisableOnFullScreen && _windowContext.IsForegroundFullScreen();

            if (isBlacklisted || isModifierPressed || isFullScreen)
            {
                Debug.WriteLine($"WheelGesture trigger isolated. Process: {processName}, Blacklisted: {isBlacklisted}, Modifier: {isModifierPressed}, FullScreen: {isFullScreen}. Passing right click through.");
                return true;
            }

            return false;
        }

        private void UpdateSelection(ScreenPoint currentPoint)
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

        private static double Distance(ScreenPoint a, ScreenPoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
