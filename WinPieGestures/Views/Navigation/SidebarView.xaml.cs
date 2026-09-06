using System.Windows.Controls;

namespace StarPie.Views.Navigation
{
    /// <summary>
    /// 侧边栏视图 (T19)：独立承担导航壳层——品牌区、数据驱动导航项与版本页脚。
    /// DataContext 由主框架分区接线指向 MainViewModel（B1/D3：导航区绑导航 VM；
    /// 主窗口壳区绑 ShellViewModel）。
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
