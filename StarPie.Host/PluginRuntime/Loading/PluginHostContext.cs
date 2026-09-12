using StarPie.Abstractions;

namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 宿主内部的最小插件上下文：把清单 id 交给插件。
    /// </summary>
    /// <remarks>插件可见的其它宿主服务在此对象上扩展，宿主组合根与服务实现类型不进入契约面。</remarks>
    internal sealed class PluginHostContext : IPluginContext
    {
        internal PluginHostContext(string pluginId)
        {
            PluginId = pluginId;
        }

        public string PluginId { get; }
    }
}
