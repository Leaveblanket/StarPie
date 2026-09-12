using System;
using System.Collections.Generic;

namespace StarPie.PluginRuntime.Ui
{
    /// <summary>UI 释放结果：是否全部摘除 + 可定位残留清单（隔离诊断的事实来源）。</summary>
    /// <param name="Succeeded">资产登记表清零且泄漏扫描无残留。</param>
    /// <param name="Residuals">残留描述（类别 + 具体对象/来源）；成功时为空。</param>
    public sealed record PluginUiReleaseResult(bool Succeeded, IReadOnlyList<string> Residuals)
    {
        /// <summary>无 UI 资产或全部摘净的结果。</summary>
        public static PluginUiReleaseResult Success { get; } = new(true, Array.Empty<string>());
    }
}
