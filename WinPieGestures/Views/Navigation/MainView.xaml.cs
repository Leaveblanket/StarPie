using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Application = System.Windows.Application;
using WinPieGestures.ViewModels;

namespace WinPieGestures.Views.Navigation
{
    /// <summary>
    /// 设置控制台主框架 (T19)：独立承担窗口职责——关窗隐藏到托盘（含兜底冲刷）、淡入淡出动画、
    /// 壳层文本本地化、标题本地化；页面区是 ContentControl（DataContext.CurrentViewModel），
    /// 页面经 DataTemplate 由页面 VM 映射呈现。壳层不感知具体页面，也不持页面 VM 引用。
    /// 落盘/托盘驻留经 <see cref="IMessenger"/> 广播由组合根承接（RootSettingsViewModel 已删除）。
    /// </summary>
    public partial class MainView : Window
    {
        private readonly IThemeService _themeService;

        public MainView(MainViewModel main, IThemeService themeService)
        {
            InitializeComponent();
            _themeService = themeService;

            DataContext = main ?? throw new ArgumentNullException(nameof(main));

            // ADR-0002：I18n 语言切换广播——壳层文本刷新（导航项标题由主框架 VM 刷新）。
            I18n.LanguageChanged += ApplyLocalization;
            Closed += (_, _) => I18n.LanguageChanged -= ApplyLocalization;

            ApplyLocalization();
        }

        public void ApplyLocalization()
        {
            Title = I18n.T("WindowTitle") + DevInstance.Suffix;
            if (BottomNoteText != null) BottomNoteText.Text = I18n.T("BottomStatusNote");
            if (SaveButton != null) SaveButton.Content = I18n.T("BtnSave");
            if (CloseButton != null) CloseButton.Content = I18n.T("BtnClose");
        }

        /// <summary>应用界面主题到主窗口（窗口视觉是壳层职责：外观页切换主题与导入后同步经此调用，
        /// 页面不持 IThemeService——保持无参构造不经容器）。</summary>
        public void ApplyAppTheme(string theme)
        {
            _themeService.ApplyTheme(this, theme);
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
            // App-level exit (ADR-0003): pending edits were already flushed by the
            // composition root — allow the close. Any other close hides to the tray.
            if (Composition.IsExiting) return;

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
