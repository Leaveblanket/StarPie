using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.Messaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using WinPieGestures.Services;
using WinPieGestures.ViewModels;
using WinPieGestures.Views.Navigation;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 外观与形态页面 (T19)：迁移前 SettingsWindow PAGE 1 原样搬迁。页面 ViewModel 为
    /// <see cref="AppearanceSettingsViewModel"/>（容器单例，构造注入方案列表 VM 供预览读选中方案）；
    /// 60FPS 实时预览的绘制与悬停/磁吸交互留在本视图层（ADR-0001）。View 无参构造、不经容器
    /// （Spec 决策：取图/选图标对话框编排住 VM；窗口主题应用经 MainView 公开方法驱动）。
    /// 页面 VM 是单例：PreviewInvalidated/PresetListChanged/ConfigReloaded 等视图事件在
    /// Loaded/Unloaded 成对订阅退订，防过期页面引用泄漏。
    /// </summary>
    public partial class AppearanceSettingsPage : SettingsPageBase
    {
        private bool _isUpdatingUi = true;
        private readonly WheelPreviewRenderer _previewRenderer = new();

        // 页面 VM 在 Loaded 时缓存(Unloaded 阶段 DataContext 已置空,见 SettingsPageBase 约定)。
        private AppearanceSettingsViewModel _vm = null!;

        private AppearanceSettingsViewModel Vm => _vm;

        public AppearanceSettingsPage()
        {
            InitializeComponent();
        }

        protected override void ApplyLocalization()
        {
            AppearancePageHeader.Text = I18n.T("AppearanceHeader");
            AppearancePageSubheader.Text = I18n.T("AppearanceSubheader");
            CustomColorsExpanderTitleText.Text = I18n.T("CustomColorsExpanderTitle");
            CustomColorsExpanderDescText.Text = I18n.T("CustomColorsExpanderDesc");
            RenameCustomColorPresetButton.Content = I18n.T("RenameCustomPresetButton");
        }

        protected override void OnPageLoaded()
        {
            _vm = (AppearanceSettingsViewModel)DataContext;
            WeakReferenceMessenger.Default.Register<AppearancePreviewInvalidatedMessage>(this, (_, _) => OnAppearancePreviewInvalidated());
            WeakReferenceMessenger.Default.Register<AppearancePresetListChangedMessage>(this, (_, _) => SyncThemePresetItems());
            WeakReferenceMessenger.Default.Register<PageConfigReloadedMessage>(this, (_, m) =>
            {
                if (m.ViewModelType == typeof(AppearanceSettingsViewModel)) OnConfigReloaded();
            });
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.PropertyChanged += OnVmPropertyChanged;

            _isUpdatingUi = true;
            try
            {
                SyncThemePresetItems();
                UpdateCoreIconPreviewUI();
                if (Vm.CoreIconType == "Image")
                {
                    UpdateCoreImageThumbnail(Vm.CoreCustomImagePath);
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }

            RenderLiveWheelPreview();
        }

        protected override void OnPageUnloaded()
        {
            WeakReferenceMessenger.Default.Unregister<AppearancePreviewInvalidatedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<AppearancePresetListChangedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<PageConfigReloadedMessage>(this);
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null!;
        }

        private void OnConfigReloaded()
        {
            // 导入后 View 层同步（迁移前窗口 ReloadAfterConfigImport 的外观部分原样搬迁）：
            // 绑定控件随 VM 重挂自动回填，此处只做动态下拉项/主题应用/核圆 UI/预览。
            _isUpdatingUi = true;
            try
            {
                SyncThemePresetItems();
                SetComboBoxSelectedValue(ThemeComboBox, Vm.SelectedTheme);
                UpdateCoreIconPreviewUI();
            }
            finally
            {
                _isUpdatingUi = false;
            }

            (Window.GetWindow(this) as MainView)?.ApplyAppTheme(Vm.AppTheme);
            RenderLiveWheelPreview();
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // T17：核圆图片路径缩略图是 View 效果，随 VM 属性变更（绑定逐键推源）刷新。
            if (e.PropertyName == nameof(AppearanceSettingsViewModel.CoreCustomImagePath))
            {
                UpdateCoreImageThumbnail(Vm.CoreCustomImagePath);
            }
        }

        // --- 事件处理器（迁移前窗口处理器原样搬迁） ---

        /// <summary>按 VM 的预设列表重建配色方案下拉的动态项（CustomPreset_*），并恢复当前选中 Tag。</summary>
        private void SyncThemePresetItems()
        {
            if (ThemeComboBox == null) return;

            var toRemove = new List<ComboBoxItem>();
            foreach (var item in ThemeComboBox.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag != null && cbi.Tag.ToString()!.StartsWith("CustomPreset_"))
                {
                    toRemove.Add(cbi);
                }
            }
            foreach (var item in toRemove)
            {
                ThemeComboBox.Items.Remove(item);
            }

            foreach (var preset in Vm.CustomPresets)
            {
                ThemeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"🎨 {preset.Name} (自定义预设)",
                    Tag = "CustomPreset_" + preset.Id
                });
            }

            SetComboBoxSelectedValue(ThemeComboBox, Vm.SelectedTheme);
        }

        private void ShowCoreIconCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // 状态经 IsChecked 双向绑定写穿外观 VM（落盘由 VM 消息管线上报）；本处理器只剩预览重绘 View 效果。
            RenderLiveWheelPreview();
        }

        private void CoreIconTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // 状态经 SelectedValue 双向绑定写穿外观 VM；本处理器只剩核圆预览面板/图标同步 View 效果。
            UpdateCoreIconPreviewUI();
        }

        private void UpdateCoreIconPreviewUI()
        {
            string coreType = Vm.CoreIconType;
            if (CustomCoreIconPanel != null)
            {
                CustomCoreIconPanel.Visibility = coreType == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            }

            if (CustomCoreImagePanel != null)
            {
                CustomCoreImagePanel.Visibility = coreType == "Image" ? Visibility.Visible : Visibility.Collapsed;
                if (coreType == "Image")
                {
                    UpdateCoreImageThumbnail(Vm.CoreCustomImagePath);
                }
            }

            if (CustomCoreIconPreviewPath != null && CustomCoreIconNameLabel != null)
            {
                var geom = IconHelper.GetCoreIconGeometry(coreType, Vm.CoreCustomIconKey, Vm.CoreCustomIconSvg);
                CustomCoreIconPreviewPath.Data = geom;
                if (!string.IsNullOrEmpty(Vm.CoreCustomIconKey))
                {
                    CustomCoreIconNameLabel.Text = Vm.CoreCustomIconKey;
                }
                else if (!string.IsNullOrEmpty(Vm.CoreCustomIconSvg))
                {
                    CustomCoreIconNameLabel.Text = "自定义 SVG 图标";
                }
                else
                {
                    CustomCoreIconNameLabel.Text = "默认五角星 (点击更换)";
                }
            }
        }

        private void UpdateCoreImageThumbnail(string? imagePath)
        {
            if (CoreImageThumbnail == null) return;
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    CoreImageThumbnail.Source = bmp;
                }
                catch
                {
                    CoreImageThumbnail.Source = null;
                }
            }
            else
            {
                CoreImageThumbnail.Source = null;
            }
        }

        private void AppThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // 状态经 SelectedValue 双向绑定写穿外观 VM（落盘由 VM 管线上报）；主题应用（窗口视觉）是 View 效果。
            (Window.GetWindow(this) as MainView)?.ApplyAppTheme(Vm.AppTheme);
            RenderLiveWheelPreview();
        }

        #region 60FPS Live Preview Canvas Rendering

        private void OnAppearancePreviewInvalidated()
        {
            RenderLiveWheelPreview();
        }

        private void RenderLiveWheelPreview()
        {
            if (LiveWheelPreviewCanvas == null || _vm == null) return;
            _previewRenderer.Render(LiveWheelPreviewCanvas, Vm, Window.GetWindow(this) as MainView);
        }

        private void LiveWheelPreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_vm == null) return;
            _previewRenderer.HandleMouseMove(LiveWheelPreviewCanvas, e, Vm);
        }

        private void LiveWheelPreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _previewRenderer.HandleMouseLeave();
        }

        #endregion
    }
}
