using System;

namespace StarPie.Services
{
    /// <summary>
    /// 宿主回调委托包：承载页面 VM 注册所需的宿主回调——托盘气泡（<see cref="ShowTrayBalloonTip"/>）
    /// 与退出（<see cref="ExitApplication"/>）。
    /// </summary>
    /// <remarks>
    /// 宿主（Host）组合根注册单例并在 AppHost 构造后回填实现。VM 注册时持稳定转发委托，
    /// 调用时读取当前回调，不反向依赖宿主类。
    /// </remarks>
    public sealed class AppHostDelegates
    {
        /// <summary>托盘气泡回调（Host AppHost 回填）；未回填时为 null（构造期惰性安全）。</summary>
        public Action<string, string>? ShowTrayBalloonTip { get; set; }

        /// <summary>退出回调（Host AppHost 回填）；未回填时为 null（构造期惰性安全）。</summary>
        public Action? ExitApplication { get; set; }
    }
}
