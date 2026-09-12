namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 审核清单判定缝：插件（id + 版本）是否命中当前审核清单。
    /// </summary>
    /// <remarks>
    /// 清单来源（签名清单、撤销黑名单）由实现方决定；判定每次都按当前清单重算，
    /// 不在宿主状态里缓存命中结果。
    /// </remarks>
    public interface IPluginReviewCatalog
    {
        /// <summary>该插件版本是否命中审核清单。</summary>
        bool IsReviewed(string pluginId, string version);
    }
}
