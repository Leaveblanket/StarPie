using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using StarPie.PluginRuntime.Loading;

namespace StarPie.PluginHosting.Verification
{
    /// <summary>
    /// 泄漏验证器：生产诊断与测试共用同一套判定——资产登记表清零 + 全局根扫描无插件残留。
    /// </summary>
    /// <remarks>
    /// 全局根扫描覆盖 <c>Application.Current.Windows</c> 与 <c>Application.Current.Resources</c> 中
    /// 的插件来源对象；插件归属按程序集所在 ALC 名（<see cref="PluginLoadContext.ContextNameFor"/>）
    /// 与资源字典来源判定。资源字典归属的三条判据依次是：来源指向插件包目录、来源引用插件 ALC 内的
    /// 程序集、字典内容（模板/样式的目标类型）命中插件 ALC——只认来源路径会漏掉
    /// <c>pack://application:,,,/插件程序集;component/...</c> 这种规范形态。
    /// ALC 与程序集本身是否回收不在本验证器判据内：UI 插件不承诺程序集回收，存活只作诊断。
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

            foreach (Window window in _application.Windows.OfType<Window>().ToList())
            {
                if (IsOwnedByPlugin(window.GetType(), pluginId))
                {
                    findings.Add($"未登记的插件窗口：{window.GetType().FullName}");

                    // 兜底摘除：越权自建的窗口按 plugin id 关闭；命中本身仍记账（调用方据此隔离）。
                    if (window.IsLoaded || window.IsVisible)
                    {
                        window.Close();
                    }
                }

                foreach (ResourceDictionary dictionary in EnumerateDictionaries(window.Resources).ToList())
                {
                    if (IsOwnedByPlugin(dictionary, pluginId))
                    {
                        findings.Add($"窗口资源残留插件资源字典：{DescribeSource(dictionary)}");
                        DetachFrom(window.Resources, dictionary);
                    }
                }
            }

            foreach (ResourceDictionary dictionary in EnumerateDictionaries(_application.Resources).ToList())
            {
                if (IsOwnedByPlugin(dictionary, pluginId))
                {
                    findings.Add($"宿主资源残留插件资源字典：{DescribeSource(dictionary)}");
                    DetachFrom(_application.Resources, dictionary);
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

        /// <summary>
        /// 从资源树里摘除一个越权并入的插件资源字典（按 plugin id 摘除的兜底动作）；
        /// 摘除不全等于放过——命中本身已由返回的残留清单记账（调用方据此隔离）。
        /// </summary>
        private static void DetachFrom(ResourceDictionary root, ResourceDictionary leaked)
        {
            if (root.MergedDictionaries.Remove(leaked))
            {
                return;
            }

            foreach (ResourceDictionary dictionary in EnumerateDictionaries(root))
            {
                if (dictionary.MergedDictionaries.Remove(leaked))
                {
                    return;
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

        /// <summary>
        /// 资源字典归属判定：来源指向插件包目录（<c>plugins/&lt;id&gt;/</c>）、来源引用插件 ALC 内的
        /// 程序集，或字典内容命中插件类型。
        /// </summary>
        private static bool IsOwnedByPlugin(ResourceDictionary dictionary, string pluginId)
        {
            if (DescribeSource(dictionary) is { Length: > 0 } source)
            {
                if (source.Contains($"/plugins/{pluginId}/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (IsOwnedByPlugin(source, pluginId))
                {
                    return true;
                }
            }

            // 无来源（内存构造）的字典只按内容归属判定：命中插件类型即认定归属。
            return ContainsPluginOwnedContent(dictionary, pluginId);
        }

        /// <summary>
        /// 来源引用插件 ALC 内程序集：<c>pack://application:,,,/程序集名;component/…</c> 是插件资源
        /// 字典的规范形态（包目录路径不出现在 pack URI 里），归属只能按 ALC 里的程序集名判定。
        /// </summary>
        private static bool IsOwnedByPlugin(string source, string pluginId)
        {
            AssemblyLoadContext? context = FindLoadContext(pluginId);
            if (context is null)
            {
                return false;
            }

            return context.Assemblies.Any(assembly =>
                source.Contains(
                    $"/{assembly.GetName().Name};component/",
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 字典内容归属判定：模板/样式直接暴露目标类型，命中插件 ALC 即认定归属；这类字典通常
        /// 由插件在内存里构造（无 pack URI 可查），是本验证器查来源之外唯一可判定的信号。
        /// </summary>
        private static bool ContainsPluginOwnedContent(ResourceDictionary dictionary, string pluginId)
        {
            foreach (object? key in dictionary.Keys)
            {
                if (IsPluginType(DescribeDataType(key), pluginId)
                    || IsPluginType(DescribeDataType(dictionary[key]), pluginId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>取模板键/值的声明目标类型：模板与样式只在这一处暴露插件类型。</summary>
        private static Type? DescribeDataType(object? candidate) => candidate switch
        {
            DataTemplateKey key => key.DataType as Type,
            Type type => type,
            DataTemplate template => template.DataType as Type,
            Style style => style.TargetType,
            _ => null,
        };

        private static bool IsPluginType(Type? type, string pluginId)
            => type is not null && IsOwnedByPlugin(type, pluginId);

        /// <summary>按插件上下文命名规则取该插件的 ALC；未装载（上下文已卸载）时为 null。</summary>
        private static AssemblyLoadContext? FindLoadContext(string pluginId)
            => AssemblyLoadContext.All.FirstOrDefault(context => string.Equals(
                context.Name,
                PluginLoadContext.ContextNameFor(pluginId),
                StringComparison.Ordinal));

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
