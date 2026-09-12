using System;
using StarPie.Abstractions;

namespace StarPie.HostServices
{
    /// <summary>
    /// 宿主日志与诊断的唯一载体：只含 plugin id、级别、文本与异常的三段字符串。
    /// </summary>
    /// <remarks>
    /// 插件异常在写入边界（<see cref="PluginLog.FromException"/>）即转成
    /// "类型全名 + message + stack 文本"，`Exception` 实例与任何插件对象都不进本类型，
    /// 因此日志 sink、诊断快照等长生命周期结构不会经此 root 插件集。
    /// </remarks>
    /// <param name="PluginId">来源插件 id（内置与宿主自有日志也走同一字段）。</param>
    /// <param name="Level">日志级别。</param>
    /// <param name="Message">日志文本。</param>
    /// <param name="ExceptionType">异常类型全名；无异常时为 null。</param>
    /// <param name="ExceptionMessage">异常 message；无异常时为 null。</param>
    /// <param name="ExceptionDetail">异常"类型 + message + stack"文本；无异常时为 null。</param>
    /// <param name="TimestampUtc">写入时刻（UTC）。</param>
    public sealed record PluginLogEntry(
        string PluginId,
        PluginLogLevel Level,
        string Message,
        string? ExceptionType,
        string? ExceptionMessage,
        string? ExceptionDetail,
        DateTimeOffset TimestampUtc)
    {
        /// <summary>构造一条不带异常的日志条目。</summary>
        /// <param name="pluginId">来源插件 id。</param>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志文本。</param>
        public static PluginLogEntry Create(string pluginId, PluginLogLevel level, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            ArgumentNullException.ThrowIfNull(message);
            return new PluginLogEntry(
                pluginId,
                level,
                message,
                null,
                null,
                null,
                DateTimeOffset.UtcNow);
        }

        /// <summary>构造一条带异常的日志条目：异常在此处降为纯字符串，之后不再引用异常实例。</summary>
        /// <param name="pluginId">来源插件 id。</param>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志文本。</param>
        /// <param name="exception">插件抛出的异常。</param>
        public static PluginLogEntry FromException(
            string pluginId,
            PluginLogLevel level,
            string message,
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            PluginLogEntry entry = Create(pluginId, level, message);
            return entry with
            {
                ExceptionType = exception.GetType().FullName,
                ExceptionMessage = exception.Message,
                ExceptionDetail = exception.ToString(),
            };
        }
    }
}
