namespace WinPieGestures.Services
{
    /// <summary>
    /// 动作执行服务 (T15, ADR-0002)：启动程序 / 热键 / 系统命令三类动作的唯一执行入口。
    /// 手势执行端（GestureController）与设置窗口的动作试执行均经此注入调用；路由决策
    /// 在 <see cref="ActionRouting"/> 纯函数中，本实现只负责进程启动、键注入等系统调用。
    /// </summary>
    public interface IActionExecutorService
    {
        /// <summary>执行一个动作（同步）。空动作与未知类型按迁移前语义静默忽略。</summary>
        void Execute(ActionItem action);
    }
}
