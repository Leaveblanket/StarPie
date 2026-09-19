using System.Windows;

namespace StarPie.Ui.Views.Navigation
{
    /// <summary>
    /// 常驻锚窗口：永不显示的隐藏窗口，长期持有 <see cref="Application.MainWindow"/>。
    /// </summary>
    /// <remarks>
    /// <see cref="Application.MainWindow"/> 会被自动设为进程中第一个实例化的 <see cref="Window"/>
    /// 并长期持强引用，官方未明确"置空后是否被后续窗口重新赋值"。设置台、轮盘与对话框都是
    /// 瞬态窗口：一旦某个瞬态窗口成为首窗，它就会永远不可回收。锚窗口在任何其它窗口之前实例化
    /// 并显式占住该属性，使瞬态窗口不可能被自动赋值钉住——该做法不依赖自动赋值条件的精确语义。
    /// 本窗口不 Show（无 HWND、不进任务栏、不参与视觉树），只在
    /// <see cref="Application.Current"/> 的窗口集合中占位。
    /// </remarks>
    public sealed class AnchorWindow : Window
    {
        public AnchorWindow()
        {
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Width = 1;
            Height = 1;
            Visibility = Visibility.Hidden;
        }
    }
}
