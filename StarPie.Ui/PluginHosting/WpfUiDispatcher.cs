using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting
{
    /// <summary>WPF 适配的 UI 线程端口：包 <see cref="Dispatcher"/> 的线程亲缘判定与排队调用。</summary>
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        /// <summary>构造适配器。</summary>
        /// <param name="dispatcher">宿主 UI 线程的调度器（非 null）。</param>
        public WpfUiDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>被包装的调度器（宿主托管层内部使用；插件只见 <see cref="IUiDispatcher"/>）。</summary>
        public Dispatcher Dispatcher { get; }

        /// <inheritdoc/>
        public bool IsOnUiThread => Dispatcher.CheckAccess();

        /// <inheritdoc/>
        public Task InvokeAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Dispatcher.InvokeAsync(action).Task;
        }

        /// <inheritdoc/>
        public Task<T> InvokeAsync<T>(Func<T> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            return Dispatcher.InvokeAsync(callback).Task;
        }
    }
}
