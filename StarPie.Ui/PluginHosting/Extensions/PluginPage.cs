using System;

namespace StarPie.PluginHosting.Extensions
{
    /// <summary>导航区内的一个插件页：宿主签发的目录标识 + 页面标题键、图标与 VM 工厂。</summary>
    /// <param name="PluginId">注册该页的插件 id。</param>
    /// <param name="AutomationId">侧边栏 UIA AutomationId（宿主签发，格式 <c>NavPlugin_&lt;插件 id&gt;</c>）。</param>
    /// <param name="TitleKey">页面标题的文案键。</param>
    /// <param name="IconData">侧边栏图标数据（几何路径串）。</param>
    /// <param name="ViewModelFactory">页面 VM 工厂；宿主在导航时调用。</param>
    public sealed record PluginPage(
        string PluginId,
        string AutomationId,
        string TitleKey,
        string IconData,
        Func<object> ViewModelFactory);
}
