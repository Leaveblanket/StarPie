using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Sdk.Services.Messages;

namespace StarPie.Host.HostServices
{
    /// <summary>
    /// 单个插件事件出口的最小契约：宿主消息泵只经它投递，不感知作用域实现（internal）。
    /// <see cref="PluginServiceScope"/> 是唯一实现，非活动/已释放时投递静默丢弃。
    /// </summary>
    public interface IPluginEventSink
    {
        /// <summary>向该出口投递一条宿主事件（订阅方在调用线程同步执行）。</summary>
        void Publish<TEvent>(TEvent hostEvent) where TEvent : class;
    }

    /// <summary>
    /// headless 插件的宿主消息泵：把宿主消息总线上的托盘状态消息（进托盘/恢复）同步转发到
    /// 全部登记的事件出口——订阅方在宿主 Send 调用线程同步执行，出账语义由此保证。
    /// UI 插件走 <c>PluginUiEvents</c> 直桥不经本泵。
    /// </summary>
    /// <remarks>
    /// 出口以弱引用登记（装载即登记，无需成对注销）：活动期作用域由宿主交接账本强持，
    /// 卸载后出口自身防御（非活动/已释放静默丢弃），对象回收后弱引用自动失效。
    /// 转发对单出口异常兜底——插件侧异常不得沿宿主 Send 传播成 UI 线程未处理异常。
    /// </remarks>
    public sealed class PluginEventPump
    {
        private readonly IMessenger _messenger;
        private readonly object _sync = new();
        private readonly List<WeakReference<IPluginEventSink>> _sinks = new();

        /// <summary>构造消息泵：注册即接线，宿主 Send 托盘状态消息即广播（中介零接线）。</summary>
        public PluginEventPump(IMessenger messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _messenger.Register<MinimizedToTrayMessage>(
                this, (recipient, message) => ((PluginEventPump)recipient).Republish(message));
            _messenger.Register<RestoredFromTrayMessage>(
                this, (recipient, message) => ((PluginEventPump)recipient).Republish(message));
        }

        /// <summary>登记一个刚装载的事件出口（弱引用；重复登记无效）。</summary>
        public void Register(IPluginEventSink sink)
        {
            ArgumentNullException.ThrowIfNull(sink);

            lock (_sync)
            {
                foreach (WeakReference<IPluginEventSink> existing in _sinks)
                {
                    if (existing.TryGetTarget(out IPluginEventSink? live) && ReferenceEquals(live, sink))
                    {
                        return;
                    }
                }
                _sinks.Add(new WeakReference<IPluginEventSink>(sink));
            }
        }

        /// <summary>当前仍存活（未回收）的登记出口数（诊断/测试口径）。</summary>
        public int LiveSinkCount
        {
            get
            {
                lock (_sync)
                {
                    PruneDeadSinksLocked();
                    return _sinks.Count;
                }
            }
        }

        private void Republish<TEvent>(TEvent hostEvent) where TEvent : class
        {
            List<IPluginEventSink> snapshot;
            lock (_sync)
            {
                PruneDeadSinksLocked();
                snapshot = new List<IPluginEventSink>(_sinks.Count);
                foreach (WeakReference<IPluginEventSink> reference in _sinks)
                {
                    if (reference.TryGetTarget(out IPluginEventSink? sink))
                    {
                        snapshot.Add(sink);
                    }
                }
            }

            foreach (IPluginEventSink sink in snapshot)
            {
                try
                {
                    sink.Publish(hostEvent);
                }
                catch (Exception exception)
                {
                    // 出口自身已防御插件处理器异常,此处兜底登记表外异常,不外溢到 Send 调用方。
                    Debug.WriteLine($"[PluginEventPump] forward {typeof(TEvent).Name} failed: {exception.Message}");
                }
            }
        }

        private void PruneDeadSinksLocked()
        {
            _sinks.RemoveAll(reference => !reference.TryGetTarget(out _));
        }
    }
}

