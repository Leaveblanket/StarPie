using System.Windows.Controls;

namespace WinPieGestures.Views.Navigation
{
    /// <summary>
    /// 侧边栏视图 (T19)：独立承担导航壳层——品牌区、数据驱动导航项与版本页脚。
    /// DataContext 继承自主框架的 <see cref="ViewModels.MainViewModel"/>。
    /// 副标题等壳层静态文案为声明式 {DynamicResource}（ADR-0010），随运行时语言字典即时刷新，
    /// 无 code-behind 本地化回填；AutomationId 保留供 e2e/辅助功能定位。
    /// </summary>
    public partial class SidebarView : UserControl
    {
        public SidebarView()
        {
            InitializeComponent();
        }
    }
}
