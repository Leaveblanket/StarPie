using System.Collections.Generic;
using StarPie.Services.Icons;

namespace StarPie.Services.Programs
{
    /// <summary>
    /// 已安装程序扫描契约（P1.4/#113 起驻 <c>StarPie.Sdk.Wpf</c>；ADR-0023/#96 随实现方 M3 下沉）：聚合八个来源——系统自带工具、
    /// 开始菜单/桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths 与 Uninstall、
    /// Program Files 顶层——返回去重排序后的候选程序列表。
    /// </summary>
    /// <remarks>
    /// ADR-0020/#88：契约化替代组合根委托注入 M3 静态扫描的接缝（S21 归零）；ADR-0023/#96
    /// 契约随实现方 M3 下沉（自 Core 迁出），P1.4/#113 随 SDK 的 WPF 面收口迁入 Sdk.Wpf。图标补全与 .lnk 解析由实现经注入的
    /// 契约完成，消费方（如对话框服务）只依赖本接口的无参扫描入口。
    /// </remarks>
    public interface IProgramScanner
    {
        /// <summary>扫描全部来源，按显示名排序返回去重后的候选程序（图标与 .lnk
        /// 解析在实现内部经注入的契约完成）。</summary>
        IReadOnlyList<ProgramEntry> ScanInstalledPrograms();
    }
}
