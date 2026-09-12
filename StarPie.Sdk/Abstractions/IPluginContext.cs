namespace StarPie.Abstractions
{
    /// <summary>
    /// 宿主交给插件的运行上下文：插件的全部宿主服务都从本对象取用。
    /// </summary>
    /// <remarks>
    /// 宿主不向插件暴露组合根或服务定位器；插件经 <see cref="IPlugin"/> 入口拿到的上下文
    /// 即其可达面，不得自行缓存宿主对象。
    /// </remarks>
    public interface IPluginContext
    {
        /// <summary>当前插件 id：清单声明的反向域名，与宿主状态条目、数据目录键一致。</summary>
        string PluginId { get; }
    }
}
