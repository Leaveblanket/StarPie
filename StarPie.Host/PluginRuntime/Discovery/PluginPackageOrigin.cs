namespace StarPie.PluginRuntime.Discovery
{
    /// <summary>插件包的发现来源目录。</summary>
    public enum PluginPackageOrigin
    {
        /// <summary>安装目录（随宿主分发；发现顺序在前）。</summary>
        Install,

        /// <summary>用户目录（发现顺序在后）。</summary>
        User,
    }
}
