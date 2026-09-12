using System;
using System.Collections.Generic;
using StarPie.PluginRuntime.Admission;

namespace StarPie.Tests;

/// <summary>审核清单测试替身：按插件 id 命中固定集合，版本维度不参与判定。</summary>
internal sealed class TestReviewCatalog : IPluginReviewCatalog
{
    private readonly HashSet<string> _reviewed;

    internal TestReviewCatalog(params string[] reviewedPluginIds)
    {
        _reviewed = new HashSet<string>(reviewedPluginIds, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsReviewed(string pluginId, string version) => _reviewed.Contains(pluginId);
}
