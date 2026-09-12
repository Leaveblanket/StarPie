using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Extensions
{
    /// <summary>托盘菜单内的一个插件菜单项：描述符 + 所属插件 id。</summary>
    /// <param name="PluginId">注册该菜单项的插件 id。</param>
    /// <param name="Descriptor">菜单项描述符（纯数据）。</param>
    public sealed record PluginMenuItem(
        string PluginId,
        PluginMenuItemDescriptor Descriptor);
}
