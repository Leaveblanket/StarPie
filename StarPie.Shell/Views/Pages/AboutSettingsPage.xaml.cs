namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 关于与更新页面 (T19/T24)：迁移前 SettingsWindow PAGE 4 原样搬迁；已本地化文本经
    /// 语言字典声明式化（里程碑等未本地化硬编码文案不在本票）。
    /// 页面 ViewModel 为 <see cref="ViewModels.AboutViewModel"/> 空壳（纯静态展示内容，无用户状态）。
    /// </summary>
    public partial class AboutSettingsPage : SettingsPageBase
    {
        public AboutSettingsPage()
        {
            InitializeComponent();
        }
    }
}
