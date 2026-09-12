using System.Collections.Generic;
using StarPie.Services.Icons;

namespace StarPie.Services.Programs
{
    /// <summary>
    /// 已安装程序扫描契约（能力 <c>program-source@1</c>）：返回一份可用程序来源的候选列表，
    /// 多来源的合并由宿主侧的来源聚合完成。
    /// </summary>
    /// <remarks>
    /// 实现分两类：宿主内置来源（<c>StarPie.Host</c> 的程序扫描件，随宿主分发）与插件来源
    /// （随包插件的程序扫描实现，停用插件即整体退出）。消费者拿到的条目不分来源、只按路径去重，
    /// 内置来源永远在前；插件缺席或停用是正常降级。图标补全不在此面（返回纯数据条目，
    /// 由 UI 消费方按路径装配图标）。
    /// </remarks>
    public interface IProgramScanner
    {
        /// <summary>扫描全部来源，按显示名排序返回去重后的候选程序（图标与 .lnk
        /// 解析在实现内部经注入的契约完成）。</summary>
        IReadOnlyList<ProgramEntry> ScanInstalledPrograms();
    }
}
