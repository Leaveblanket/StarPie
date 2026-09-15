using System;

namespace StarPie.Services
{
    /// <summary>
    /// 宿主回调委托包：承载页面 VM 所需的宿主动作回调——托盘气泡
    /// （<see cref="ShowTrayBalloonTip"/>）、退出（<see cref="ExitApplication"/>）与
    /// 立即提权重启（<see cref="RestartElevated"/>）。
    /// </summary>
    /// <remarks>
    /// 宿主（Host）组合根注册单例并在 <c>ShellHost</c> 构造后回填实现。VM 注册时持稳定转发委托，
    /// 调用时读取当前回调，不反向依赖宿主类。
    /// 托盘气泡与退出是**壳层的呈现与编排**：M3 起内置消费方已改由壳层直接呈现/执行
    ///（设置台页面 VM 是会话内的，关闭期间不存在），这两个属性保留为契约面。
    /// 本导出面在 1.0 之前可变：运行期按需提权重启的移除删掉了原先的提权重启成员（见 ADR-0042）；
    /// 「立即以管理员身份重启」以经任务的即时触发形态回归——同一件事在托盘与设置页共用一条触发路径
    ///（见 ADR-0043 与 ADR-0042 决策 2 的边界收窄）。
    /// </remarks>
    public sealed class AppHostDelegates
    {
        /// <summary>托盘气泡回调（ShellHost 回填）；未回填时为 null（构造期惰性安全）。</summary>
        public Action<string, string>? ShowTrayBalloonTip { get; set; }

        /// <summary>退出回调（ShellHost 回填）；未回填时为 null（构造期惰性安全）。</summary>
        public Action? ExitApplication { get; set; }

        /// <summary>
        /// 立即以管理员身份重启回调（ShellHost 回填）；未回填时为 null（构造期惰性安全）。
        /// 触发的是提权自启那颗计划任务（触发本身不弹 UAC），接管由提权新实例按既有判定完成
        /// ——应用内入口不开专用通道，与托盘菜单项是同一件事。
        /// </summary>
        public Action? RestartElevated { get; set; }
    }
}
