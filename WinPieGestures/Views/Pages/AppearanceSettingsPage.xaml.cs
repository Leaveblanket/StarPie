using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 外观与形态页面 (T19/T21)：页面整体 DataContext 是薄聚合
    /// <see cref="AppearanceSettingsViewModel"/>；各设置卡 DataContext 指向对应子 VM——界面主题卡 =
    /// InterfaceTheme（#54）、其余轮盘外观卡 = WheelAppearance（#56，配色下拉 ItemsSource 化、核圆
    /// 面板可见性 DataTrigger 化、核圆图标预览/名称与图片缩略图 Converter 绑定化）。code-behind 只
    /// 保留实时预览画布渲染等 ADR-0009 白名单项（页面文本经 T24 语言字典声明式化）。页面 VM 是单例：
    /// PreviewInvalidated/PageConfigReloaded 视图消息在 Loaded/Unloaded 成对订阅退订，防过期页面引用泄漏。
    /// #54（ADR-0014 决策 6/7）：界面主题卡 DataContext 指向 <see cref="InterfaceThemeSettingsViewModel"/>
    /// （经 <see cref="AppearanceSettingsViewModel.InterfaceTheme"/> 绑定），主题应用改消息驱动——
    /// 由壳层主窗口订阅 <see cref="AppThemeChangedMessage"/> 执行，本页面不再有主题
    /// SelectionChanged 处理器；导入后的窗口主题应用路径同步移出页面。
    /// #55（ADR-0014 决策 8）：实时预览渲染/交互路径只依赖轮盘模块只读状态接口
    /// <see cref="IWheelAppearanceState"/>，不再以具体聚合 VM 类型为参数；具体聚合 VM 引用仅保留
    /// 给 DataContext 桥接（取 WheelAppearance 子 VM）——#56 起轮盘外观状态/命令已迁入
    /// <see cref="WheelAppearanceSettingsViewModel"/>（实现该接口），页面不再有任何状态读穿聚合 VM。
    /// </summary>
    public partial class AppearanceSettingsPage : SettingsPageBase
    {
        private readonly WheelPreviewRenderer _previewRenderer = new();

        // 预览桥接在 Loaded 时缓存(Unloaded 阶段 DataContext 已置空,见 SettingsPageBase 约定)为
        // IWheelAppearanceState：渲染/交互代码路径只读该接口，具体实现是聚合 VM 暴露的轮盘外观子 VM。
        private IWheelAppearanceState _previewState = null!;

        private IWheelAppearanceState PreviewState => _previewState;

        public AppearanceSettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnPageLoaded()
        {
            // 页面整体 DataContext 仍是薄聚合 VM；预览状态经其 WheelAppearance 子 VM 取得（#56）。
            _previewState = ((AppearanceSettingsViewModel)DataContext).WheelAppearance;
            WeakReferenceMessenger.Default.Register<AppearancePreviewInvalidatedMessage>(this, (_, _) => OnAppearancePreviewInvalidated());
            WeakReferenceMessenger.Default.Register<PageConfigReloadedMessage>(this, (_, m) =>
            {
                if (m.ViewModelType == typeof(AppearanceSettingsViewModel)) OnConfigReloaded();
            });

            RenderLiveWheelPreview();
        }

        protected override void OnPageUnloaded()
        {
            WeakReferenceMessenger.Default.Unregister<AppearancePreviewInvalidatedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<PageConfigReloadedMessage>(this);
            _previewState = null!;
        }

        private void OnConfigReloaded()
        {
            // ADR-0009 白名单：#54 起导入后只剩预览重绘这一 View 效果（主题应用由界面主题子 VM
            // 发 AppThemeChangedMessage、壳层主窗口订阅执行）；状态、配色下拉项与核圆面板/文本
            // 均声明式绑定，随 VM 通知自动刷新。
            RenderLiveWheelPreview();
        }

        #region 60FPS Live Preview Canvas Rendering

        private void OnAppearancePreviewInvalidated()
        {
            RenderLiveWheelPreview();
        }

        private void RenderLiveWheelPreview()
        {
            if (LiveWheelPreviewCanvas == null || _previewState == null) return;
            _previewRenderer.Render(LiveWheelPreviewCanvas, PreviewState, Window.GetWindow(this) as MainView);
        }

        private void LiveWheelPreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_previewState == null) return;
            _previewRenderer.HandleMouseMove(LiveWheelPreviewCanvas, e, PreviewState);
        }

        private void LiveWheelPreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _previewRenderer.HandleMouseLeave();
        }

        #endregion
    }
}
