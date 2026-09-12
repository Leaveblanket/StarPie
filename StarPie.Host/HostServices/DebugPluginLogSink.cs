using System.Diagnostics;

namespace StarPie.HostServices
{
    /// <summary>
    /// 默认日志落点：写 <see cref="Debug"/> 输出。
    /// </summary>
    internal sealed class DebugPluginLogSink : IPluginLogSink
    {
        /// <summary>无状态单例（sink 不得持有状态，可安全共享）。</summary>
        internal static DebugPluginLogSink Instance { get; } = new();

        private DebugPluginLogSink()
        {
        }

        /// <inheritdoc/>
        public void Write(PluginLogEntry entry)
        {
            string detail = entry.ExceptionDetail is null ? string.Empty : $" | {entry.ExceptionDetail}";
            Debug.WriteLine($"[plugin:{entry.PluginId}][{entry.Level}] {entry.Message}{detail}");
        }
    }
}
