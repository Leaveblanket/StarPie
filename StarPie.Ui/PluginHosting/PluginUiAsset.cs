using System;

namespace StarPie.PluginHosting
{
    /// <summary>
    /// 资产登记表条目：一项 UI 资产的身份、类别、可读描述与摘除动作。
    /// </summary>
    /// <remarks>
    /// 摘除动作由登记方（宿主托管层）提供，返回是否摘除成功；动画等"只停不摘"的类别只有在
    /// 真正从视觉树/时钟树摘除（<c>Storyboard.Remove</c>）后才算摘除成功。
    /// </remarks>
    public sealed class PluginUiAsset
    {
        private readonly Func<bool> _detach;

        internal PluginUiAsset(
            string pluginId,
            PluginUiAssetKind kind,
            string description,
            Func<bool> detach)
        {
            PluginId = pluginId;
            Kind = kind;
            Description = description;
            _detach = detach;
        }

        /// <summary>所属插件 id。</summary>
        public string PluginId { get; }

        /// <summary>资产类别。</summary>
        public PluginUiAssetKind Kind { get; }

        /// <summary>可读描述（用于诊断与残留清单）。</summary>
        public string Description { get; }

        /// <summary>最近一次摘除失败的原因（异常类型 + 消息，或"摘除动作返回失败"）；未失败时为 null。</summary>
        public string? DetachFailure { get; internal set; }

        /// <summary>执行摘除动作；返回是否已从宿主现场摘除（失败即残留）。</summary>
        internal bool TryDetach() => _detach();
    }
}
