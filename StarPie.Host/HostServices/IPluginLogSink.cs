namespace StarPie.HostServices
{
    /// <summary>
    /// 宿主日志落点：插件日志与能力守卫日志都经本接缝写宿主侧诊断。
    /// </summary>
    /// <remarks>
    /// 接缝只收 <see cref="PluginLogEntry"/>（纯字符串与值类型 DTO）——实现方不得要求插件对象，
    /// 这是"日志与诊断不 root 插件集"的机械保障。实现方必须线程安全且不抛异常。
    /// </remarks>
    public interface IPluginLogSink
    {
        /// <summary>写入一条日志条目。</summary>
        /// <param name="entry">宿主 DTO（只含字符串与值类型）。</param>
        void Write(PluginLogEntry entry);
    }
}
