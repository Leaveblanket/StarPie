using System;
using System.Collections.Generic;
using StarPie.Abstractions;
using StarPie.Events;

namespace StarPie.HostServices
{
    /// <summary>
    /// 单插件事件中介实现（插件只看到 <see cref="IPluginEvents"/>）：订阅句柄进作用域账本，
    /// 退订与作用域释放都强制摘除；处理器异常在写入边界转成宿主 DTO，不打断宿主发布路径。
    /// </summary>
    /// <remarks>
    /// 事件只由宿主发布、不跨插件。投递不是能力调用：处理器异常不进能力熔断（熔断只对
    /// <see cref="PluginRuntime.Registry.CapabilityGuard"/> 下的能力调用记账）。
    /// 非活动态与已释放态都不投递：宿主在卸载编排里先置 Stopping，投递随即停摆。
    /// </remarks>
    internal sealed class PluginEvents : IPluginEvents
    {
        private readonly PluginServiceScope _scope;
        private readonly IPluginLogSink _logSink;
        private readonly object _sync = new();
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private bool _closed;

        internal PluginEvents(PluginServiceScope scope, IPluginLogSink logSink)
        {
            _scope = scope;
            _logSink = logSink;
        }

        /// <inheritdoc/>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(handler);

            AddHandler(handler);
            var subscription = new Subscription<TEvent>(this, handler);
            try
            {
                return _scope.RegisterHandle(subscription);
            }
            catch (ObjectDisposedException)
            {
                // 作用域已释放：撤回刚登记的处理器，避免留下无句柄订阅。
                subscription.Dispose();
                throw;
            }
        }

        /// <summary>向本插件当前订阅者投递事件（宿主发布路径；调用方负责状态判定）。</summary>
        internal void Publish<TEvent>(TEvent payload) where TEvent : class
        {
            Delegate[] snapshot;
            lock (_sync)
            {
                if (_closed || !_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
                {
                    return;
                }

                snapshot = handlers.ToArray();
            }

            foreach (Delegate handler in snapshot)
            {
                // 释放/停机竞态窗口内不再起新的插件代码执行。
                if (_scope.IsDisposed || !_scope.IsActive)
                {
                    return;
                }

                try
                {
                    ((Action<TEvent>)handler)(payload);
                }
                catch (Exception exception)
                {
                    _logSink.Write(PluginLogEntry.FromException(
                        _scope.PluginId,
                        PluginLogLevel.Error,
                        "宿主中介事件处理器抛出异常",
                        exception));
                }
            }
        }

        /// <summary>关闭订阅表（作用域释放时调用）：之后一律不再投递。</summary>
        internal void Close()
        {
            lock (_sync)
            {
                _closed = true;
                _handlers.Clear();
            }
        }

        private void AddHandler<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            lock (_sync)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
                {
                    handlers = new List<Delegate>();
                    _handlers[typeof(TEvent)] = handlers;
                }

                handlers.Add(handler);
            }
        }

        private void RemoveHandler<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            lock (_sync)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                    {
                        _handlers.Remove(typeof(TEvent));
                    }
                }
            }
        }

        /// <summary>单条订阅：退订幂等，重复退订不影响账本与投递。</summary>
        private sealed class Subscription<TEvent> : IDisposable where TEvent : class
        {
            private readonly PluginEvents _owner;
            private readonly Action<TEvent> _handler;
            private bool _disposed;

            internal Subscription(PluginEvents owner, Action<TEvent> handler)
            {
                _owner = owner;
                _handler = handler;
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

                _owner.RemoveHandler(_handler);
            }
        }
    }
}
