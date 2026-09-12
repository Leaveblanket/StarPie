using StarPie.PluginHosting.Extensions;

namespace StarPie.ViewModels.Pages
{
    /// <summary>设置面里的一个插件区块：已本地化的标题 + 区块 VM（由插件描述符工厂创建）。</summary>
    /// <param name="PluginId">贡献该区块的插件 id。</param>
    /// <param name="Title">已本地化的区块标题。</param>
    /// <param name="ViewModel">区块 VM（宿主在刷新时按工厂创建）。</param>
    public sealed record PluginSettingsSectionViewModel(
        string PluginId,
        string Title,
        object ViewModel)
    {
        /// <summary>由协调器的区块登记项构造显示模型。</summary>
        /// <param name="section">插件区块登记项（非 null）。</param>
        /// <param name="title">已本地化标题（非 null）。</param>
        public static PluginSettingsSectionViewModel From(PluginSettingsSection section, string title)
            => new(section.PluginId, title, section.Descriptor.ViewModelFactory());
    }
}
