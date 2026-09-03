using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinPieGestures.ViewModels.Dialogs;

namespace WinPieGestures.Views.Controls
{
    /// <summary>
    /// 色盘取点附加行为 (ADR-0009)：Canvas 没有 Command 属性，鼠标按下/拖动属原始输入——
    /// 本行为负责纯 UI 的坐标翻译（像素 → 归一化饱和度/明度点，含捕获与夹紧），再执行
    /// 绑定的 VM 命令（<see cref="ColorPickerViewModel.SetSpectrumPointActionCommand"/>）。
    /// 颜色状态与夹紧兜底仍归 VM，行为不触碰业务。
    /// </summary>
    public static class SpectrumCanvasBehavior
    {
        /// <summary>取点命令（DataContext 绑定到 ColorPickerViewModel.SetSpectrumPointActionCommand）。</summary>
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(ICommand),
                typeof(SpectrumCanvasBehavior),
                new PropertyMetadata(null, OnCommandChanged));

        public static ICommand? GetCommand(DependencyObject d) => (ICommand?)d.GetValue(CommandProperty);

        public static void SetCommand(DependencyObject d, ICommand? value) => d.SetValue(CommandProperty, value);

        private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Canvas canvas) return;

            if (e.OldValue != null)
            {
                canvas.MouseDown -= OnMouseDown;
                canvas.MouseMove -= OnMouseMove;
                canvas.MouseUp -= OnMouseUp;
            }

            if (e.NewValue != null)
            {
                canvas.MouseDown += OnMouseDown;
                canvas.MouseMove += OnMouseMove;
                canvas.MouseUp += OnMouseUp;
            }
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Canvas canvas || e.LeftButton != MouseButtonState.Pressed) return;

            canvas.CaptureMouse();
            UpdateFromMouse(canvas, e.GetPosition(canvas));
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not Canvas canvas || !canvas.IsMouseCaptured ||
                e.LeftButton != MouseButtonState.Pressed) return;

            UpdateFromMouse(canvas, e.GetPosition(canvas));
        }

        private static void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Canvas canvas && canvas.IsMouseCaptured)
            {
                canvas.ReleaseMouseCapture();
            }
        }

        /// <summary>像素坐标 → 归一化点：x → 饱和度（左白右纯色），y 翻转 → 明度（上亮下黑）。</summary>
        private static void UpdateFromMouse(Canvas canvas, Point pos)
        {
            var command = GetCommand(canvas);
            if (command == null) return;

            double w = canvas.ActualWidth > 0 ? canvas.ActualWidth : 440;
            double h = canvas.ActualHeight > 0 ? canvas.ActualHeight : 180;

            double x = Math.Max(0, Math.Min(w, pos.X));
            double y = Math.Max(0, Math.Min(h, pos.Y));

            var point = new SpectrumPoint(x / w, 1.0 - (y / h));
            if (command.CanExecute(point))
            {
                command.Execute(point);
            }
        }
    }
}
