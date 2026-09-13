using System.Runtime.InteropServices;

namespace StarPie
{
    /// <summary>单实例重激活的窗口消息：置前实例经 <see cref="RegisterWindowMessage"/> 解析
    /// 同一消息 id 后投递给主框架，主框架 WndProc 收到后走 WPF 显示路径自恢复
    /// （ShowAndActivate）——纯外部 ShowWindow 不更新 WPF 的 IsVisible 状态，
    /// 隐藏到托盘的窗口恢复序列（导航重放/恢复信号）不会触发，故必须经本消息驱动。</summary>
    internal static class SingleInstanceRestore
    {
        /// <summary>进程内缓存的注册消息 id（RegisterWindowMessage 对同一字符串全机返回同值）。</summary>
        private static int? _messageId;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int RegisterWindowMessage(string messageName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>单实例恢复消息 id（全机唯一；两端各自注册同一名称字符串）。</summary>
        public static int MessageId => _messageId ??= RegisterWindowMessage("StarPie_SingleInstance_Restore");

        /// <summary>向目标主框架窗口投递恢复消息（阻塞式；接收方在自身 UI 线程处理）。</summary>
        public static void Send(IntPtr mainWindow)
        {
            _ = SendMessage(mainWindow, MessageId, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
