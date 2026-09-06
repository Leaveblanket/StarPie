using System;

namespace StarPie.Services
{
    /// <summary>
    /// 宿主回调委托包（ADR-0011/0016 决策 8，B6/#79 上提共享内核）：承载页面 VM 注册所需的
    /// 宿主回调——托盘气泡（<see cref="ShowTrayBalloonTip"/>）与退出（<see cref="ExitApplication"/>）。
    /// 原为 Composition.cs 内 internal 类；M5 拆集后模块注册器不能反向引用 Host，故上提为 Core
    /// 公开契约（Host 组合根注册单例并在 AppHost 构造后回填实现，见 host.md）。VM 注册时持
    /// 稳定转发委托，调用时读取当前回调——VM 不反向依赖宿主类。
    /// </summary>
    public sealed class AppHostDelegates
    {
        /// <summary>托盘气泡回调（Host AppHost 回填）；未回填时为 null（构造期惰性安全）。</summary>
        public Action<string, string>? ShowTrayBalloonTip { get; set; }

        /// <summary>退出回调（Host AppHost 回填）；未回填时为 null（构造期惰性安全）。</summary>
        public Action? ExitApplication { get; set; }
    }
}
