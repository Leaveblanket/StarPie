using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Input;
using StarPie.PluginRuntime.Loading;

namespace StarPie.PluginHosting.Verification
{
    /// <summary>
    /// 泄漏验证器：生产诊断与测试共用同一套判定——资产登记表清零 + 全局根扫描无插件残留。
    /// </summary>
    /// <remarks>
    /// 全局根扫描覆盖 <c>Application.Current.Windows</c> 与 <c>Application.Current.Resources</c> 中
    /// 的插件来源对象；插件归属按程序集所在 ALC 名（<see cref="PluginLoadContext.ContextNameFor"/>）
    /// 与资源字典来源路径判定。ALC 与程序集本身是否回收不在本验证器判据内：UI 插件不承诺程序集
    /// 回收，存活只作诊断。
    /// </remarks>
    public sealed class PluginUiLeakVerifier
    {
        private readonly Application _application;

        /// <summary>构造验证器。</summary>
        /// <param name="application">宿主应用实例（窗口与资源的全局根）。</param>
        public PluginUiLeakVerifier(Application application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        /// <summary>验证指定插件的 UI 现场；返回可定位残留清单（空清单即通过）。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="assets">资产登记表。</param>
        public IReadOnlyList<string> Verify(string pluginId, PluginUiAssetRegistry assets)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            ArgumentNullException.ThrowIfNull(assets);

            var findings = new List<string>();

            foreach (PluginUiAsset asset in assets.Snapshot(pluginId))
            {
                string failure = string.IsNullOrEmpty(asset.DetachFailure)
                    ? string.Empty
                    : $"：{asset.DetachFailure}";
                findings.Add($"登记表残留：{DescribeKind(asset.Kind)}（{asset.Description}）{failure}");
            }

            foreach (Window window in _application.Windows.OfType<Window>())
            {
                if (IsOwnedByPlugin(window.GetType(), pluginId))
                {
                    findings.Add($"未登记的插件窗口：{window.GetType().FullName}");
                }

                foreach (ResourceDictionary dictionary in EnumerateDictionaries(window.Resources))
                {
                    if (IsOwnedByPlugin(dictionary, pluginId))
                    {
                        findings.Add($"窗口资源残留插件资源字典：{DescribeSource(dictionary)}");
                    }
                }
            }

            foreach (ResourceDictionary dictionary in EnumerateDictionaries(_application.Resources))
            {
                if (IsOwnedByPlugin(dictionary, pluginId))
                {
                    findings.Add($"宿主资源残留插件资源字典：{DescribeSource(dictionary)}");
                }
            }

            if (_application.MainWindow is { } mainWindow)
            {
                foreach (InputBinding binding in mainWindow.InputBindings.OfType<InputBinding>())
                {
                    object? owner = binding.Command ?? binding.CommandParameter;
                    if (owner is not null && IsOwnedByPlugin(owner.GetType(), pluginId))
                    {
                        findings.Add($"主窗口残留插件输入绑定：{binding.GetType().FullName}");
                    }
                }
            }

            return findings;
        }

        /// <summary>递归枚举合并表里的资源字典（含嵌套合并）。</summary>
        private static IEnumerable<ResourceDictionary> EnumerateDictionaries(ResourceDictionary root)
        {
            foreach (ResourceDictionary dictionary in root.MergedDictionaries)
            {
                yield return dictionary;
                foreach (ResourceDictionary nested in EnumerateDictionaries(dictionary))
                {
                    yield return nested;
                }
            }
        }

        /// <summary>插件归属判定：类型所在 ALC 名等于插件上下文命名规则。</summary>
        private static bool IsOwnedByPlugin(Type type, string pluginId)
        {
            AssemblyLoadContext? context = AssemblyLoadContext.GetLoadContext(type.Assembly);
            return string.Equals(
                context?.Name,
                PluginLoadContext.ContextNameFor(pluginId),
                StringComparison.Ordinal);
        }

        /// <summary>资源字典归属判定：来源指向插件包目录（<c>plugins/&lt;id&gt;/</c>）。</summary>
        private static bool IsOwnedByPlugin(ResourceDictionary dictionary, string pluginId)
            => DescribeSource(dictionary) is string source
                && source.Contains($"/plugins/{pluginId}/", StringComparison.OrdinalIgnoreCase);

        private static string? DescribeSource(ResourceDictionary dictionary)
            => dictionary.Source?.OriginalString;

        private static string DescribeKind(PluginUiAssetKind kind) => kind switch
        {
            PluginUiAssetKind.View => "视图",
            PluginUiAssetKind.Window => "窗口",
            PluginUiAssetKind.ResourceRoot => "资源",
            PluginUiAssetKind.Page => "导航页",
            PluginUiAssetKind.SettingsSection => "设置区块",
            PluginUiAssetKind.MenuItem => "菜单项",
            PluginUiAssetKind.Command => "命令",
            PluginUiAssetKind.Timer => "定时器",
            PluginUiAssetKind.Animation => "动画",
            PluginUiAssetKind.Subscription => "订阅",
            _ => kind.ToString(),
        };
    }
}
