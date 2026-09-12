using System.Collections.Generic;
using StarPie.PluginHosting.Verification;
using StarPie.PluginRuntime.Ui;

namespace StarPie.PluginHosting.Cleanup
{
    /// <summary>
    /// UI 线程上的有序清理编排：先执行该插件的资产摘除（登记表按类别顺序出账），
    /// 再做全局根扫描；任一残留都如实回报，不谎报成功。
    /// </summary>
    internal sealed class PluginUiCleanup
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly PluginUiLeakVerifier _verifier;

        internal PluginUiCleanup(PluginUiAssetRegistry assets, PluginUiLeakVerifier verifier)
        {
            _assets = assets;
            _verifier = verifier;
        }

        /// <summary>执行释放编排；<paramref name="host"/> 为 null 表示该插件没有 UI 上下文。</summary>
        internal PluginUiReleaseResult Release(string pluginId, PluginUiHost? host)
        {
            // 摘除先执行；未摘净的资产由验证器统一作为残留报告，避免两处各报一遍。
            host?.Release();
            IReadOnlyList<string> residuals = _verifier.Verify(pluginId, _assets);
            return residuals.Count == 0
                ? PluginUiReleaseResult.Success
                : new PluginUiReleaseResult(false, residuals);
        }
    }
}
