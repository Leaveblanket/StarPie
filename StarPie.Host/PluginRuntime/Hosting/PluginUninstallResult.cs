using System;
using System.Collections.Generic;

namespace StarPie.PluginRuntime.Hosting
{
    /// <summary>
    /// 彻底移除的结果：四类产物（包 / 配置段 / 插件数据 / 宿主状态）是否全部离场。
    /// </summary>
    /// <remarks>
    /// 任何一类没清掉都算失败并逐条给出原因——部分清理比不清理更危险，
    /// 管理面据 <see cref="Failures"/> 告诉用户还剩下什么。
    /// </remarks>
    /// <param name="PluginId">插件 id。</param>
    /// <param name="Succeeded">四类产物是否全部离场。</param>
    /// <param name="Failures">未清掉的产物清单（可读原因）；全部离场时为空。</param>
    public sealed record PluginUninstallResult(
        string PluginId,
        bool Succeeded,
        IReadOnlyList<string> Failures)
    {
        /// <summary>失败原因的单行摘要；成功时为 null。</summary>
        public string? FailureReason => Failures.Count == 0 ? null : string.Join("；", Failures);

        /// <summary>四类产物全部离场的成功结论。</summary>
        public static PluginUninstallResult Success(string pluginId)
            => new(pluginId, true, Array.Empty<string>());
    }
}
