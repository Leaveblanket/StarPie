using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Extensions
{
    /// <summary>设置区内的一个插件区块：描述符 + 所属插件 id。</summary>
    /// <param name="PluginId">注册该区块的插件 id。</param>
    /// <param name="Descriptor">区块描述符（纯数据 + 工厂）。</param>
    public sealed record PluginSettingsSection(
        string PluginId,
        PluginSettingsSectionDescriptor Descriptor);
}
