using System;
using System.Windows;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Sdk.ViewModels.Pages;
using StarPie.Sdk.ViewModels.Wheel;
using StarPie.Ui.ViewModels.Dialogs;
using StarPie.Ui.ViewModels.WheelInteraction;
using StarPie.Ui.ViewModels.Navigation;
using StarPie.Ui.ViewModels.Pages;
using StarPie.Ui.ViewModels.Wheel;

namespace StarPie.Ui.Views.Navigation
{
    /// <summary>
    /// 设置台窗口：独立承担窗口职责——淡入淡出动画与界面主题应用；
    /// 页面区是 ContentControl（DataContext.CurrentViewModel），页面经 DataTemplate 由页面 VM 映射呈现。
    /// 常驻壳层不感知具体页面，也不持页面 VM 引用。落盘/托盘驻留经 <see cref="IMessenger"/> 广播
    /// 由组合根承接。
    /// </summary>
    /// <remarks>
    /// 本窗口是设置台租户的瞬态窗口：关窗即销毁（不隐藏、不保留状态），托盘驻留由常驻壳层承担，
    /// 重开时重建。常驻壳层静态文案为声明式 {DynamicResource}；Window.Title 收进
    /// <see cref="WindowChromeViewModel.WindowTitle"/>。DataContext 分区——窗口外框（本窗口）绑常驻壳层 VM，
    /// 导航区（侧栏 + 页面 ContentControl）绑 <see cref="NavigationViewModel"/>。
    /// 界面主题应用改消息驱动：订阅 <see cref="AppThemeChangedMessage"/> 执行
    /// <see cref="ApplyAppTheme"/>（配置导入后的重挂路径同样经此消息由常驻壳层执行），初始主题
    /// 仍由设置台开窗时直调本方法。
    /// </remarks>
    public partial class SettingsConsoleWindow : Window
    {
        private readonly WindowChromeViewModel _shell;
        private readonly IThemeService _themeService;

        public SettingsConsoleWindow(NavigationViewModel main, WindowChromeViewModel shell, IThemeService themeService)
        {
            InitializeComponent();
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            // 主框架分区 DataContext：窗口外框（窗口标题/底部操作区）
            // 绑常驻壳层 VM，导航区（侧栏导航项 + 当前页 ContentControl）绑导航 VM。
            DataContext = _shell;
            NavSidebar.DataContext = main ?? throw new ArgumentNullException(nameof(main));
            PageContent.DataContext = main;

            // 常驻壳层静态文案声明式化（{DynamicResource}）；Window.Title 绑定常驻壳层 VM，
            // 语言切换由 WindowChromeViewModel（本地化订阅）刷新，View 不做本地化回填。

            // 主题变更消息订阅（常驻壳层 code-behind 白名单）：界面主题子 VM 写穿配置后发布，
            // 此处执行窗口主题应用；弱引用接收，常驻壳层随窗口生命周期常驻。
            WeakReferenceMessenger.Default.Register<AppThemeChangedMessage>(this, (_, m) => ApplyAppTheme(m.AppTheme));
        }

        /// <summary>应用界面主题到主窗口（窗口视觉是常驻壳层职责：外观页切换主题与导入后同步经此调用，
        /// 页面不持 IThemeService——保持无参构造不经容器）。单一入口 SetTheme + 本窗口 DWM 应用。</summary>
        public void ApplyAppTheme(string appTheme)
        {
            _themeService.SetTheme(appTheme);
            _themeService.ApplyWindowTheme(this);
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
