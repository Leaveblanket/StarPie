using System;

namespace StarPie.Events
{
    /// <summary>
    /// 宿主中介事件面：插件订阅宿主公开的事件，宿主登记订阅句柄并在卸载时强制断开。
    /// </summary>
    /// <remarks>
    /// 首期不支持插件之间互发事件：事件由宿主发布，插件只能订阅。订阅返回的
    /// <see cref="IDisposable"/> 进该插件的服务作用域账本，插件不自行释放也不会残留。
    /// </remarks>
    public interface IPluginEvents
    {
        /// <summary>订阅宿主事件；返回的句柄用于退订（幂等）。</summary>
        /// <typeparam name="TEvent">事件载体类型（宿主公开的事件契约）。</typeparam>
        /// <param name="handler">事件处理器。</param>
        /// <returns>退订句柄；随作用域释放强制清理。</returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
    }
}
