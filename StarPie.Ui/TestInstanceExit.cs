using System.Runtime.InteropServices;

namespace StarPie
{
    /// <summary>测试实例退出的窗口消息：测试运行器以此请求被测进程走真实退出路径
    /// （<see cref="ShellHost.ExitApplication"/> 的落盘 → 释放托盘 → 应用关闭），取代硬杀进程。</summary>
    /// <remarks>
    /// 硬杀（<c>TerminateProcess</c>）不执行用户态收尾，托盘图标不会有
    /// <c>Shell_NotifyIcon(NIM_DELETE)</c>，shell 的通知区会留下宿主窗口已失效的死条目
    /// （幽灵托盘图标），要等通知区收到鼠标输入才被摘除。注册窗口消息全机可投递，故常驻壳层
    /// 只对命令行声明了测试实例的进程受理（与单实例闸门的绕过口径同一标记）。
    /// </remarks>
    internal static class TestInstanceExit
    {
        /// <summary>进程内缓存的注册消息 id（RegisterWindowMessage 对同一字符串全机返回同值）。</summary>
        private static int? _messageId;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int RegisterWindowMessage(string messageName);

        /// <summary>测试实例退出消息 id（全机唯一；测试侧按同一名称字符串解析后投递）。</summary>
        public static int MessageId => _messageId ??= RegisterWindowMessage("StarPie_TestInstance_Exit");
    }
}
