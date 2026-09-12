namespace StarPie.Abstractions
{
    /// <summary>插件日志级别（由低到高）。</summary>
    public enum PluginLogLevel
    {
        /// <summary>最细粒度的追踪信息。</summary>
        Trace,

        /// <summary>调试信息。</summary>
        Debug,

        /// <summary>常规运行信息。</summary>
        Information,

        /// <summary>可恢复的异常情况。</summary>
        Warning,

        /// <summary>失败但插件可继续运行。</summary>
        Error,

        /// <summary>插件自身难以继续运行的严重失败。</summary>
        Critical,
    }

    /// <summary>
    /// 宿主日志面：插件只经本接口写宿主日志，宿主自动带上 plugin id。
    /// </summary>
    /// <remarks>
    /// 宿主不在日志结构里保存插件对象：异常在写入边界即转成"类型全名 + message + stack 文本"，
    /// 因此插件异常与插件自定义类型不会经日志被长生命周期结构 root。
    /// </remarks>
    public interface IPluginLog
    {
        /// <summary>写一条不带异常的日志。</summary>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志文本。</param>
        void Write(PluginLogLevel level, string message);

        /// <summary>写一条带异常的日志；异常在边界处转为宿主 DTO 文本。</summary>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志文本。</param>
        /// <param name="exception">插件抛出的异常（宿主不保存该实例）。</param>
        void Write(PluginLogLevel level, string message, System.Exception exception);
    }
}
