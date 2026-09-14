using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.ViewModels;

namespace StarPie.Views.Navigation
{
    /// <summary>
    /// 设置控制台主框架：独立承担窗口职责——淡入淡出动画与界面主题应用；
    /// 页面区是 ContentControl（DataContext.CurrentViewModel），页面经 DataTemplate 由页面 VM 映射呈现。
    /// 壳层不感知具体页面，也不持页面 VM 引用。落盘/托盘驻留经 <see cref="IMessenger"/> 广播
    /// 由组合根承接。
    /// </summary>
    /// <remarks>
    /// 本窗口是设置台租户的瞬态窗口：关窗即销毁（不隐藏、不保留状态），托盘驻留由常驻壳层承担，
    /// 重开时重建。壳层静态文案为声明式 {DynamicResource}；Window.Title 收进
    /// <see cref="ShellViewModel.WindowTitle"/>。DataContext 分区——壳区（本窗口）绑壳层 VM，
    /// 导航区（侧栏 + 页面 ContentControl）绑 <see cref="MainViewModel"/>。
    /// 界面主题应用改消息驱动：订阅 <see cref="AppThemeChangedMessage"/> 执行
    /// <see cref="ApplyAppTheme"/>（配置导入后的重挂路径同样经此消息由壳层执行），初始主题
    /// 仍由设置台开窗时直调本方法。
    /// </remarks>
    public partial class MainView : Window
    {
        private readonly ShellViewModel _shell;
        private readonly IThemeService _themeService;

        public MainView(MainViewModel main, ShellViewModel shell, IThemeService themeService)
        {
            InitializeComponent();
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            // 主框架分区 DataContext：壳区（窗口标题/底部操作区）
            // 绑壳层 VM，导航区（侧栏导航项 + 当前页 ContentControl）绑导航 VM。
            DataContext = _shell;
            NavSidebar.DataContext = main ?? throw new ArgumentNullException(nameof(main));
            PageContent.DataContext = main;

            // 壳层静态文案声明式化（{DynamicResource}）；Window.Title 绑定壳层 VM，
            // 语言切换由 ShellViewModel（本地化订阅）刷新，View 不做本地化回填。

            // 主题变更消息订阅（壳层 code-behind 白名单）：界面主题子 VM 写穿配置后发布，
            // 此处执行窗口主题应用；弱引用接收，壳层随窗口生命周期常驻。
            WeakReferenceMessenger.Default.Register<AppThemeChangedMessage>(this, (_, m) => ApplyAppTheme(m.AppTheme));
        }

        /// <summary>应用界面主题到主窗口（窗口视觉是壳层职责：外观页切换主题与导入后同步经此调用，
        /// 页面不持 IThemeService——保持无参构造不经容器）。单一入口 SetTheme + 本窗口 DWM 应用。</summary>
        public void ApplyAppTheme(string appTheme)
        {
            _themeService.SetTheme(appTheme);
            _themeService.ApplyWindowTheme(this);
        }

        /// <summary>
        /// 界面整体按比例缩放并同步窗口尺寸（静默形态 1/2 线性 → 窗口面积 1/4）：
        /// 缩放走根布局 LayoutTransform，逻辑坐标系不变——内容完整可见且 UIA 元素齐全可驱动。
        /// </summary>
        public void ApplyLayoutScale(double scale)
        {
            RootLayout.LayoutTransform = new ScaleTransform(scale, scale);
            Width = Math.Round(Width * scale);
            Height = Math.Round(Height * scale);
        }

        /// <summary>显示并激活主窗口（托盘直达/双击/单实例恢复），带淡入动画。
        /// 首次显示与已显示窗口的重新激活共用本入口（页面切换由目录执行缝
        /// <see cref="INavigationExecutor"/> 先行完成）。</summary>
        public void ShowAndActivate()
        {
            Opacity = 0.0;
            Show();
            WindowState = WindowState.Normal;
            Activate();

            var anim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(160)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(Window.OpacityProperty, anim);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
