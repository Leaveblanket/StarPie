using System.Windows;
using System.Windows.Threading;

namespace StarPie.Ui.Services.Shell
{
    /// <summary>
    /// 瞬态窗口收尾纪律的唯一实现：清窗口动画 → <c>Close()</c> → 排空 Dispatcher →
    /// 处理 <see cref="Application.MainWindow"/> → （强引用由调用方丢弃）。
    /// </summary>
    /// <remarks>
    /// 收尾步骤集中在此处，设置台、轮盘、对话框与插件窗口共用同一实现，不各自复制：
    /// ① 清窗口动画 ② 丢弃内容与 DataContext ③ <c>Close()</c> ④ 排空 Dispatcher
    /// ⑤ 处理 <see cref="Application.MainWindow"/> ⑥ 调用方丢弃强引用。
    /// 第 ④ 步不可省：<c>Close()</c> 之后仍有布局/渲染清理载荷排在队列上，它们持有刚关闭的窗口；
    /// 不排空则该窗口及其视图树滞留。第 ② 步保护的是"窗口对象本身被 WPF 滞留"的情形
    ///（运行时行为，弱引用判定可复现）：滞留方只应挂住一个空窗口，不得顺带挂住整棵视图树与 VM 树。
    /// <see cref="Application.MainWindow"/> 的处理用显式回退窗口表达：仅当该窗口当前
    /// 就是主窗口时才改写，避免把瞬态窗口留在主窗口属性上——被主窗口属性钉住的对象
    /// 不可回收，且"已关闭窗口仍是主窗口"会让依赖该属性的消费方读到死窗口。
    /// </remarks>
    public static class TransientWindowTeardown
    {
        /// <summary>
        /// 关闭一个瞬态窗口并完成收尾；调用方随后丢弃自己的强引用。
        /// </summary>
        /// <param name="window">待收尾的窗口（非 null）。</param>
        /// <param name="mainWindowFallback">
        /// 该窗口当前是主窗口时的回退目标（常驻锚窗口）；不需要回退时传 null。
        /// </param>
        /// <param name="alreadyClosed">
        /// 窗口已在外层关窗流程中关闭（如从 <c>Closed</c> 事件收敛回来）：跳过 <c>Close()</c> 与
        /// Dispatcher 排空——在关窗处理中重入 Dispatcher 会嵌套派发，而此时窗口已在关闭路径上。
        /// </param>
        public static void Complete(Window window, Window? mainWindowFallback = null, bool alreadyClosed = false)
        {
            ArgumentNullException.ThrowIfNull(window);

            // 1. 清窗口动画：淡出等动画的时钟会持有窗口，关闭前不清则窗口被动画滞留。
            window.BeginAnimation(UIElement.OpacityProperty, null);

            // 2. 丢弃窗口内容与 DataContext：WPF 会长期强引用已关闭的窗口对象本身
            //（运行时行为；弱引用判定可复现，见测试类 remarks），被滞留的窗口若仍持内容，
            // 就会顺带钉住整棵视图树与挂在 DataContext 上的 VM 树。
            window.DataContext = null;
            if (window.Content is not null)
            {
                window.Content = null;
            }

            // 3. 关闭并从全局窗口集合出账。
            if (!alreadyClosed)
            {
                window.Close();

                // 4. 排空 Dispatcher 上遗留的窗口清理载荷（它们同样持有刚关闭的窗口）。
                DrainDispatcher(window.Dispatcher);
            }

            // 5. Application.MainWindow 的处理：WPF 在主窗口被关闭时会自行把该属性清成 null，
            // 故回退判定同时接受"已被清空"与"仍指向本窗口"两种状态——两种都必须落到回退目标
            // （常驻锚窗口）上，否则常驻期主窗口为空，依赖该属性的消费方读不到活动窗口。
            if (Application.Current is { } application
                && mainWindowFallback is not null
                && (application.MainWindow is null || ReferenceEquals(application.MainWindow, window)))
            {
                application.MainWindow = mainWindowFallback;
            }
        }

        /// <summary>
        /// 排空 Dispatcher 上遗留的窗口清理载荷——它们持有刚关闭的窗口，不排空则窗口滞留。
        /// 排空点是 <see cref="DispatcherPriority.ApplicationIdle"/>：`Invoke` 本身作为该优先级的
        /// 载荷入队，必然在所有更高优先级（Loaded/Input/Background/Render）的清理载荷之后执行；
        /// 已显示窗口的句柄销毁与布局清理排在 Input/Loaded 档，只排 Render 会让窗口留在队列上。
        /// 关停中不再排空：Dispatcher 已进入关停就不会再处理载荷。
        /// </summary>
        private static void DrainDispatcher(Dispatcher dispatcher)
        {
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return;
            }

            dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }
}
