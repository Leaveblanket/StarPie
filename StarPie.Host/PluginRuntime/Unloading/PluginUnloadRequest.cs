using System;
using System.Linq;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Loading;

namespace StarPie.PluginRuntime.Unloading
{
    /// <summary>
    /// 卸载请求：从装载结果交接而来的插件对象所有权（入口实例、ALC、服务作用域）。
    /// </summary>
    /// <remarks>
    /// 回收判定要求插件对象在 GC 时确实无强引用，因此卸载管线在交出结束前会清空本对象持有的
    /// 强引用（<see cref="ReleaseLoadedPlugin"/>）；<see cref="FromLoaded"/> 是插件对象所有权的唯一
    /// 转移点，交接时即清空装载结果持有的入口实例、ALC 与作用域，调用方之后无从再经装载结果持有插件对象。
    /// </remarks>
    public sealed class PluginUnloadRequest
    {
        private IPlugin? _plugin;
        private PluginLoadContext? _loadContext;
        private PluginServiceScope? _scope;

        private PluginUnloadRequest(
            string pluginId,
            IPlugin? plugin,
            PluginLoadContext loadContext,
            PluginServiceScope? scope,
            PluginLifecycleStateMachine lifecycle)
        {
            PluginId = pluginId;
            _plugin = plugin;
            _loadContext = loadContext;
            _scope = scope;
            Lifecycle = lifecycle;
            HasScope = scope is not null;
            PluginProbe = new WeakReference(plugin);
            LoadContextProbe = new WeakReference(loadContext);
            // 未启动成功的隔离结果没有入口实例，回落到 ALC 内已加载的入口程序集；
            // 连程序集都没加载起来（例如入口程序集损坏）时探针为空，判定按"已回收"计。
            EntryAssemblyProbe = new WeakReference(
                plugin?.GetType().Assembly ?? loadContext.Assemblies.FirstOrDefault());
        }

        /// <summary>插件 id。</summary>
        public string PluginId { get; }

        /// <summary>该插件的生命周期状态机。</summary>
        internal PluginLifecycleStateMachine Lifecycle { get; }

        /// <summary>
        /// 请求是否携带服务作用域（活动态装载结果为真，启动失败的隔离装载结果为假）。
        /// 判定必须是构造期算好的布尔值：卸载帧直接读实例字段会把引用留在栈槽里，
        /// 回收判定时被 GC 当作 root（headless 硬判据要求插件对象全部死亡）。
        /// </summary>
        internal bool HasScope { get; }

        /// <summary>入口实例（交接期间有效；仅活动态装载结果的请求可用）。</summary>
        internal IPlugin Plugin => _plugin
            ?? throw new InvalidOperationException("卸载请求已交出插件引用");

        /// <summary>服务作用域（交接期间有效；仅活动态装载结果的请求可用）。</summary>
        internal PluginServiceScope Scope => _scope
            ?? throw new InvalidOperationException("卸载请求已交出插件引用");

        /// <summary>入口实例弱引用（回收判定用，不 root 插件）。</summary>
        internal WeakReference PluginProbe { get; }

        /// <summary>ALC 弱引用（回收判定用）。</summary>
        internal WeakReference LoadContextProbe { get; }

        /// <summary>入口程序集弱引用（回收判定用）。</summary>
        internal WeakReference EntryAssemblyProbe { get; }

        /// <summary>从活动态装载结果构造卸载请求；交接后装载结果的插件对象引用被清空。</summary>
        /// <param name="loaded">装载结果（必须为活动态且带插件实例、ALC 与作用域）。</param>
        /// <exception cref="ArgumentException">装载结果不是活动态或缺必要部件。</exception>
        public static PluginUnloadRequest FromLoaded(PluginLoadResult loaded)
        {
            ArgumentNullException.ThrowIfNull(loaded);
            if (loaded.Status != PluginLoadStatus.Active
                || loaded.Plugin is null
                || loaded.LoadContext is null
                || loaded.Scope is null)
            {
                throw new ArgumentException(
                    $"卸载请求要求活动态装载结果（含实例、ALC 与作用域）：{loaded.Status}",
                    nameof(loaded));
            }

            var request = new PluginUnloadRequest(
                loaded.PluginId,
                loaded.Plugin,
                loaded.LoadContext,
                loaded.Scope,
                loaded.Lifecycle);
            loaded.ReleaseForUnload();
            return request;
        }

        /// <summary>
        /// 从隔离装载结果构造回收请求：插件没启动成功，没有实例与作用域可清，
        /// 只回收失败装载留下的 ALC 与已加载程序集。
        /// </summary>
        /// <param name="loaded">隔离的装载结果（拒绝路径没有 ALC，不接受）。</param>
        /// <exception cref="ArgumentException">装载结果不是带 ALC 的隔离结论。</exception>
        public static PluginUnloadRequest FromQuarantined(PluginLoadResult loaded)
        {
            ArgumentNullException.ThrowIfNull(loaded);
            if (loaded.Status != PluginLoadStatus.Quarantined || loaded.LoadContext is null)
            {
                throw new ArgumentException(
                    $"回收请求要求带 ALC 的隔离装载结果：{loaded.Status}",
                    nameof(loaded));
            }

            var request = new PluginUnloadRequest(
                loaded.PluginId,
                loaded.Plugin,
                loaded.LoadContext,
                loaded.Scope,
                loaded.Lifecycle);
            loaded.ReleaseForUnload();
            return request;
        }

        /// <summary>取出 ALC 并清空请求内的该引用（调用方负责 Unload 并丢弃本地引用）。</summary>
        internal PluginLoadContext TakeLoadContext()
        {
            PluginLoadContext loadContext = _loadContext
                ?? throw new InvalidOperationException("卸载请求已交出插件引用");
            _loadContext = null;
            return loadContext;
        }

        /// <summary>
        /// 清空请求持有的入口实例与服务作用域引用（回收判定前调用）；ALC 另行经
        /// <see cref="TakeLoadContext"/> 交到卸载帧，避免在判定期间被栈/字段无意 root。
        /// </summary>
        internal void ReleaseLoadedPlugin()
        {
            _plugin = null;
            _scope = null;
        }
    }
}
