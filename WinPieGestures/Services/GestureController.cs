using System;
using System.Diagnostics;
using System.Windows;
using Point = System.Windows.Point;
using Application = System.Windows.Application;

namespace WinPieGestures
{
    public class GestureController
    {
        private readonly MouseHook _mouseHook;
        private readonly IConfigService _config;
        private RadialWindow _radialWindow;

        private Point _startPoint;
        private bool _isWaitingForThreshold = false;
        private bool _isGestureActive = false;
        private WheelProfile _activeProfile;
        private int _selectedSectorIndex = -1;

        public GestureController(MouseHook mouseHook, IConfigService config)
        {
            _mouseHook = mouseHook;
            _config = config;
            _mouseHook.OnRightButtonDown += Hook_OnRightButtonDown;
            _mouseHook.OnRightButtonUp += Hook_OnRightButtonUp;
            _mouseHook.OnMouseMove += Hook_OnMouseMove;
        }

        private void Hook_OnRightButtonDown(object sender, MouseEventArgs e)
        {
            // Check if gesture should be isolated (blacklisted, modifiers pressed, or active window full-screen)
            string processName = ActiveWindowHelper.GetActiveWindowProcessName();
            
            bool isBlacklisted = false;
            if (_config.Current.BlacklistedProcesses != null)
            {
                string normProc = processName.Trim().ToLower();
                foreach (var blacklisted in _config.Current.BlacklistedProcesses)
                {
                    if (blacklisted.Trim().ToLower() == normProc)
                    {
                        isBlacklisted = true;
                        break;
                    }
                }
            }

            bool isCtrlPressed = _config.Current.DisableOnCtrl && 
                                 (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
            bool isShiftPressed = _config.Current.DisableOnShift && 
                                  (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
            bool isAltPressed = _config.Current.DisableOnAlt && 
                                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0;
            bool isModifierPressed = isCtrlPressed || isShiftPressed || isAltPressed;

            bool isFullScreen = _config.Current.DisableOnFullScreen && FullScreenHelper.IsActiveWindowFullScreen();

            if (isBlacklisted || isModifierPressed || isFullScreen)
            {
                Debug.WriteLine($"Gesture trigger isolated. Process: {processName}, Blacklisted: {isBlacklisted}, Modifier: {isModifierPressed}, FullScreen: {isFullScreen}. Passing right click through.");
                _isWaitingForThreshold = false;
                _isGestureActive = false;
                e.Handled = false; // Do not block the right-down event
                return;
            }

            _startPoint = e.Position;
            _isWaitingForThreshold = true;
            _isGestureActive = false;
            _selectedSectorIndex = -1;

            // Block the initial right down, we will replay it if it's just a click
            e.Handled = true;
            Debug.WriteLine($"RightMouseDown at {_startPoint.X}, {_startPoint.Y}. Waiting for threshold.");
        }

        private void Hook_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isWaitingForThreshold)
            {
                double dx = e.Position.X - _startPoint.X;
                double dy = e.Position.Y - _startPoint.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance >= _config.Current.DragThreshold)
                {
                    _isWaitingForThreshold = false;
                    _isGestureActive = true;

                    // Detect foreground process
                    string processName = ActiveWindowHelper.GetActiveWindowProcessName();
                    _activeProfile = _config.GetProfileForProcess(processName);

                    Debug.WriteLine($"Gesture activated. Process: {processName}, Profile: {_activeProfile.ProcessName}, Sectors: {_activeProfile.SectorCount}");

                    // Show the UI on the main thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ShowRadialUI(_startPoint, _activeProfile);
                        UpdateSelectedSector(e.Position);
                    });
                }
            }
            else if (_isGestureActive)
            {
                // Update selected sector
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateSelectedSector(e.Position);
                });
            }
        }

        private void Hook_OnRightButtonUp(object sender, MouseEventArgs e)
        {
            if (_isWaitingForThreshold)
            {
                _isWaitingForThreshold = false;
                Debug.WriteLine("Normal click detected. Replaying right click.");

                // Replay the right click on another thread to avoid blocking the hook thread
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _mouseHook.ReplayRightClick();
                }));
                e.Handled = true;
            }
            else if (_isGestureActive)
            {
                _isGestureActive = false;
                Debug.WriteLine($"Gesture completed. Selected sector: {_selectedSectorIndex}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    HideRadialUI();

                    if (_activeProfile != null && _selectedSectorIndex >= 0 && _selectedSectorIndex < _activeProfile.Actions.Count)
                    {
                        var action = _activeProfile.Actions[_selectedSectorIndex];
                        if (action != null && !string.IsNullOrEmpty(action.Type))
                        {
                            Debug.WriteLine($"Executing action: {action.Name} ({action.Type}: {action.Parameter})");
                            ActionExecutor.Execute(action);
                        }
                    }
                });

                e.Handled = true;
            }
        }

        private void ShowRadialUI(Point center, WheelProfile profile)
        {
            if (_radialWindow != null)
            {
                _radialWindow.Close();
            }

            _radialWindow = new RadialWindow(center, profile);
            _radialWindow.Show();
        }

        private void UpdateSelectedSector(Point currentPoint)
        {
            if (_radialWindow == null || _activeProfile == null) return;

            double dx = currentPoint.X - _startPoint.X;
            double dy = currentPoint.Y - _startPoint.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // 1. Center deadzone cancel (拖回中心核圆取消)
            if (distance < _config.Current.DragThreshold * 0.6)
            {
                _selectedSectorIndex = -1;
                _radialWindow.HighlightSector(-1);
                _radialWindow.SetOuterEscapeState(false);
                return;
            }

            // 2. Scheme 2: Outer Escape Cancel (顺势外甩脱离取消)
            bool enableOuterEscape = _config.Current.EnableOuterEscapeCancel;
            double outerRadius = _config.Current.WheelRadius > 0 ? _config.Current.WheelRadius : 138.0;
            double escapeThreshold = _config.Current.OuterEscapeDistance > 0 
                ? _config.Current.OuterEscapeDistance 
                : outerRadius * 1.50;

            if (enableOuterEscape && distance > escapeThreshold)
            {
                _selectedSectorIndex = -1;
                _radialWindow.HighlightSector(-1);
                _radialWindow.SetOuterEscapeState(true);
                return;
            }
            else
            {
                _radialWindow.SetOuterEscapeState(false);
            }

            // Calculate angle in degrees from [0, 360)
            double radians = Math.Atan2(dy, dx);
            double degrees = radians * (180.0 / Math.PI);
            if (degrees < 0)
            {
                degrees += 360.0;
            }

            int n = _activeProfile.SectorCount;
            double sectorSize = 360.0 / n;

            // Math.Round aligns 0 degrees (Right) as the center of sector 0
            int index = (int)Math.Round(degrees / sectorSize) % n;

            _selectedSectorIndex = index;
            _radialWindow.HighlightSector(index);
        }

        private void HideRadialUI()
        {
            if (_radialWindow != null)
            {
                _radialWindow.Close();
                _radialWindow = null;
                MemoryOptimizer.TrimMemory();
            }
        }
    }
}
