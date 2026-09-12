using StarPie.Events;

namespace StarPie.Abstractions
{
    /// <summary>
    /// 宿主交给插件的运行上下文：插件的全部宿主服务都从本对象取用。
    /// </summary>
    /// <remarks>
    /// 宿主不向插件暴露组合根或服务定位器；插件经 <see cref="IPlugin"/> 入口拿到的上下文
    /// 即其可达面，不得自行缓存宿主对象。上下文按插件作用域隔离：日志、事件与能力注册
    /// 都落在该插件自己的服务作用域内，卸载时随作用域一次性释放。
    /// </remarks>
    public interface IPluginContext
    {
        /// <summary>当前插件 id：清单声明的反向域名，与宿主状态条目、数据目录键一致。</summary>
        string PluginId { get; }

        /// <summary>宿主日志面（自动带 plugin id）。</summary>
        IPluginLog Log { get; }

        /// <summary>宿主中介事件面（订阅句柄进本插件作用域账本）。</summary>
        IPluginEvents Events { get; }

        /// <summary>
        /// 在 <see cref="IPlugin.StartAsync"/> 内注册能力实例：实例活在本插件的服务作用域内，
        /// 消费者经宿主守卫短租用。
        /// </summary>
        /// <typeparam name="T">
        /// 能力接口类型；宿主必须已声明对应能力契约，且清单已声明该能力，否则注册失败。
        /// </typeparam>
        /// <param name="instance">能力实现实例（非 null）。</param>
        void RegisterCapability<T>(T instance) where T : class;
    }
}
