using StarPie.Abstractions;
using StarPie.Events;

namespace StarPie.HostServices
{
    /// <summary>
    /// 宿主内部的最小插件上下文：插件唯一的宿主可达面（实现类型 internal，插件只拿到 SDK 接口）。
    /// </summary>
    /// <remarks>
    /// 宿主组合根与服务实现类型都不进入契约面；上下文按插件作用域创建，卸载随作用域一起作废。
    /// </remarks>
    internal sealed class PluginHostContext : IPluginContext
    {
        private readonly PluginServiceScope _scope;

        internal PluginHostContext(PluginServiceScope scope, IPluginLog log)
        {
            _scope = scope;
            Log = log;
        }

        /// <inheritdoc/>
        public string PluginId => _scope.PluginId;

        /// <inheritdoc/>
        public IPluginLog Log { get; }

        /// <inheritdoc/>
        public IPluginEvents Events => _scope.Events;

        /// <inheritdoc/>
        public void RegisterCapability<T>(T instance) where T : class
            => _scope.RegisterCapability(instance);
    }
}
