namespace StarPie.Manifest
{
    /// <summary>
    /// plugin.json 的纯数据模型；解析与校验归宿主，SDK 不做 IO。
    /// </summary>
    /// <remarks>
    /// 宿主用宽松选项反序列化（大小写不敏感、允许注释与尾随逗号），因此可空字段一律按「可能缺失」
    /// 处理——语义规则由宿主校验层给出，本类型不做判定。
    /// </remarks>
    public sealed class PluginManifest
    {
        /// <summary>清单格式版本（宿主按自身支持的版本校验，不支持即拒绝装载）。</summary>
        public int SchemaVersion { get; set; }

        /// <summary>插件 id：反向域名、发布后不可变。</summary>
        public string? Id { get; set; }

        /// <summary>插件展示名。</summary>
        public string? Name { get; set; }

        /// <summary>插件版本（SemVer）。</summary>
        public string? Version { get; set; }

        /// <summary>headless SDK ABI 版本（"主.次"）。</summary>
        public string? Sdk { get; set; }

        /// <summary>UI 段：声明插件含 UI 时给出 UI 侧 ABI 与入口类型；缺省即 headless 插件。</summary>
        public PluginUiManifest? Ui { get; set; }

        /// <summary>入口程序集文件名（包内裸文件名，不含路径）。</summary>
        public string? EntryAssembly { get; set; }

        /// <summary>headless 入口类型全名。</summary>
        public string? EntryType { get; set; }

        /// <summary>
        /// 能力列表排序优先级（默认 0，数值小者靠前）。
        /// </summary>
        /// <remarks>只影响插件之间的顺序：内置条目永远最前，其后按 priority、plugin id 稳定序排列。</remarks>
        public int Priority { get; set; }

        /// <summary>能力声明：一个插件可声明多个能力，每条各自带 ABI。</summary>
        public List<PluginCapabilityReference>? Capabilities { get; set; }

        /// <summary>设置 schema 文件名（包内裸文件名）；宿主据此渲染通用设置表单。</summary>
        public string? SettingsSchema { get; set; }
    }
}
