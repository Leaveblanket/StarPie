using System;
using StarPie.Abstractions;

namespace StarPie.HostServices
{
    /// <summary>
    /// 单插件日志实现（插件只看到 <see cref="IPluginLog"/>）：自动带 plugin id，
    /// 异常在边界处转成宿主 DTO；作用域释放后静默丢弃（卸载后不再受理插件写入）。
    /// </summary>
    internal sealed class PluginLog : IPluginLog
    {
        private readonly string _pluginId;
        private readonly IPluginLogSink _sink;
        private readonly Func<bool> _isScopeDisposed;

        internal PluginLog(string pluginId, IPluginLogSink sink, Func<bool> isScopeDisposed)
        {
            _pluginId = pluginId;
            _sink = sink;
            _isScopeDisposed = isScopeDisposed;
        }

        /// <inheritdoc/>
        public void Write(PluginLogLevel level, string message)
        {
            if (_isScopeDisposed())
            {
                return;
            }

            _sink.Write(PluginLogEntry.Create(_pluginId, level, message));
        }

        /// <inheritdoc/>
        public void Write(PluginLogLevel level, string message, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (_isScopeDisposed())
            {
                return;
            }

            _sink.Write(PluginLogEntry.FromException(_pluginId, level, message, exception));
        }
    }
}
