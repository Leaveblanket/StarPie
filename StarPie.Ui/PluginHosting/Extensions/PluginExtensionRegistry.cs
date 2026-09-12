using System;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Extensions
{
    /// <summary>
    /// 固定扩展点托管：导航页与设置区注册（纯数据 + 工厂）；挂载与卸载编排由宿主在
    /// 对应扩展点执行（本类只登记与出账）。
    /// </summary>
    internal sealed class PluginExtensionRegistry
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;

        internal PluginExtensionRegistry(PluginUiAssetRegistry assets, string pluginId)
        {
            _assets = assets;
            _pluginId = pluginId;
        }

        /// <summary>登记导航页。</summary>
        internal IDisposable RegisterPage(PluginPageDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Page,
                    $"导航页 {descriptor.NavigationKey}",
                    () => true));
        }

        /// <summary>登记设置页区块。</summary>
        internal IDisposable RegisterSettingsSection(PluginSettingsSectionDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.SettingsSection,
                    $"设置区块 {descriptor.SectionKey}",
                    () => true));
        }
    }
}
