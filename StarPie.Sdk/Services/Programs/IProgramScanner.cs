using System.Collections.Generic;
using StarPie.Services.Icons;

namespace StarPie.Services.Programs
{
    /// <summary>
    /// 已安装程序扫描契约：聚合八个来源——系统自带工具、开始菜单/桌面快捷方式、
    /// 用户 AppData、WindowsApps、注册表 App Paths 与 Uninstall、Program Files 顶层——
    /// 返回去重排序后的候选程序列表。
    /// </summary>
    /// <remarks>
    /// 实现驻 <c>StarPie.Host</c> 的程序扫描件；图标补全不在此面（返回纯数据条目，
    /// 由 UI 消费方按路径装配图标）。
    /// </remarks>
    public interface IProgramScanner
    {
        /// <summary>扫描全部来源，按显示名排序返回去重后的候选程序（图标与 .lnk
        /// 解析在实现内部经注入的契约完成）。</summary>
        IReadOnlyList<ProgramEntry> ScanInstalledPrograms();
    }
}
