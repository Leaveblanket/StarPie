namespace StarPie.PluginRuntime.State
{
    /// <summary>
    /// 宿主插件状态文档：开发者模式开关 + 按插件 id 索引的条目表。
    /// </summary>
    public sealed class PluginStateDocument
    {
        /// <summary>状态文件格式版本。</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>开发者模式开关（默认关闭）；开启时对未命中清单的插件按开发者模式准入。</summary>
        public bool DeveloperModeEnabled { get; set; }

        /// <summary>插件状态条目（键为插件 id）。</summary>
        public Dictionary<string, PluginStateEntry> Plugins { get; set; } = new(StringComparer.Ordinal);

        /// <summary>取该 id 的状态条目；不存在时创建一条默认启用、尚未准入的条目。</summary>
        public PluginStateEntry GetOrCreate(string pluginId)
        {
            if (!Plugins.TryGetValue(pluginId, out PluginStateEntry? entry))
            {
                entry = new PluginStateEntry();
                Plugins[pluginId] = entry;
            }

            return entry;
        }
    }
}
