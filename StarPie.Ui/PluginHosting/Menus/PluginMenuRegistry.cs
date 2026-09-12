using System;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Menus
{
    /// <summary>菜单托管：登记插件菜单项（纯数据），卸载时从登记表出账。</summary>
    internal sealed class PluginMenuRegistry
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;

        internal PluginMenuRegistry(PluginUiAssetRegistry assets, string pluginId)
        {
            _assets = assets;
            _pluginId = pluginId;
        }

        internal IDisposable Register(PluginMenuItemDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.MenuItem,
                    $"菜单项 {descriptor.ItemKey}",
                    () => true));
        }
    }
}
