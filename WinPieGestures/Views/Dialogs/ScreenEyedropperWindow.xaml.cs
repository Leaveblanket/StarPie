using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinPieGestures.ViewModels.Dialogs;

namespace WinPieGestures.Views.Dialogs
{
    /// <summary>
    /// 屏上取色覆盖层窗口 (T08, ADR-0004/0009)：全屏置顶、无 Owner。
    /// 拾取状态与结果在 <see cref="ScreenEyedropperViewModel"/>；code-behind 只剩
    /// Win32 取像素与放大镜摆放（纯视觉白名单）。
    /// </summary>
    public partial class ScreenEyedropperWindow : Window
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
            if (GetCursorPos(out POINT pt))
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
                if (GetCursorPos(out POINT pt))
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
            IntPtr hdc = GetDC(IntPtr.Zero);
            uint pixel = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);

            byte r = (byte)(pixel & 0x000000FF);
            byte g = (byte)((pixel & 0x0000FF00) >> 8);
            byte b = (byte)((pixel & 0x00FF0000) >> 16);

            return Color.FromRgb(r, g, b);
        }
    }
}
