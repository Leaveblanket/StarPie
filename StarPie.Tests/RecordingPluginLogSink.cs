using System.Collections.Generic;
using StarPie.HostServices;

namespace StarPie.Tests;

/// <summary>
/// 日志 sink 测试替身：只收宿主 DTO，供断言"入 sink 的不是插件对象"。
/// </summary>
public sealed class RecordingPluginLogSink : IPluginLogSink
{
    /// <summary>按写入顺序记录的日志条目。</summary>
    public List<PluginLogEntry> Entries { get; } = new();

    /// <inheritdoc/>
    public void Write(PluginLogEntry entry) => Entries.Add(entry);
}
