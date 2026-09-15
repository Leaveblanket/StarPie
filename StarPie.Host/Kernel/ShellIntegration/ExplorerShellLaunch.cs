using System;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 经 Explorer 中介的降权启动：把"拉起新进程"交给已在运行的 explorer.exe 代劳，
    /// 子进程因而落在 Explorer 的（非提权）完整性级别上。提权宿主直接 <c>Process.Start</c>
    /// 会让子进程继承管理员令牌——资源管理器往其窗口拖文件失败、映射网络驱动器不可见、
    /// Chromium 系应用拒绝以管理员启动（见 [ADR-0040] 决策 6）。
    /// </summary>
    /// <remarks>
    /// 机制取自微软官方示例 Execute In Explorer：从**已打开的**资源管理器窗口取它的
    /// <c>IShellDispatch2</c>（<c>Document.Application</c>）再调 <c>ShellExecute</c>。该接口带
    /// Args 与 Directory 参数，故参数不丢——"explorer 中转必丢参数"只成立于裸命令行
    /// <c>explorer.exe "路径"</c> 那种写法，本类不走那条路。自建降权令牌是另一条被明确否决的路
    /// （Raymond Chen：很难把令牌的提权性质正确地剥掉）。
    /// 没有可用的资源管理器窗口（如 explorer.exe 已结束）或调用失败一律返回 false，
    /// 由调用方回退直接启动——本类是尽力而为的一跳，不是启动的必经路径。
    /// 与 <see cref="ProcessElevation"/> 同属无状态系统调用静态工具，经委托注入消费
    /// （可测缝是消费方的注入委托，不是 COM 调用本身）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class ExplorerShellLaunch
    {
        /// <summary>
        /// 请 Explorer 以自身权限启动目标程序。成功返回 true；无可用 Explorer 窗口、
        /// 或 COM 调用抛错时返回 false（静默降级，不抛异常、不弹窗）。
        /// </summary>
        public static bool TryShellExecute(string fileName, string arguments, string workingDirectory)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            try
            {
                Type? shellWindowsType = Type.GetTypeFromProgID("ShellWindows");
                if (shellWindowsType == null) return false;

                dynamic shellWindows = Activator.CreateInstance(shellWindowsType)!;
                int count = shellWindows.Count;
                for (int i = 0; i < count; i++)
                {
                    dynamic window = shellWindows.Item(i);
                    dynamic? application = window?.Document?.Application;
                    if (application == null) continue;

                    application.ShellExecute(fileName, arguments, workingDirectory, "open", 1);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Explorer-mediated launch failed: {ex.Message}");
            }

            return false;
        }
    }
}
