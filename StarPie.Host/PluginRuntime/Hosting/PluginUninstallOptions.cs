using System;

namespace StarPie.PluginRuntime.Hosting
{
    /// <summary>
    /// 彻底移除的三条宿主侧接缝：配置段删除、插件数据根目录与配置落盘冲刷。
    /// </summary>
    /// <remarks>
    /// 三条接缝总是一起提供（同一动作的三个落点），合并成一个选项对象避免宿主运行时
    /// 为它们各开一个构造参数；缺省即"不动作"，便于 headless 夹具不依赖配置服务构造。
    /// 配置段删除只动内存态，必须配套冲刷：挂起的防抖落盘会把旧段写回磁盘。
    /// </remarks>
    public sealed record PluginUninstallOptions
    {
        /// <summary>删除 <c>plugins.&lt;id&gt;</c> 配置段的接缝；null 表示不动作。</summary>
        public Action<string>? RemoveConfigSection { get; init; }

        /// <summary>插件数据根目录（每插件一个子目录）；null 表示不清理插件数据。</summary>
        public string? PluginDataRoot { get; init; }

        /// <summary>配置落盘冲刷接缝；null 表示不冲刷。</summary>
        public Action? FlushPendingSaves { get; init; }

        /// <summary>三条接缝都不动作的缺省选项（headless 夹具用）。</summary>
        public static PluginUninstallOptions None { get; } = new();
    }
}
