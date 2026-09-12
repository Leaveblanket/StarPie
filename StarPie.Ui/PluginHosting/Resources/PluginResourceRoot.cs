using System;
using System.Windows;

namespace StarPie.PluginHosting.Resources
{
    /// <summary>
    /// 每插件一个资源根容器：插件的资源字典全部并入本容器，卸载时宿主整根摘除。
    /// </summary>
    /// <remarks>
    /// 资源不逐条合并进 <c>Application.Current.Resources</c>，避免"插件资源散落在宿主资源里无法定位"；
    /// DataTemplate/Style/Theme 随根容器一次摘净。
    /// </remarks>
    public sealed class PluginResourceRoot
    {
        internal PluginResourceRoot(string pluginId)
        {
            PluginId = pluginId;
        }

        /// <summary>所属插件 id。</summary>
        public string PluginId { get; }

        /// <summary>插件资源根容器本身（宿主在挂载时并入宿主资源合并表）。</summary>
        public ResourceDictionary Dictionary { get; } = new();

        /// <summary>根容器内已并入的资源字典数。</summary>
        public int MergedCount => Dictionary.MergedDictionaries.Count;

        /// <summary>把资源根并入宿主资源合并表并登记；返回摘除句柄（幂等）。</summary>
        /// <param name="hostResources">宿主资源（通常是 Application.Resources）。</param>
        /// <param name="assets">插件资产登记表。</param>
        internal IDisposable AttachTo(ResourceDictionary hostResources, PluginUiAssetRegistry assets)
        {
            ArgumentNullException.ThrowIfNull(hostResources);
            ArgumentNullException.ThrowIfNull(assets);

            if (!hostResources.MergedDictionaries.Contains(Dictionary))
            {
                hostResources.MergedDictionaries.Add(Dictionary);
            }

            return new PluginUiAssetHandle(
                assets,
                assets.Track(
                    PluginId,
                    PluginUiAssetKind.ResourceRoot,
                    $"资源根 {PluginId}",
                    () => hostResources.MergedDictionaries.Remove(Dictionary)));
        }

        /// <summary>并入一个插件资源字典并登记；返回摘除句柄。</summary>
        /// <param name="packUri">插件包内资源字典的 pack URI。</param>
        /// <param name="assets">插件资产登记表。</param>
        internal IDisposable Merge(Uri packUri, PluginUiAssetRegistry assets)
        {
            ArgumentNullException.ThrowIfNull(packUri);
            ArgumentNullException.ThrowIfNull(assets);

            var dictionary = new ResourceDictionary { Source = packUri };
            Dictionary.MergedDictionaries.Add(dictionary);
            return new PluginUiAssetHandle(
                assets,
                assets.Track(
                    PluginId,
                    PluginUiAssetKind.ResourceRoot,
                    $"资源字典 {packUri}",
                    () => Dictionary.MergedDictionaries.Remove(dictionary)));
        }
    }
}
