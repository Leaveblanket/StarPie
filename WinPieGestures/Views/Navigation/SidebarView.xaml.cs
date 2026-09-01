using System.Windows.Controls;
using WinPieGestures.Services;

namespace WinPieGestures.Views.Navigation
{
    /// <summary>
    /// 侧边栏视图 (T19)：独立承担导航壳层——品牌区、数据驱动导航项与版本页脚。
    /// DataContext 继承自主框架的 <see cref="ViewModels.MainViewModel"/>。
    /// 壳层文本（副标题）随 I18n 语言广播刷新；本视图与主窗口同生命周期，静态订阅无泄漏。
    /// </summary>
    public partial class SidebarView : UserControl
    {
        public SidebarView()
        {
            InitializeComponent();
            ApplyLocalization();
            I18n.LanguageChanged += ApplyLocalization;
        }

        private void ApplyLocalization()
        {
            if (SidebarSubtitleText != null) SidebarSubtitleText.Text = I18n.T("AppSubtitle");
        }
    }
}
