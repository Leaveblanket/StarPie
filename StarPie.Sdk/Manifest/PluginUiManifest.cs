namespace StarPie.Manifest
{
    /// <summary>
    /// 清单的 ui 段：插件含 UI 时声明 UI 侧 SDK ABI 与 <c>IPluginUiModule</c> 入口类型。
    /// </summary>
    public sealed class PluginUiManifest
    {
        /// <summary>UI 侧 SDK ABI 版本（"主.次"）；兼容判定归 Ui 侧插件托管层。</summary>
        public string? Sdk { get; set; }

        /// <summary>UI 入口类型全名。</summary>
        public string? EntryType { get; set; }
    }
}
