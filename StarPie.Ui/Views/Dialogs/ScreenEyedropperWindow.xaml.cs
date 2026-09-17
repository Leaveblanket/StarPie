using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StarPie.ViewModels.Dialogs;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace StarPie.Views.Dialogs
{
    /// <summary>
    /// 屏上取色覆盖层窗口：全屏置顶、无 Owner。
    /// 拾取状态与结果在 <see cref="ScreenEyedropperViewModel"/>；code-behind 只剩
    /// Win32 取像素与放大镜摆放（纯视觉白名单）。
    /// Win32 声明来自 CsWin32 源生成（清单为项目根 NativeMethods.txt，ADR-0051）。
    /// </summary>
    public partial class ScreenEyedropperWindow : Window
    {
        private readonly ScreenEyedropperViewModel _vm;

        /// <summary>捕获的颜色（仅在 DialogResult == true 时非空）。</summary>
        public string? CapturedHexColor => _vm.CapturedHexColor;

        public ScreenEyedropperWindow(ScreenEyedropperViewModel viewModel)
        {
            InitializeComponent();

            _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }

        private void ScreenEyedropperWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (PInvoke.GetCursorPos(out System.Drawing.Point pt))
            {
                Color c = GetPixelColor(pt.X, pt.Y);
                _vm.TrackColor(c.R, c.G, c.B);

                Point winPos = e.GetPosition(this);
                var (lx, ly) = ScreenEyedropperViewModel.GetLoupePosition(
                    winPos.X, winPos.Y, LoupeBorder.Width, LoupeBorder.Height, ActualWidth, ActualHeight);

                Canvas.SetLeft(LoupeBorder, lx);
                Canvas.SetTop(LoupeBorder, ly);
            }
        }

        private void ScreenEyedropperWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (PInvoke.GetCursorPos(out System.Drawing.Point pt))
                {
                    Color c = GetPixelColor(pt.X, pt.Y);
                    DialogResult = _vm.Capture(c.R, c.G, c.B);
                    Close();
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                DialogResult = _vm.Cancel();
                Close();
            }
        }

        private void ScreenEyedropperWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = _vm.Cancel();
                Close();
            }
        }

        private static Color GetPixelColor(int x, int y)
        {
            HDC hdc = PInvoke.GetDC(default);
            COLORREF pixel = PInvoke.GetPixel(hdc, x, y);
            _ = PInvoke.ReleaseDC(default, hdc);

            byte r = (byte)(pixel.Value & 0x000000FF);
            byte g = (byte)((pixel.Value & 0x0000FF00) >> 8);
            byte b = (byte)((pixel.Value & 0x00FF0000) >> 16);

            return Color.FromRgb(r, g, b);
        }
    }
}
