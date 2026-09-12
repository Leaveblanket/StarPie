using System;
using System.Windows;
using StarPie.Abstractions.Ui;

namespace StarPie.Plugin.MinimalUi
{
    /// <summary>
    /// 最小 UI 插件的 UI 入口（清单 ui.entryType）：合并资源字典 + 注册一个导航页。
    /// 全部 UI 资产只经 <see cref="IPluginUiContext"/> 契约进入宿主资产登记表。
    /// </summary>
    public sealed class MinimalUiModule : IPluginUiModule
    {
        private static readonly Uri ResourceDictionaryUri = new(
            "pack://application:,,,/StarPie.Plugin.MinimalUi;component/Themes/MinimalResources.xaml",
            UriKind.Absolute);

        /// <inheritdoc/>
        public void RegisterUi(IPluginUiContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // 视图映射（VM → 视图的 DataTemplate）随插件资源根并入宿主，卸载时整根摘除。
            context.MergeResourceDictionary(ResourceDictionaryUri);

            // 导航页：宿主在 UI 线程调用工厂创建 VM；宿主侧边栏出现 NavPlugin_<插件 id> 项。
            context.RegisterPage(new PluginPageDescriptor(
                "minimal-main",
                "最小 UI 示例",
                "M3,3H21V21H3V3M5,5V19H19V5H5Z",
                () => new MinimalPageViewModel()));
        }
    }
}
