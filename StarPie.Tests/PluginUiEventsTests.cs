using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.PluginHosting;
using Xunit;

namespace StarPie.Tests
{
    /// <summary>
    /// UI 插件事件中介的桥接行为：订阅即收宿主消息、退订即断、
    /// 处理器异常在桥接边界吞掉不打断宿主发布路径。
    /// </summary>
    public sealed class PluginUiEventsTests
    {
        private sealed record ProbeEvent(string Payload);

        [Fact]
        public void 订阅即收消息_退订即断_处理器异常不打断发布()
        {
            var bus = new WeakReferenceMessenger();
            PluginUiEvents events = new(bus);
            var received = new List<string>();

            IDisposable subscription = events.Subscribe<ProbeEvent>(payload => received.Add(payload.Payload));
            bus.Send(new ProbeEvent("第一条"));
            Assert.Single(received);

            subscription.Dispose();
            bus.Send(new ProbeEvent("退订后"));
            Assert.Single(received);
        }

        [Fact]
        public void 处理器抛异常_宿主Send路径不被打断()
        {
            var bus = new WeakReferenceMessenger();
            PluginUiEvents events = new(bus);
            var received = new List<string>();

            IDisposable throwing = events.Subscribe<ProbeEvent>(_ => throw new InvalidOperationException("插件处理器异常"));
            IDisposable healthy = events.Subscribe<ProbeEvent>(payload => received.Add(payload.Payload));

            var exception = Record.Exception(() => bus.Send(new ProbeEvent("脉冲")));
            Assert.Null(exception);
            Assert.Single(received);

            throwing.Dispose();
            healthy.Dispose();
        }
    }
}
