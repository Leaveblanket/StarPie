using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using Application = System.Windows.Application;
using StarPie.ViewModels;

namespace StarPie.Views.Navigation
{
    /// <summary>
    /// 设置控制台主框架：独立承担窗口职责——关窗隐藏到托盘（含兜底冲刷）、淡入淡出动画；
    /// 页面区是 ContentControl（DataContext.CurrentViewModel），页面经 DataTemplate 由页面 VM 映射呈现。
    /// 壳层不感知具体页面，也不持页面 VM 引用。落盘/托盘驻留经 <see cref="IMessenger"/> 广播
    /// 由组合根承接。
    /// </summary>
    /// <remarks>
    /// 壳层静态文案为声明式 {DynamicResource}；Window.Title 收进
    /// <see cref="ShellViewModel.WindowTitle"/>。DataContext 分区——壳区（本窗口）绑壳层 VM，
    /// 导航区（侧栏 + 页面 ContentControl）绑 <see cref="MainViewModel"/>。
    /// 界面主题应用改消息驱动：订阅 <see cref="AppThemeChangedMessage"/> 执行
    /// <see cref="ApplyAppTheme"/>（配置导入后的重挂路径同样经此消息由壳层执行），初始主题
    /// 仍由 AppHost.Run 直调本方法。
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
            WeakReferenceMessenger.Default.Register<AppThemeChangedMessage>(this, (_, m) => ApplyAppTheme(m.Theme));
        }

        /// <summary>应用界面主题到主窗口（窗口视觉是壳层职责：外观页切换主题与导入后同步经此调用，
        /// 页面不持 IThemeService——保持无参构造不经容器）。单一入口 SetTheme + 本窗口 DWM 应用。</summary>
        public void ApplyAppTheme(string theme)
        {
            _themeService.SetTheme(theme);
            _themeService.ApplyWindowTheme(this);
        }

        /// <summary>Windows 系统深浅色探测（外观页预览渲染取主题用；同属壳层主题职责）。</summary>
        public bool IsWindowsInDarkTheme() => _themeService.IsWindowsInDarkTheme();

        /// <summary>显示并激活主窗口（托盘直达/双击；页面切换由类型化导航服务先行完成），
        /// 带淡入动画。</summary>
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // App 级退出：挂起修改已由组合根冲刷，放行关窗；退出状态在壳层 VM
            // （ShellViewModel.IsExiting），视图不反向依赖组合根。
            if (_shell.IsExiting) return;

            e.Cancel = true;

            // 隐藏前先淡出
            var anim = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(120)));
            anim.Completed += (s, ev) =>
            {
                Hide();
                Opacity = 1.0;
            };
            BeginAnimation(Window.OpacityProperty, anim);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
