using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace P0.Host;

/// <summary>
/// P0 打样：宿主侧 UI 载体。一切插件资产先入宿主容器/宿主属性，再由宿主清理。
/// 窗口放在屏幕外，避免打断使用者。
/// </summary>
internal static class Harness
{
    public static Window Window { get; private set; } = null!;

    /// <summary>视图 / DataTemplate 的宿主容器（对应 P3 的 PluginViewHost）。</summary>
    public static ContentControl ContentHost { get; private set; } = null!;

    /// <summary>Binding 的宿主目标（绑定必须挂在宿主对象上）。</summary>
    public static TextBlock BindingTarget { get; private set; } = null!;

    /// <summary>Animation 的宿主目标。</summary>
    public static Rectangle AnimationTarget { get; private set; } = null!;

    public static void Init()
    {
        ContentHost = new ContentControl();
        BindingTarget = new TextBlock();
        AnimationTarget = new Rectangle { Width = 40, Height = 12, Fill = Brushes.SteelBlue };

        Window = new Window
        {
            Title = "P0 harness",
            Width = 260,
            Height = 160,
            Left = -3000,
            Top = -3000,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.ToolWindow,
            Content = new StackPanel
            {
                Children = { ContentHost, BindingTarget, AnimationTarget }
            }
        };
        Window.Show();
        DoEvents(120);
    }

    public static void Teardown()
    {
        ClearPluginSurface();
        if (Window.IsVisible)
        {
            Window.Close();
        }
    }

    /// <summary>幂等：把宿主容器与宿主属性恢复到"无插件资产"状态。</summary>
    public static void ClearPluginSurface()
    {
        BindingOperations.ClearBinding(BindingTarget, TextBlock.TextProperty);
        BindingTarget.DataContext = null;
        BindingTarget.Text = string.Empty;

        AnimationTarget.BeginAnimation(UIElement.OpacityProperty, null);
        AnimationTarget.Opacity = 1.0;

        ContentHost.ContentTemplate = null;
        ContentHost.Content = null;
        ContentHost.DataContext = null;
    }

    /// <summary>跑消息泵 <paramref name="milliseconds"/> 毫秒，让布局/绑定/动画/模板真正执行。</summary>
    public static void DoEvents(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(milliseconds),
            DispatcherPriority.Background,
            (_, _) => frame.Continue = false,
            Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    /// <summary>等到条件成立或超时。</summary>
    public static bool WaitUntil(Func<bool> condition, int timeoutMs = 1500)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }

            DoEvents(25);
        }

        return condition();
    }
}
