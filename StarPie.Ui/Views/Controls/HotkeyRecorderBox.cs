using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Control = System.Windows.Controls.Control;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Cursors = System.Windows.Input.Cursors;

namespace StarPie.Views.Controls
{
    /// <summary>
    /// 热键录制输入框：文案与状态配色一律声明式。
    /// </summary>
    /// <remarks>
    /// 占位文案由消费方经 <see cref="Placeholder"/> 传入（{DynamicResource} 语言键）；
    /// 录制提示与录制态配色由控件模板（Views/Styles/HotkeyRecorderBox.xaml）持有，
    /// code-behind 只负责输入逻辑与动态文本/可见性编排，不出现静态文案或 hex 画刷。
    /// </remarks>
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
                new PropertyMetadata(string.Empty, OnPlaceholderChanged));

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
        private TextBlock? _placeholderTextBlock;
        private TextBlock? _hintTextBlock;
        private Button? _clearButton;

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
            _placeholderTextBlock = GetTemplateChild("PART_PlaceholderText") as TextBlock;
            _hintTextBlock = GetTemplateChild("PART_HintText") as TextBlock;
            _clearButton = GetTemplateChild("PART_ClearButton") as Button;

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

            // 处理取消（Esc）与清除（Backspace/Delete）。
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

            // 若只按下修饰键（Ctrl/Shift/Alt/Win），仅做组合提示，不结束录制。
            if (IsModifierKey(key))
            {
                UpdateModifierOnlyDisplay();
                return;
            }

            // 有效组合键：拼出标准表示并结束录制。
            string combo = BuildHotkeyString(key);
            if (!string.IsNullOrEmpty(combo))
            {
                HotkeyText = combo;
                IsRecording = false;
                Keyboard.ClearFocus();
                UpdateVisualDisplay();
            }
        }

        /// <summary>键抬起时若仍在录制，刷新“修饰键 + …”的中间提示（避免抬起后残留旧文本）。</summary>
        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            if (IsRecording)
            {
                e.Handled = true;
                UpdateModifierOnlyDisplay();
            }
            base.OnPreviewKeyUp(e);
        }

        /// <summary>判断是否为纯修饰键（左右 Ctrl/Alt/Shift 与 Win 键）。</summary>
        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin;
        }

        /// <summary>录制中只按下修饰键时，显示"Ctrl + Shift + …"式提示；无修饰键则隐藏提示。</summary>
        private void UpdateModifierOnlyDisplay()
        {
            if (!IsRecording) return;

            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) sb.Append("Ctrl + ");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift + ");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt + ");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0 || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) sb.Append("Win + ");

            bool hasModifier = sb.Length > 0;
            if (_displayTextBlock != null)
            {
                _displayTextBlock.Text = hasModifier ? sb.ToString() + "..." : string.Empty;
                _displayTextBlock.Visibility = hasModifier ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_hintTextBlock != null)
            {
                _hintTextBlock.Visibility = hasModifier ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        /// <summary>把当前按下的修饰键与主键拼成标准的 "Ctrl + Shift + A" 形式；仅修饰键时返回空。</summary>
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

        /// <summary>把 WPF 键值格式化为可读文本：数字键还原数字、符号键还原符号、其余用键名。</summary>
        private static string FormatKeyName(Key key)
        {
            // 主键盘数字键 D0–D9：去掉键名前缀输出数字本身。
            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)key - (int)Key.D0).ToString();
            }

            // 小键盘数字键 NumPad0–NumPad9：加 "Num" 前缀以示区分。
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return "Num" + ((int)key - (int)Key.NumPad0).ToString();
            }

            // 功能键 F1–F24：直接用键名。
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

                // OEM 符号键：映射为对应符号字符。
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

                // 小键盘运算键：以 Num 前缀命名，与主键盘符号区分。
                case Key.Add: return "NumAdd";
                case Key.Subtract: return "NumSubtract";
                case Key.Multiply: return "NumMultiply";
                case Key.Divide: return "NumDivide";
                case Key.Decimal: return "NumDecimal";

                // 媒体与浏览器键。
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

        /// <summary>按当前状态（是否录制、是否有已录制文本）统一编排占位/提示/显示/清除按钮的可见性。</summary>
        private void UpdateVisualDisplay()
        {
            bool isRecording = IsRecording;
            bool hasText = !string.IsNullOrEmpty(HotkeyText);
            bool showPlaceholder = !isRecording && !hasText && !string.IsNullOrEmpty(Placeholder);

            if (_placeholderTextBlock != null)
            {
                _placeholderTextBlock.Visibility = showPlaceholder ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_hintTextBlock != null)
            {
                _hintTextBlock.Visibility = isRecording ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_displayTextBlock != null)
            {
                if (!isRecording)
                {
                    // 仅回填动态内容（已录制热键）；占位/提示静态文案由模板声明式提供。
                    _displayTextBlock.Text = HotkeyText;
                }
                _displayTextBlock.Visibility = (!isRecording && hasText) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_clearButton != null)
            {
                _clearButton.Visibility = (!isRecording && hasText) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>HotkeyText 变化时刷新显示。</summary>
        private static void OnHotkeyTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyRecorderBox control)
            {
                control.UpdateVisualDisplay();
            }
        }

        /// <summary>录制状态变化时刷新显示。</summary>
        private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyRecorderBox control)
            {
                control.UpdateVisualDisplay();
            }
        }

        /// <summary>占位文案变化时刷新显示。</summary>
        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyRecorderBox control)
            {
                control.UpdateVisualDisplay();
            }
        }
    }
}
