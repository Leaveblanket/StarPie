using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures
{
    using Button = System.Windows.Controls.Button;
    using Color = System.Windows.Media.Color;
    using ColorConverter = System.Windows.Media.ColorConverter;
    using Cursors = System.Windows.Input.Cursors;
    using Point = System.Windows.Point;
    using Orientation = System.Windows.Controls.Orientation;
    using Brushes = System.Windows.Media.Brushes;
    using HorizontalAlignment = System.Windows.HorizontalAlignment;
    using VerticalAlignment = System.Windows.VerticalAlignment;

    public partial class ColorPickerWindow : Window
    {
        public string SelectedHexColor { get; private set; } = "#FF2563EB";

        private double _hue = 0;        // 0 ~ 360
        private double _saturation = 1; // 0 ~ 1
        private double _value = 1;      // 0 ~ 1
        private byte _alpha = 255;      // 0 ~ 255
        private bool _isUpdating = false;

        private static readonly string[] PresetColors = new[]
        {
            "#EB18181B", "#F0F8FAFC", "#FF2563EB", "#FF3B82F6", "#FF60A5FA", "#FF06B6D4", "#FF0EA5E9",
            "#FF10B981", "#FF22C55E", "#FF84CC16", "#FFEAB308", "#FFF97316", "#FFEF4444", "#FFF43F5E",
            "#FFEC4899", "#FFD946EF", "#FFA855F7", "#FF8B5CF6", "#FF6366F1", "#FF475569", "#FF64748B",
            "#FF94A3B8", "#FFCBD5E1", "#FFF1F5F9", "#FFFFFF", "#FF000000", "#9016161A", "#35FFFFFF",
            "#E06C4DFF", "#A0FFFFFF", "#E0FFFFFF", "#E60F172A", "#E61E1B4B", "#E6142E1F", "#E6181111"
        };

        public ColorPickerWindow(IThemeService themeService, string initialHex = "#FF2563EB")
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            PopulateSwatches();
            SetColorFromHex(string.IsNullOrWhiteSpace(initialHex) ? "#FF2563EB" : initialHex);
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            this.Title = $"{I18n.T("ColorPickerTitle")} - StarPie";
            if (HueLabelText != null) HueLabelText.Text = I18n.T("ColorPickerHue");
            if (AlphaLabelText != null) AlphaLabelText.Text = I18n.T("ColorPickerAlpha");
            if (EyedropperTitleText != null) EyedropperTitleText.Text = I18n.T("ColorPickerEyedropperTitle");
            if (EyedropperDescText != null) EyedropperDescText.Text = I18n.T("ColorPickerEyedropperDesc");
            if (EyedropperButton != null) EyedropperButton.Content = I18n.T("ColorPickerEyedropperBtn");
            if (SwatchesTitleText != null) SwatchesTitleText.Text = I18n.T("ColorPickerSwatches");
            if (CancelButton != null) CancelButton.Content = I18n.T("BtnCancel");
            if (OkButton != null) OkButton.Content = I18n.T("ColorPickerApply");
        }

        private void PopulateSwatches()
        {
            SwatchesPanel.Children.Clear();
            foreach (var hex in PresetColors)
            {
                try
                {
                    var col = (Color)ColorConverter.ConvertFromString(hex);
                    var btn = new Button
                    {
                        Style = (Style)FindResource("ColorSwatchButtonStyle"),
                        Background = new SolidColorBrush(col),
                        ToolTip = hex
                    };
                    btn.Click += (s, e) => SetColorFromHex(hex);
                    SwatchesPanel.Children.Add(btn);
                }
                catch { }
            }
        }

        public void SetColorFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                var color = (Color)ColorConverter.ConvertFromString(hex);
                _alpha = color.A;
                ColorToHsv(color, out _hue, out _saturation, out _value);

                _isUpdating = true;
                HueSlider.Value = _hue;
                AlphaSlider.Value = _alpha;
                HexInputBox.Text = hex.ToUpper();
                _isUpdating = false;

                UpdateSpectrumCanvasColor();
                UpdateSpectrumThumbPosition();
                UpdatePreview();
            }
            catch { }
        }

        private void UpdateSpectrumCanvasColor()
        {
            var pureHueColor = HsvToRgb(_hue, 1, 1);
            SpectrumCanvas.Background = new SolidColorBrush(pureHueColor);
        }

        private void UpdateSpectrumThumbPosition()
        {
            double w = SpectrumCanvas.ActualWidth > 0 ? SpectrumCanvas.ActualWidth : 440;
            double h = SpectrumCanvas.ActualHeight > 0 ? SpectrumCanvas.ActualHeight : 180;

            double x = _saturation * w;
            double y = (1.0 - _value) * h;

            Canvas.SetLeft(SpectrumThumb, x);
            Canvas.SetTop(SpectrumThumb, y);
        }

        private void UpdatePreview()
        {
            var rgb = HsvToRgb(_hue, _saturation, _value);
            var finalColor = Color.FromArgb(_alpha, rgb.R, rgb.G, rgb.B);
            SelectedHexColor = $"#{finalColor.A:X2}{finalColor.R:X2}{finalColor.G:X2}{finalColor.B:X2}";

            ColorPreviewBorder.Background = new SolidColorBrush(finalColor);

            if (!_isUpdating && HexInputBox != null)
            {
                _isUpdating = true;
                HexInputBox.Text = SelectedHexColor;
                _isUpdating = false;
            }
        }

        private void SpectrumCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                SpectrumCanvas.CaptureMouse();
                UpdateFromSpectrumMouse(e.GetPosition(SpectrumCanvas));
            }
        }

        private void SpectrumCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && SpectrumCanvas.IsMouseCaptured)
            {
                UpdateFromSpectrumMouse(e.GetPosition(SpectrumCanvas));
            }
        }

        protected override void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (SpectrumCanvas.IsMouseCaptured)
            {
                SpectrumCanvas.ReleaseMouseCapture();
            }
        }

        private void UpdateFromSpectrumMouse(Point pos)
        {
            double w = SpectrumCanvas.ActualWidth > 0 ? SpectrumCanvas.ActualWidth : 440;
            double h = SpectrumCanvas.ActualHeight > 0 ? SpectrumCanvas.ActualHeight : 180;

            double x = Math.Max(0, Math.Min(w, pos.X));
            double y = Math.Max(0, Math.Min(h, pos.Y));

            _saturation = x / w;
            _value = 1.0 - (y / h);

            UpdateSpectrumThumbPosition();
            UpdatePreview();
        }

        private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            _hue = HueSlider.Value;
            UpdateSpectrumCanvasColor();
            UpdatePreview();
        }

        private void AlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            _alpha = (byte)AlphaSlider.Value;
            UpdatePreview();
        }

        private void HexInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            string text = HexInputBox.Text.Trim();
            if (text.Length == 7 || text.Length == 9)
            {
                try
                {
                    if (!text.StartsWith("#")) text = "#" + text;
                    var color = (Color)ColorConverter.ConvertFromString(text);
                    _alpha = color.A;
                    ColorToHsv(color, out _hue, out _saturation, out _value);

                    _isUpdating = true;
                    HueSlider.Value = _hue;
                    AlphaSlider.Value = _alpha;
                    _isUpdating = false;

                    UpdateSpectrumCanvasColor();
                    UpdateSpectrumThumbPosition();
                    UpdatePreview();
                }
                catch { }
            }
        }

        private void Eyedropper_Click(object sender, RoutedEventArgs e)
        {
            var eyedropper = new ScreenEyedropperOverlay();
            if (eyedropper.ShowDialog() == true && !string.IsNullOrEmpty(eyedropper.CapturedHexColor))
            {
                SetColorFromHex(eyedropper.CapturedHexColor);
            }
        }

        private void SwatchesScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ScrollViewer scv)
            {
                scv.ScrollToVerticalOffset(scv.VerticalOffset - (e.Delta / 3.0));
                e.Handled = true;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #region HSV / RGB Conversion

        private static Color HsvToRgb(double h, double s, double v)
        {
            int hi = (int)Math.Floor(h / 60) % 6;
            double f = (h / 60) - Math.Floor(h / 60);

            v *= 255;
            byte vVal = (byte)Math.Max(0, Math.Min(255, v));
            byte p = (byte)Math.Max(0, Math.Min(255, v * (1 - s)));
            byte q = (byte)Math.Max(0, Math.Min(255, v * (1 - f * s)));
            byte t = (byte)Math.Max(0, Math.Min(255, v * (1 - (1 - f) * s)));

            switch (hi)
            {
                case 0: return Color.FromRgb(vVal, t, p);
                case 1: return Color.FromRgb(q, vVal, p);
                case 2: return Color.FromRgb(p, vVal, t);
                case 3: return Color.FromRgb(p, q, vVal);
                case 4: return Color.FromRgb(t, p, vVal);
                default: return Color.FromRgb(vVal, p, q);
            }
        }

        private static void ColorToHsv(Color color, out double h, out double s, out double v)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            v = max;

            if (max <= 0) s = 0;
            else s = delta / max;

            if (delta <= 0)
            {
                h = 0;
            }
            else
            {
                if (Math.Abs(r - max) < 0.0001) h = (g - b) / delta;
                else if (Math.Abs(g - max) < 0.0001) h = 2 + (b - r) / delta;
                else h = 4 + (r - g) / delta;

                h *= 60;
                if (h < 0) h += 360;
            }
        }

        #endregion
    }

    #region Screen Eyedropper Overlay Window

    public class ScreenEyedropperOverlay : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public string CapturedHexColor { get; private set; } = "";

        private readonly Canvas _loupeCanvas;
        private readonly Border _loupeBorder;
        private readonly TextBlock _hexLabel;
        private readonly Border _swatchBorder;

        public ScreenEyedropperOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            _loupeCanvas = new Canvas { IsHitTestVisible = false };
            Content = _loupeCanvas;

            _loupeBorder = new Border
            {
                Width = 110,
                Height = 60,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.4 }
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            _swatchBorder = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.White, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 0) };
            _hexLabel = new TextBlock { Text = "#FFFFFF", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };

            topRow.Children.Add(_swatchBorder);
            topRow.Children.Add(_hexLabel);
            stack.Children.Add(topRow);

            var hint = new TextBlock { Text = "单击取色 / Esc取消", FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(200, 203, 213, 225)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            stack.Children.Add(hint);

            _loupeBorder.Child = stack;
            _loupeCanvas.Children.Add(_loupeBorder);

            MouseMove += ScreenEyedropperOverlay_MouseMove;
            MouseDown += ScreenEyedropperOverlay_MouseDown;
            KeyDown += ScreenEyedropperOverlay_KeyDown;
        }

        private void ScreenEyedropperOverlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (GetCursorPos(out POINT pt))
            {
                Color c = GetPixelColor(pt.X, pt.Y);
                string hex = $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
                _hexLabel.Text = hex;
                _swatchBorder.Background = new SolidColorBrush(c);

                Point winPos = e.GetPosition(this);
                double lx = winPos.X + 20;
                double ly = winPos.Y + 20;

                if (lx + _loupeBorder.Width > ActualWidth) lx = winPos.X - _loupeBorder.Width - 10;
                if (ly + _loupeBorder.Height > ActualHeight) ly = winPos.Y - _loupeBorder.Height - 10;

                Canvas.SetLeft(_loupeBorder, lx);
                Canvas.SetTop(_loupeBorder, ly);
            }
        }

        private void ScreenEyedropperOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (GetCursorPos(out POINT pt))
                {
                    Color c = GetPixelColor(pt.X, pt.Y);
                    CapturedHexColor = $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
                    DialogResult = true;
                    Close();
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                DialogResult = false;
                Close();
            }
        }

        private void ScreenEyedropperOverlay_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private static Color GetPixelColor(int x, int y)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            uint pixel = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);

            byte r = (byte)(pixel & 0x000000FF);
            byte g = (byte)((pixel & 0x0000FF00) >> 8);
            byte b = (byte)((pixel & 0x00FF0000) >> 16);

            return Color.FromRgb(r, g, b);
        }
    }

    #endregion
}
