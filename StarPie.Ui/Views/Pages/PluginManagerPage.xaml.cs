using System.Windows.Controls;

namespace StarPie.Views.Pages
{
    /// <summary>
    /// 插件管理页：插件列表、状态与启停/重试/诊断入口。
    /// </summary>
    /// <remarks>
    /// 全部行为在 <see cref="ViewModels.Pages.PluginManagerViewModel"/>；code-behind 只做
    /// <c>InitializeComponent</c>，不持有 VM 类型。
    /// </remarks>
    public partial class PluginManagerPage : UserControl
    {
        public PluginManagerPage()
        {
            InitializeComponent();
        }
    }
}
