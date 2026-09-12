using System;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Commands
{
    /// <summary>命令托管：登记插件命令（含可选举措），卸载注销时从登记表出账。</summary>
    internal sealed class PluginCommandRegistry
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;

        internal PluginCommandRegistry(PluginUiAssetRegistry assets, string pluginId)
        {
            _assets = assets;
            _pluginId = pluginId;
        }

        internal IDisposable Register(PluginCommandDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Command,
                    $"命令 {descriptor.CommandId}",
                    () => true));
        }
    }
}
