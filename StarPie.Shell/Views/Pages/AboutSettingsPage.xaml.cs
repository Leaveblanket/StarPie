using System.Windows.Controls;

namespace StarPie.Views.Pages
{
    /// <summary>
    /// 关于与更新页面：纯静态展示内容、无用户状态，ViewModel 为
    /// <see cref="ViewModels.AboutViewModel"/> 空壳；里程碑等静态内容由 XAML 声明。
    /// </summary>
    public partial class AboutSettingsPage : UserControl
    {
        public AboutSettingsPage()
        {
            InitializeComponent();
        }
    }
}
