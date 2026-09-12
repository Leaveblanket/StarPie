using System;
using System.Collections.Generic;
using System.Linq;
using StarPie.Abstractions;
using StarPie.Events;
using StarPie.Manifest;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Registry;

namespace StarPie.HostServices
{
    /// <summary>
    /// 每插件一个的服务作用域：宿主服务的实例边界、句柄账本与释放前的前置条件。
    /// </summary>
    /// <remarks>
    /// 插件只经 <see cref="Context"/>（<see cref="IPluginContext"/> 面）取用宿主服务；服务实现类型
    /// 一律 internal，插件拿不到。订阅、回调、动作等每次注册都返回 <see cref="IDisposable"/> 并登记
    /// 进本作用域账本，卸载按插件强制枚举清理，不依赖插件自觉 Dispose。
    /// <see cref="Dispose"/> 幂等，且是 ALC 卸载的前置：释放后账本必为零、登记一律被拒。
    /// 能力实例活在本作用域内（宿主单例不缓存插件实例），释放时连同能力条目一并摘除。
    /// </remarks>
    public sealed class PluginServiceScope : IDisposable
    {
        private readonly object _sync = new();
        private readonly HashSet<ScopeHandle> _handles = new();
        private readonly Dictionary<Type, object> _capabilityInstances = new();
        private readonly PluginManifest _manifest;
        private readonly PluginLifecycleStateMachine _lifecycle;
        private readonly CapabilityRegistry _capabilityRegistry;
        private readonly PluginEvents _events;
        private bool _disposed;

        /// <summary>构造单插件作用域。</summary>
        /// <param name="manifest">插件清单（提供 id、priority 与能力声明）。</param>
        /// <param name="lifecycle">该插件的生命周期状态机（守卫读状态、熔断置隔离）。</param>
        /// <param name="capabilityRegistry">宿主能力表（注册与摘除条目）。</param>
        /// <param name="logSink">日志落点；缺省写 Debug。</param>
        /// <param name="guardOptions">守卫阈值（超时 / 熔断）；缺省 5 秒 / 3 次。</param>
        public PluginServiceScope(
            PluginManifest manifest,
            PluginLifecycleStateMachine lifecycle,
            CapabilityRegistry capabilityRegistry,
            IPluginLogSink? logSink = null,
            CapabilityGuardOptions? guardOptions = null)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Id);
            ArgumentNullException.ThrowIfNull(lifecycle);
            ArgumentNullException.ThrowIfNull(capabilityRegistry);

            _manifest = manifest;
            _lifecycle = lifecycle;
            _capabilityRegistry = capabilityRegistry;
            IPluginLogSink sink = logSink ?? DebugPluginLogSink.Instance;
            Guard = new CapabilityGuard(manifest.Id!, lifecycle, sink, guardOptions);
            _events = new PluginEvents(this, sink);
            Context = new PluginHostContext(this, new PluginLog(manifest.Id!, sink, () => IsDisposed));
        }

        /// <summary>插件 id（清单声明）。</summary>
        public string PluginId => _manifest.Id!;

        /// <summary>交给插件的上下文（插件唯一的宿主可达面）。</summary>
        public IPluginContext Context { get; }

        /// <summary>本插件的能力守卫（能力表构造守卫适配器时使用）。</summary>
        public CapabilityGuard Guard { get; }

        /// <summary>账本中仍挂着的句柄数（卸载前必须归零）。</summary>
        public int HandleCount
        {
            get
            {
                lock (_sync)
                {
                    return _handles.Count;
                }
            }
        }

        /// <summary>是否已释放（释放后不再受理登记与投递）。</summary>
        public bool IsDisposed
        {
            get
            {
                lock (_sync)
                {
                    return _disposed;
                }
            }
        }

        /// <summary>宿主中介事件面（插件只经 <see cref="Context"/> 取用）。</summary>
        internal IPluginEvents Events => _events;

        /// <summary>是否处于活动态（只有活动态允许执行插件代码：投递、取用与注册都据此判定）。</summary>
        internal bool IsActive => _lifecycle.Current == PluginLifecycleState.Active;

        /// <summary>清单声明的能力排序优先级。</summary>
        internal int ManifestPriority => _manifest.Priority;

        /// <summary>把一个句柄登记进账本；返回的句柄释放时会自动出账（幂等）。</summary>
        /// <param name="handle">待登记的句柄。</param>
        /// <exception cref="ObjectDisposedException">作用域已释放。</exception>
        public IDisposable RegisterHandle(IDisposable handle)
        {
            ArgumentNullException.ThrowIfNull(handle);
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(PluginServiceScope));
                }

                var registered = new ScopeHandle(this, handle);
                _handles.Add(registered);
                return registered;
            }
        }

        /// <summary>向本插件投递宿主中介事件；非活动态或已释放时静默丢弃（不得执行插件代码）。</summary>
        /// <typeparam name="TEvent">事件载体类型。</typeparam>
        /// <param name="pluginEvent">事件载体。</param>
        public void Publish<TEvent>(TEvent pluginEvent) where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(pluginEvent);
            if (IsDisposed || !IsActive)
            {
                return;
            }

            _events.Publish(pluginEvent);
        }

        /// <summary>
        /// 释放作用域：清理账本内全部句柄并摘除能力条目；幂等，是 ALC 卸载的前置。
        /// </summary>
        /// <exception cref="AggregateException">
        /// 有句柄在清理时抛异常；作用域仍已标记释放且账本已清零，异常交给卸载管线判隔离。
        /// </exception>
        public void Dispose()
        {
            ScopeHandle[] handles;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                handles = _handles.ToArray();
                _handles.Clear();
            }

            List<Exception>? failures = null;
            foreach (ScopeHandle handle in handles)
            {
                try
                {
                    handle.Dispose();
                }
                catch (Exception exception)
                {
                    (failures ??= new List<Exception>()).Add(exception);
                }
            }

            lock (_sync)
            {
                _capabilityInstances.Clear();
            }

            _events.Close();
            _capabilityRegistry.RemovePlugin(this);

            if (failures is { Count: > 0 })
            {
                throw new AggregateException(
                    $"插件 {PluginId} 的作用域释放时句柄清理失败",
                    failures);
            }
        }

        /// <summary>注册能力实例（插件经 <see cref="Context"/> 调用）。</summary>
        internal void RegisterCapability<T>(T instance) where T : class
        {
            ArgumentNullException.ThrowIfNull(instance);
            Type interfaceType = typeof(T);

            CapabilityContract contract = _capabilityRegistry.FindContract(interfaceType)
                ?? throw new InvalidOperationException($"宿主未声明能力契约：{interfaceType.FullName}");
            if (!IsDeclaredCapability(contract))
            {
                throw new InvalidOperationException(
                    $"清单未声明能力：{contract.Id} ABI {contract.Abi}");
            }

            PluginLifecycleState state = _lifecycle.Current;
            if (state is not (PluginLifecycleState.Starting or PluginLifecycleState.Active))
            {
                throw new InvalidOperationException(
                    $"插件 {PluginId} 处于 {state}，拒绝能力注册：{contract.Id}");
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(PluginServiceScope));
                }

                if (!_capabilityInstances.TryAdd(interfaceType, instance))
                {
                    throw new InvalidOperationException($"能力重复注册：{contract.Id}");
                }
            }

            _capabilityRegistry.RegisterPluginCapability(this, contract);
        }

        /// <summary>按契约取本作用域内的能力实例（能力表在消费者取用时调用；找不到即条目已摘除）。</summary>
        internal object? ResolveCapability(CapabilityContract contract)
        {
            lock (_sync)
            {
                return _capabilityInstances.TryGetValue(contract.InterfaceType, out object? instance)
                    ? instance
                    : null;
            }
        }

        /// <summary>
        /// 构造守卫适配器：适配器只持守卫与"每次调用现取实例"的解析器，不持插件实例。
        /// </summary>
        /// <param name="contract">能力契约。</param>
        /// <returns>适配器；作用域已释放或条目已摘除时为 null。</returns>
        internal object? CreateAdapter(CapabilityContract contract)
        {
            lock (_sync)
            {
                if (_disposed || !_capabilityInstances.ContainsKey(contract.InterfaceType))
                {
                    return null;
                }
            }

            return contract.CreateAdapter(Guard, ResolveRequiredCapability);

            object ResolveRequiredCapability()
                => ResolveCapability(contract)
                    ?? throw new CapabilityUnavailableException(
                        PluginId,
                        contract.Id,
                        _lifecycle.Current);
        }

        private bool IsDeclaredCapability(CapabilityContract contract)
            => _manifest.Capabilities?.Any(reference =>
                string.Equals(reference.Id, contract.Id, StringComparison.Ordinal)
                && reference.Abi == contract.Abi) == true;

        private void ReleaseHandle(ScopeHandle handle)
        {
            lock (_sync)
            {
                _handles.Remove(handle);
            }
        }

        /// <summary>账本句柄：出账后释放内层句柄；重复释放无副作用。</summary>
        private sealed class ScopeHandle : IDisposable
        {
            private readonly PluginServiceScope _scope;
            private readonly IDisposable _inner;
            private bool _disposed;

            internal ScopeHandle(PluginServiceScope scope, IDisposable inner)
            {
                _scope = scope;
                _inner = inner;
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                }

                _scope.ReleaseHandle(this);
                _inner.Dispose();
            }
        }
    }
}
