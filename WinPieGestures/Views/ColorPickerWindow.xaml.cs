using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures.Views
{
    using Button = System.Windows.Controls.Button;
    using Color = System.Windows.Media.Color;
    using ColorConverter = System.Windows.Media.ColorConverter;
    using Cursors = System.Windows.Input.Cursors;
    using Orientation = System.Windows.Controls.Orientation;
    using Point = System.Windows.Point;

    /// <summary>
    /// 颜色选择器窗口 (T08)：HSV 状态机、十六进制输入解析与确认结果全部在
    /// <see cref="ColorPickerViewModel"/>；code-behind 只剩色盘取点、取色圈定位、
    /// 本地化文案与把确认/取消落成 DialogResult。由 <see cref="DialogService"/> 创建，
    /// Owner 归设置窗口。
    /// </summary>
    public partial class ColorPickerWindow : Window
    {
        private readonly ColorPickerViewModel _vm;

        public ColorPickerWindow(IThemeService themeService, IDialogService dialogService, string initialHex = "#FF2563EB")
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            _vm = new ColorPickerViewModel(dialogService, initialHex);
            _vm.SpectrumChanged += UpdateSpectrumThumbPosition;
            DataContext = _vm;
            PopulateSwatches();
            UpdateSpectrumThumbPosition();
            ApplyLocalization();
        }

        /// <summary>确认结果（仅在 DialogResult == true 时非空）。</summary>
        public ColorPickResult? BuildResult() => _vm.BuildResult();

        private void PopulateSwatches()
        {
            SwatchesPanel.Children.Clear();
            foreach (var hex in ColorPickerViewModel.PresetColors)
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
                    btn.Click += (s, e) => _vm.SetColorFromHex(hex);
                    SwatchesPanel.Children.Add(btn);
                }
                catch { }
            }
        }

        private void UpdateSpectrumThumbPosition()
        {
            double w = SpectrumCanvas.ActualWidth > 0 ? SpectrumCanvas.ActualWidth : 440;
            double h = SpectrumCanvas.ActualHeight > 0 ? SpectrumCanvas.ActualHeight : 180;

            Canvas.SetLeft(SpectrumThumb, _vm.Saturation * w);
            Canvas.SetTop(SpectrumThumb, (1.0 - _vm.Value) * h);
        }

        private void SpectrumCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                SpectrumCanvas.CaptureMouse();
                UpdateFromSpectrumMouse(e.GetPosition(SpectrumCanvas));
            }
        }

        private void SpectrumCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && SpectrumCanvas.IsMouseCaptured)
            {
                UpdateFromSpectrumMouse(e.GetPosition(SpectrumCanvas));
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
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

            _vm.SetSpectrumPoint(x / w, 1.0 - (y / h));
        }

        private void SwatchesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scv)
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
    }

    #region Screen Eyedropper Overlay Window

    /// <summary>
    /// 屏上取色覆盖层 (T08)：全屏置顶、无 Owner（ADR-0004）。拾取状态与结果在
    /// <see cref="ScreenEyedropperViewModel"/>，code-behind 只剩 Win32 取像素与放大镜摆放。
    /// </summary>
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

        private readonly ScreenEyedropperViewModel _vm = new();
        private readonly Canvas _loupeCanvas;
        private readonly Border _loupeBorder;

        /// <summary>捕获的颜色（仅在 DialogResult == true 时非空）。</summary>
        public string? CapturedHexColor => _vm.CapturedHexColor;

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

            var swatchBorder = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), BorderBrush = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 0) };
            var hexLabel = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center };

            topRow.Children.Add(swatchBorder);
            topRow.Children.Add(hexLabel);
            stack.Children.Add(topRow);

            var hint = new TextBlock { Text = "单击取色 / Esc取消", FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(200, 203, 213, 225)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            stack.Children.Add(hint);

            _loupeBorder.Child = stack;
            _loupeCanvas.Children.Add(_loupeBorder);

            // 放大镜文案与色块走 VM 绑定。
            DataContext = _vm;
            hexLabel.SetBinding(TextBlock.TextProperty, new Binding(nameof(ScreenEyedropperViewModel.HexText)));
            swatchBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(ScreenEyedropperViewModel.SwatchBrush)));

            _vm.CloseRequested += confirmed =>
            {
                DialogResult = confirmed;
                Close();
            };

            MouseMove += ScreenEyedropperOverlay_MouseMove;
            MouseDown += ScreenEyedropperOverlay_MouseDown;
            KeyDown += ScreenEyedropperOverlay_KeyDown;
        }

        private void ScreenEyedropperOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (GetCursorPos(out POINT pt))
            {
                Color c = GetPixelColor(pt.X, pt.Y);
                _vm.TrackColor(c.R, c.G, c.B);

                Point winPos = e.GetPosition(this);
                var (lx, ly) = ScreenEyedropperViewModel.GetLoupePosition(
                    winPos.X, winPos.Y, _loupeBorder.Width, _loupeBorder.Height, ActualWidth, ActualHeight);

                Canvas.SetLeft(_loupeBorder, lx);
                Canvas.SetTop(_loupeBorder, ly);
            }
        }

        private void ScreenEyedropperOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (GetCursorPos(out POINT pt))
                {
                    Color c = GetPixelColor(pt.X, pt.Y);
                    _vm.Capture(c.R, c.G, c.B);
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                _vm.Cancel();
            }
        }

        private void ScreenEyedropperOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _vm.Cancel();
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
