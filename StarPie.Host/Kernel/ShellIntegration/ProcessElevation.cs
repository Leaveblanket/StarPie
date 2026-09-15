using System;
using System.Runtime.Versioning;

namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 当前进程是否以管理员身份运行的探测。与 <c>AutostartRegistry</c> 同属无状态系统调用静态工具
    /// ——高级与系统页经委托注入消费（可测缝是 ViewModel 的注入委托，不是身份探测本身），
    /// 壳层直接消费以决定单实例恢复消息的跨完整性级别放行。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class ProcessElevation
    {
        /// <summary>当前进程是否以管理员身份运行；探测失败按未提权处理。</summary>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
