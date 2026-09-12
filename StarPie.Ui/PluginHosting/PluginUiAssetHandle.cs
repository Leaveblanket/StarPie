using System;

namespace StarPie.PluginHosting
{
    /// <summary>插件侧注销句柄：Dispose 即执行摘除动作并从登记表出账（幂等）。</summary>
    internal sealed class PluginUiAssetHandle : IDisposable
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly PluginUiAsset _asset;
        private bool _disposed;

        internal PluginUiAssetHandle(PluginUiAssetRegistry assets, PluginUiAsset asset)
        {
            _assets = assets;
            _asset = asset;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_asset.TryDetach())
            {
                _assets.Forget(_asset);
            }
        }
    }
}
