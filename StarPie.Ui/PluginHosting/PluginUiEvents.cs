using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Events;

namespace StarPie.PluginHosting
{
    /// <summary>
    /// UI 插件的应用级事件中介：把宿主消息总线（<see cref="IMessenger"/>）桥接为
    /// <see cref="IPluginEvents"/>——宿主经消息总线发布的事件，插件侧原样订阅。
    /// </summary>
    /// <remarks>
    /// 订阅返回的句柄由 <see cref="PluginUiHost"/> 包进资产登记表，卸载编排 Dispose 即退订；
    /// 本类只做桥接与线程内投递，不做插件作用域记账（那是 headless 侧 <c>PluginEvents</c> 的职责）。
    /// 处理器异常在桥接边界吞掉并记调试输出：宿主 Send 路径不能被插件处理器打断。
    /// </remarks>
    public sealed class PluginUiEvents : IPluginEvents
    {
        private readonly IMessenger _messenger;

        /// <summary>构造桥接中介：消息总线为宿主组合根注册的单一实例。</summary>
        public PluginUiEvents(IMessenger messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }

        /// <inheritdoc/>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(handler);

            // 每条订阅一个独立 recipient：同类型重复订阅互不冲突，退订按 recipient 精确摘除。
            var recipient = new Recipient<TEvent>(handler);
            _messenger.Register(recipient, (Recipient<TEvent> r, TEvent payload) => r.Handle(payload));
            return new Subscription<TEvent>(_messenger, recipient);
        }

        private sealed class Recipient<TEvent> where TEvent : class
        {
            private readonly Action<TEvent> _handler;

            internal Recipient(Action<TEvent> handler) => _handler = handler;

            internal void Handle(TEvent payload)
            {
                try
                {
                    _handler(payload);
                }
                catch (Exception exception)
                {
                    // 插件处理器异常不得打断宿主发布路径；现状只落调试输出，与宿主诊断面解耦。
                    Debug.WriteLine($"Plugin UI event handler threw: {exception}");
                }
            }
        }

        private sealed class Subscription<TEvent> : IDisposable where TEvent : class
        {
            private readonly IMessenger _messenger;
            private readonly Recipient<TEvent> _recipient;
            private bool _disposed;

            internal Subscription(IMessenger messenger, Recipient<TEvent> recipient)
            {
                _messenger = messenger;
                _recipient = recipient;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _messenger.Unregister<TEvent>(_recipient);
            }
        }
    }
}
