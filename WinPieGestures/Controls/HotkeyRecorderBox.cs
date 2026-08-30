using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Control = System.Windows.Controls.Control;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;

namespace WinPieGestures
{
    public class HotkeyRecorderBox : Control
    {
        public static readonly DependencyProperty HotkeyTextProperty =
            DependencyProperty.Register(
                nameof(HotkeyText), 
                typeof(string), 
                typeof(HotkeyRecorderBox), 
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyTextChanged));

        public static readonly DependencyProperty IsRecordingProperty =
            DependencyProperty.Register(
                nameof(IsRecording), 
                typeof(bool), 
                typeof(HotkeyRecorderBox), 
                new PropertyMetadata(false, OnIsRecordingChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder), 
                typeof(string), 
                typeof(HotkeyRecorderBox), 
                new PropertyMetadata("点击录制快捷键..."));

        public string HotkeyText
        {
            get => (string)GetValue(HotkeyTextProperty);
            set => SetValue(HotkeyTextProperty, value);
        }

        public bool IsRecording
        {
            get => (bool)GetValue(IsRecordingProperty);
            set => SetValue(IsRecordingProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        private TextBlock? _displayTextBlock;
        private Button? _clearButton;
        private Border? _mainBorder;

        static HotkeyRecorderBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyRecorderBox), new FrameworkPropertyMetadata(typeof(HotkeyRecorderBox)));
            FocusableProperty.OverrideMetadata(typeof(HotkeyRecorderBox), new FrameworkPropertyMetadata(true));
        }

        public HotkeyRecorderBox()
        {
            FocusVisualStyle = null;
            Cursor = Cursors.Hand;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _displayTextBlock = GetTemplateChild("PART_DisplayText") as TextBlock;
            _clearButton = GetTemplateChild("PART_ClearButton") as Button;
            _mainBorder = GetTemplateChild("PART_Border") as Border;

            if (_clearButton != null)
            {
                _clearButton.Click += (s, e) =>
                {
                    HotkeyText = string.Empty;
                    IsRecording = false;
                    e.Handled = true;
                };
            }

            UpdateVisualDisplay();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Left)
            {
                Focus();
                IsRecording = true;
                e.Handled = true;
            }
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            IsRecording = true;
            UpdateVisualDisplay();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            IsRecording = false;
            UpdateVisualDisplay();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!IsRecording)
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            e.Handled = true;
            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            // Handle Cancel & Clear
            if (key == Key.Escape)
            {
                IsRecording = false;
                Keyboard.ClearFocus();
                UpdateVisualDisplay();
                return;
            }

            if (key == Key.Back || key == Key.Delete)
            {
                HotkeyText = string.Empty;
                IsRecording = false;
                Keyboard.ClearFocus();
                UpdateVisualDisplay();
                return;
            }

            // Check if it's purely a modifier key press
            if (IsModifierKey(key))
            {
                UpdateModifierOnlyDisplay();
                return;
            }

            // Valid key combo: Build standard representation
            string combo = BuildHotkeyString(key);
            if (!string.IsNullOrEmpty(combo))
            {
                HotkeyText = combo;
                IsRecording = false;
                Keyboard.ClearFocus();
                UpdateVisualDisplay();
            }
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            if (IsRecording)
            {
                e.Handled = true;
                UpdateModifierOnlyDisplay();
            }
            base.OnPreviewKeyUp(e);
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin;
        }

        private void UpdateModifierOnlyDisplay()
        {
            if (!IsRecording) return;

            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) sb.Append("Ctrl + ");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift + ");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt + ");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0 || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) sb.Append("Win + ");

            if (sb.Length > 0)
            {
                if (_displayTextBlock != null)
                {
                    _displayTextBlock.Text = sb.ToString() + "...";
                    _displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
                }
            }
            else
            {
                if (_displayTextBlock != null)
                {
                    _displayTextBlock.Text = "🔴 请按下快捷键组合...";
                    _displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E11D48"));
                }
            }
        }

        private static string BuildHotkeyString(Key mainKey)
        {
            var parts = new List<string>();

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0 || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) parts.Add("Win");

            string keyName = FormatKeyName(mainKey);
            if (!string.IsNullOrEmpty(keyName))
            {
                parts.Add(keyName);
            }

            return string.Join(" + ", parts);
        }

        private static string FormatKeyName(Key key)
        {
            // D0 - D9
            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)key - (int)Key.D0).ToString();
            }

            // NumPad0 - NumPad9
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return "Num" + ((int)key - (int)Key.NumPad0).ToString();
            }

            // Function keys
            if (key >= Key.F1 && key <= Key.F24)
            {
                return key.ToString();
            }

            switch (key)
            {
                case Key.Return: return "Enter";
                case Key.Space: return "Space";
                case Key.Tab: return "Tab";
                case Key.Back: return "Backspace";
                case Key.Delete: return "Delete";
                case Key.Insert: return "Insert";
                case Key.Home: return "Home";
                case Key.End: return "End";
                case Key.PageUp: return "PageUp";
                case Key.PageDown: return "PageDown";
                case Key.Left: return "Left";
                case Key.Up: return "Up";
                case Key.Right: return "Right";
                case Key.Down: return "Down";
                case Key.PrintScreen: return "PrintScreen";
                case Key.Pause: return "Pause";
                case Key.CapsLock: return "CapsLock";
                case Key.Scroll: return "ScrollLock";
                case Key.NumLock: return "NumLock";

                // OEM Symbols
                case Key.Oem1: return ";";
                case Key.OemPlus: return "=";
                case Key.OemComma: return ",";
                case Key.OemMinus: return "-";
                case Key.OemPeriod: return ".";
                case Key.Oem2: return "/";
                case Key.Oem3: return "`";
                case Key.Oem4: return "[";
                case Key.Oem5: return "\\";
                case Key.Oem6: return "]";
                case Key.Oem7: return "'";

                // Math & Numpad
                case Key.Add: return "NumAdd";
                case Key.Subtract: return "NumSubtract";
                case Key.Multiply: return "NumMultiply";
                case Key.Divide: return "NumDivide";
                case Key.Decimal: return "NumDecimal";

                // Media & Browser
                case Key.VolumeMute: return "VolumeMute";
                case Key.VolumeDown: return "VolumeDown";
                case Key.VolumeUp: return "VolumeUp";
                case Key.MediaNextTrack: return "MediaNext";
                case Key.MediaPreviousTrack: return "MediaPrev";
                case Key.MediaStop: return "MediaStop";
                case Key.MediaPlayPause: return "MediaPlayPause";
                case Key.BrowserBack: return "BrowserBack";
                case Key.BrowserForward: return "BrowserForward";
                case Key.BrowserRefresh: return "BrowserRefresh";
                case Key.BrowserHome: return "BrowserHome";
                case Key.BrowserSearch: return "BrowserSearch";

                default:
                    return key.ToString();
            }
        }

        private void UpdateVisualDisplay()
        {
            if (_displayTextBlock == null) return;

            if (IsRecording)
            {
                _displayTextBlock.Text = "🔴 请按下快捷键组合...";
                _displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E11D48"));
                if (_mainBorder != null)
                {
                    _mainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
                    _mainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
                }
            }
            else
            {
                if (string.IsNullOrEmpty(HotkeyText))
                {
                    _displayTextBlock.Text = Placeholder;
                    _displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                }
                else
                {
                    _displayTextBlock.Text = HotkeyText;
                    _displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                }

                if (_mainBorder != null)
                {
                    _mainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                    _mainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                }
            }

            if (_clearButton != null)
            {
                _clearButton.Visibility = (!string.IsNullOrEmpty(HotkeyText) && !IsRecording) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnHotkeyTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyRecorderBox control)
            {
                control.UpdateVisualDisplay();
            }
        }

        private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyRecorderBox control)
            {
                control.UpdateVisualDisplay();
            }
        }
    }
}
