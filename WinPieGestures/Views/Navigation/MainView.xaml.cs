using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using Application = System.Windows.Application;
using WinPieGestures.ViewModels;

namespace WinPieGestures.Views.Navigation
{
    /// <summary>
    /// 设置控制台主框架 (T19)：独立承担窗口职责——关窗隐藏到托盘（含兜底冲刷）、淡入淡出动画；
    /// 页面区是 ContentControl（DataContext.CurrentViewModel），页面经 DataTemplate 由页面 VM 映射呈现。
    /// 壳层不感知具体页面，也不持页面 VM 引用。落盘/托盘驻留经 <see cref="IMessenger"/> 广播由组合根承接
    /// （RootSettingsViewModel 已删除）。壳层静态文案为声明式 {DynamicResource}（ADR-0010），
    /// Window.Title 收进 <see cref="MainViewModel.WindowTitle"/>（壳层 VM 生命周期，ADR-0010 第 3 条）。
    /// #54（ADR-0014 决策 7）：界面主题应用改消息驱动——订阅 <see cref="AppThemeChangedMessage"/>
    /// 执行 <see cref="ApplyAppTheme"/>（页面主题 SelectionChanged 处理器已删除；配置导入后重挂
    /// 路径同样经此消息由壳层执行），初始主题仍由 AppHost.Run 直调本方法。
    public partial class MainView : Window
    {
        private readonly MainViewModel _main;
        private readonly IThemeService _themeService;

        public MainView(MainViewModel main, IThemeService themeService)
        {
            InitializeComponent();
            _main = main ?? throw new ArgumentNullException(nameof(main));
            _themeService = themeService;

            DataContext = _main;

            // ADR-0010：壳层静态文案声明式化（{DynamicResource}）；Window.Title 绑定壳层 VM，
            // 语言切换由 MainViewModel（I18n 订阅）刷新，View 不再回填本地化。

            // #54：主题变更消息订阅（壳层 code-behind 白名单，ADR-0009）——界面主题子 VM 写穿
            // 配置后发布，此处执行窗口主题应用；消息接收方为弱引用，壳层随窗口生命周期常驻。
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

        /// <summary>显示并激活主窗口（托盘直达/双击；迁移前 ShowSettings(int) 的窗口部分——
        /// 页面切换改由类型化导航服务先行完成），淡入动画原样保留。</summary>
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
            // App-level exit (ADR-0003, #27): pending edits were already flushed by the
            // composition root — allow the close. Exit state lives on the shell VM
            // (MainViewModel.IsExiting), so the View has no reverse dependency on Composition.
            if (_main.IsExiting) return;

            e.Cancel = true;

            // Fade out before hiding
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
