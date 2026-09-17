using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace StarPie
{
    /// <summary>单实例重激活的窗口消息：置前实例经 <see cref="PInvoke.RegisterWindowMessage"/> 解析
    /// 同一消息 id 后投递给主框架，主框架 WndProc 收到后走 WPF 显示路径自恢复
    /// （ShowAndActivate）——纯外部 ShowWindow 不更新 WPF 的 IsVisible 状态，
    /// 隐藏到托盘的窗口恢复序列（导航重放/恢复信号）不会触发，故必须经本消息驱动。</summary>
    internal static class SingleInstanceRestore
    {
        /// <summary>进程内缓存的注册消息 id（<see cref="PInvoke.RegisterWindowMessage"/> 对同一字符串全机返回同值）。</summary>
        private static int? _messageId;

        /// <summary>单实例恢复消息 id（全机唯一；两端各自注册同一名称字符串）。
        /// 注册消息 id 落在 0xC000–0xFFFF，用 int 承载与 WPF 窗口消息同型。</summary>
        public static int MessageId => _messageId ??= (int)PInvoke.RegisterWindowMessage("StarPie_SingleInstance_Restore");

        /// <summary>向目标主框架窗口投递恢复消息（阻塞式；接收方在自身 UI 线程处理）。</summary>
        public static void Send(IntPtr mainWindow)
        {
            _ = PInvoke.SendMessage(new HWND(mainWindow), (uint)MessageId, default, default);
        }

        /// <summary>
        /// 让接收窗口放行本恢复消息的跨完整性级别投递。UIPI 只拦截更高完整性级别接收方的
        /// 窗口消息，而 <see cref="PInvoke.RegisterWindowMessage"/> 的消息值必大于 WM_USER，故提权实例
        /// 持有托盘窗口时，非提权实例的置前请求会被默认拦下（"双击图标没反应"）。
        /// 放行按窗口而非进程生效，且只涉及本进程自有的这一个注册消息——同一完整性级别下的
        /// 投递本就畅通，这里不改动其它消息。
        /// </summary>
        /// <returns>放行成功为 true；窗口无效或调用失败为 false（同级别投递不受影响）。</returns>
        public static bool AllowFromLowerIntegrity(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return PInvoke.ChangeWindowMessageFilterEx(
                    new HWND(window),
                    (uint)MessageId,
                    WINDOW_MESSAGE_FILTER_ACTION.MSGFLT_ALLOW);
            }
            catch
            {
                return false;
            }
        }
    }
}
