namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 准入判定：按「内置清单 → 审核清单 → 开发者模式 → 拒绝」顺序给出准入结果。
    /// </summary>
    /// <remarks>
    /// 不做默认侧载放行：即便插件已签名，未命中清单者仍被拒（签名与清单属同一道闸）。
    /// 本类型只判定准入，不判定清单/包校验违规——违规由扫描层先行拦下并直接记拒绝。
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
        };

        private readonly IReadOnlyCollection<string> _builtInPluginIds;
        private readonly IPluginReviewCatalog _reviewCatalog;

        /// <summary>构造准入策略：内置 id 清单 + 审核清单（缺省为空清单）。</summary>
        public PluginAdmissionPolicy(IEnumerable<string> builtInPluginIds, IPluginReviewCatalog? reviewCatalog = null)
        {
            _builtInPluginIds = builtInPluginIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _reviewCatalog = reviewCatalog ?? new EmptyPluginReviewCatalog();
        }

        /// <summary>判定准入结果；调用方已确认清单与包校验通过时才会走到这里。</summary>
        public PluginAdmissionDecision Decide(string pluginId, string version, bool developerModeEnabled)
        {
            if (_builtInPluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase))
            {
                return new PluginAdmissionDecision(PluginAdmission.BuiltIn, "内置：命中随包第一方插件清单");
            }

            if (_reviewCatalog.IsReviewed(pluginId, version))
            {
                return new PluginAdmissionDecision(PluginAdmission.Reviewed, "已审核：命中审核清单");
            }

            if (developerModeEnabled)
            {
                return new PluginAdmissionDecision(PluginAdmission.DeveloperMode, "开发者模式：未命中审核清单，经开发者模式放行");
            }

            return new PluginAdmissionDecision(PluginAdmission.Rejected, "拒绝：未命中审核清单，且开发者模式未开启");
        }
    }
}
