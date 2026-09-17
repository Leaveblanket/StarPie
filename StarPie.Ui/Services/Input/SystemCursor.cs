using System.Drawing;
using StarPie.Models;
using Windows.Win32;

namespace StarPie.Services.Input
{
    /// <summary>
    /// 系统光标位置的只读探针：看门狗的系统调用接缝（可注入假体），读不到时返回 null，
    /// 判定侧据此放弃本次探测而不是误判钩子死亡。
    /// </summary>
    internal static class SystemCursor
    {
        /// <summary>读取当前光标屏幕坐标（物理像素）；调用失败返回 null。</summary>
        public static GesturePoint? TryGetPosition()
            => PInvoke.GetCursorPos(out Point point)
                ? new GesturePoint(point.X, point.Y)
                : null;
    }
}
