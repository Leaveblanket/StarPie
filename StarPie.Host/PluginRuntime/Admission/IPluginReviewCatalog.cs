namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 审核清单判定缝：插件（id + 版本）是否命中当前审核清单，含版本级撤销。
    /// </summary>
    /// <remarks>
    /// 清单来源（签名清单、撤销黑名单）由实现方决定；判定每次都按当前清单重算，
    /// 不在宿主状态里缓存命中结果。实现方实现 <see cref="Review"/> 以给出撤销等可读裁决；
    /// 只关心白名单命中的实现可只实现 <see cref="IsReviewed"/>。
    /// </remarks>
    public interface IPluginReviewCatalog
    {
        /// <summary>该插件版本是否命中审核清单白名单。</summary>
        bool IsReviewed(string pluginId, string version);

        /// <summary>
        /// 完整裁决：白名单命中 / 版本级撤销 / 未列入；默认按 <see cref="IsReviewed"/> 退化，
        /// 不区分撤销与未列入。
        /// </summary>
        PluginReviewDecision Review(string pluginId, string version)
            => IsReviewed(pluginId, version)
                ? new PluginReviewDecision(PluginReviewOutcome.Reviewed, null)
                : new PluginReviewDecision(PluginReviewOutcome.NotListed, null);
    }
}
