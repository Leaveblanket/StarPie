namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 空审核清单：任何插件版本都不命中。
    /// </summary>
    public sealed class EmptyPluginReviewCatalog : IPluginReviewCatalog
    {
        /// <inheritdoc/>
        public bool IsReviewed(string pluginId, string version) => false;
    }
}
