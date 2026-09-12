namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 准入判定：按「内置清单 → 审核清单（含签名）→ 开发者模式 → 拒绝」顺序给出准入结果。
    /// </summary>
    /// <remarks>
    /// 不做默认侧载放行：即便插件已签名，未命中清单者仍被拒（签名与清单属同一道闸）。
    /// 审核清单命中还须签名可信（Authenticode 可信链，或签名链不可信但发布者指纹被 pin）；
    /// 版本级撤销命中即拒绝。本类型只判定准入，不判定清单/包校验违规——违规由扫描层先行拦下并直接记拒绝。
    /// </remarks>
    public sealed class PluginAdmissionPolicy
    {
        /// <summary>
        /// 随包第一方插件的内置 id 清单：随宿主分发并在此登记 id 的插件视为内置。
        /// 未登记的包（即便落在安装目录）按未审核处理，不做位置信任。
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultBuiltInPluginIds = new[]
        {
            // 首个随包 headless 插件：程序来源（plugins/src/StarPie.Plugin.Programs）。
            "starpie.builtin.program-source",
            // 首个随包 UI 示例插件（plugins/src/StarPie.Plugin.SampleUi）。
            "starpie.builtin.sample-ui",
        };

        private readonly IReadOnlyCollection<string> _builtInPluginIds;
        private readonly IPluginReviewCatalog _reviewCatalog;
        private readonly HashSet<string> _pinnedPublisherHashes;

        /// <summary>构造准入策略：内置 id 清单 + 审核清单（缺省为空清单）+ 受 pin 的发布者指纹。</summary>
        /// <param name="builtInPluginIds">内置插件 id 清单。</param>
        /// <param name="reviewCatalog">审核清单；缺省为空清单。</param>
        /// <param name="pinnedPublisherHashes">
        /// 受 pin 的发布者指纹（签名证书 SHA-256 哈希十六进制串）：清单命中而 Authenticode 链不可信时，
        /// 指纹命中且内容未被篡改者仍视为签名可信（「受 pin 的发布者证书」路径）。
        /// </param>
        public PluginAdmissionPolicy(
            IEnumerable<string> builtInPluginIds,
            IPluginReviewCatalog? reviewCatalog = null,
            IEnumerable<string>? pinnedPublisherHashes = null)
        {
            _builtInPluginIds = builtInPluginIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _reviewCatalog = reviewCatalog ?? new EmptyPluginReviewCatalog();
            _pinnedPublisherHashes = new HashSet<string>(
                pinnedPublisherHashes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>判定准入结果；调用方已确认清单与包校验通过时才会走到这里。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="version">清单版本（清单不可用时为空串）。</param>
        /// <param name="developerModeEnabled">开发者模式是否开启。</param>
        /// <param name="signature">入口程序集签名校验结果；扫描器未配置校验器时为 null（视为不可信）。</param>
        public PluginAdmissionDecision Decide(
            string pluginId,
            string version,
            bool developerModeEnabled,
            PluginSignatureCheck? signature)
        {
            if (_builtInPluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase))
            {
                return new PluginAdmissionDecision(PluginAdmission.BuiltIn, "内置：命中随包第一方插件清单");
            }

            PluginReviewDecision review = _reviewCatalog.Review(pluginId, version);
            if (review.Outcome == PluginReviewOutcome.Revoked)
            {
                return new PluginAdmissionDecision(
                    PluginAdmission.Rejected, $"已撤销：{review.Detail ?? "命中审核清单黑名单"}");
            }

            if (review.Outcome == PluginReviewOutcome.Reviewed)
            {
                if (IsSignatureTrusted(signature))
                {
                    return new PluginAdmissionDecision(PluginAdmission.Reviewed, "已审核：命中审核清单，且签名可信");
                }

                return new PluginAdmissionDecision(
                    PluginAdmission.Rejected,
                    $"命中审核清单但签名不可信：{signature?.Detail ?? "无签名校验结果"}");
            }

            if (developerModeEnabled)
            {
                return new PluginAdmissionDecision(PluginAdmission.DeveloperMode, "开发者模式：未命中审核清单，经开发者模式放行");
            }

            return new PluginAdmissionDecision(PluginAdmission.Rejected, "拒绝：未命中审核清单，且开发者模式未开启");
        }

        /// <summary>
        /// 签名可信判定：Authenticode 可信链通过；或链不可信但发布者指纹被 pin 且内容未被篡改——
        /// 发布者可信不等于被改过的文件可信，摘要失配一律不可信。
        /// </summary>
        private bool IsSignatureTrusted(PluginSignatureCheck? signature)
            => signature is not null
                && !signature.ContentMismatch
                && (signature.Status == PluginSignatureStatus.Valid
                    || (signature.Status == PluginSignatureStatus.Invalid
                        && signature.PublisherHash is not null
                        && _pinnedPublisherHashes.Contains(signature.PublisherHash)));
    }
}
