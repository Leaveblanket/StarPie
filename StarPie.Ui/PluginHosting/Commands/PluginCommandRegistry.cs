using System;
using System.Collections.Generic;
using System.Linq;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Commands
{
    /// <summary>命令托管：登记插件命令（含可选举措），按命令 id 供宿主调用；卸载注销时从登记表出账。</summary>
    internal sealed class PluginCommandRegistry
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;
        private readonly List<PluginCommandDescriptor> _commands = new();

        internal PluginCommandRegistry(PluginUiAssetRegistry assets, string pluginId)
        {
            _assets = assets;
            _pluginId = pluginId;
        }

        internal IDisposable Register(PluginCommandDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (string.IsNullOrWhiteSpace(descriptor.CommandId))
            {
                throw new ArgumentException("命令 id 不能为空", nameof(descriptor));
            }

            _commands.Add(descriptor);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Command,
                    $"命令 {descriptor.CommandId}",
                    () =>
                    {
                        _commands.Remove(descriptor);
                        return true;
                    }));
        }

        /// <summary>按命令 id 取已登记命令；未登记时为 null。</summary>
        internal PluginCommandDescriptor? Find(string commandId)
            => _commands.FirstOrDefault(
                descriptor => string.Equals(descriptor.CommandId, commandId, StringComparison.Ordinal));
    }
}
