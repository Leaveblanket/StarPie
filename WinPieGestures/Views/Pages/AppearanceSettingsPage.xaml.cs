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
        private bool _isRenderingPreview = false;

        // Preview rendering tracking
        private readonly List<System.Windows.Shapes.Path> _previewSectorPaths = new List<System.Windows.Shapes.Path>();
        private readonly List<TranslateTransform> _previewTransforms = new List<TranslateTransform>();
        private readonly List<double> _previewAngles = new List<double>();
        private IRadialStyleRenderer? _previewStyleRenderer;
        private Brush? _previewDefaultBrush;
        private Brush? _previewHighlightBrush;
        private Brush? _previewBorderBrush;
        private Brush? _previewHighlightBorderBrush;
        private Brush? _previewTextBrush;
        private Brush? _previewCoreBgBrush;
        private Brush? _previewCoreBorderBrush;
        private Ellipse? _previewCoreCircle;
        private System.Windows.Shapes.Path? _previewExitIcon;
        private int _lastHoveredSector = -2;

        private AppearanceSettingsViewModel Vm => (AppearanceSettingsViewModel)DataContext;

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
            Vm.PreviewInvalidated += OnAppearancePreviewInvalidated;
            Vm.PresetListChanged += SyncThemePresetItems;
            Vm.ConfigReloaded += OnConfigReloaded;
            Vm.PropertyChanged += OnVmPropertyChanged;

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
            Vm.PreviewInvalidated -= OnAppearancePreviewInvalidated;
            Vm.PresetListChanged -= SyncThemePresetItems;
            Vm.ConfigReloaded -= OnConfigReloaded;
            Vm.PropertyChanged -= OnVmPropertyChanged;
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

        private void PickCoreIconButton_Click(object sender, RoutedEventArgs e)
        {
            // 对话框与写回编排进外观 VM（T16 PickCoreIcon），落盘由 VM 消息上报；此处只剩 View 层效果。
            if (Vm.PickCoreIcon())
            {
                UpdateCoreIconPreviewUI();
                RenderLiveWheelPreview();
            }
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

        private void BrowseCoreImageButton_Click(object sender, RoutedEventArgs e)
        {
            // 取图对话框编排住外观 VM（T19 收编，页面保持无参构造）；缩略图随 VM PropertyChanged 刷新，
            // 预览重绘与落盘由该属性管线发出。
            if (Vm.BrowseCoreImage())
            {
                UpdateCoreImageThumbnail(Vm.CoreCustomImagePath);
            }
        }

        private void ClearCoreImageButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.CoreCustomImagePath = "";
        }

        private void PickIcon_Click(object sender, RoutedEventArgs e)
        {
            // 图标选取对话框编排已迁 SlotViewModel (T12)；此处只剩 View 层效果：
            // 图标变化影响轮盘预览（本页可见即当前页）。
            if (sender is FrameworkElement elem && elem.DataContext is SlotViewModel vm && vm.PickIcon())
            {
                RenderLiveWheelPreview();
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

        /// <summary>外观设置变化（VM PreviewInvalidated）→ 重绘实时预览（页面挂载中即本页可见）。</summary>
        private void OnAppearancePreviewInvalidated()
        {
            RenderLiveWheelPreview();
        }

        private void RenderLiveWheelPreview()
        {
            if (_isRenderingPreview || LiveWheelPreviewCanvas == null) return;
            _isRenderingPreview = true;

            try
            {
                LiveWheelPreviewCanvas.Children.Clear();
                _previewSectorPaths.Clear();
                _previewTransforms.Clear();
                _previewAngles.Clear();
                _lastHoveredSector = -2;

                // 预览输入（当前外观设置值）从外观 ViewModel 读取；选中方案经构造注入的方案列表 VM 读取。
                double canvasSize = 300.0;
                double cx = canvasSize / 2.0;
                double cy = canvasSize / 2.0;

                double maxR = Math.Max(80.0, Vm.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);

                double outerR = Math.Max(30.0, Vm.WheelRadius * scale);
                double innerR = Math.Max(15.0, Vm.InnerRadius * scale);
                double coreR = Math.Max(10.0, Vm.CoreRadius * scale);
                double gap = Math.Max(0.0, Vm.SectorGap * scale);
                double cornerRadius = Math.Max(0.0, Vm.SectorCornerRadius * scale);

                if (innerR >= outerR) innerR = outerR * 0.5;
                if (coreR >= innerR) coreR = innerR * 0.8;

                string uiStyle = Vm.UiStyle ?? "ClassicRing";
                string theme = Vm.SelectedTheme ?? "System";
                string shape = Vm.Shape ?? "Original";
                string layoutMode = Vm.IconLayoutMode ?? "IconAndText";
                bool showText = Vm.ShowText && layoutMode != "IconOnly";

                _previewStyleRenderer = StyleRendererFactory.CreateRenderer(uiStyle);
                var shell = Window.GetWindow(this) as MainView;
                _previewStyleRenderer.Initialize(theme, Vm.CurrentConfig, shell?.IsWindowsInDarkTheme() ?? false);
                _previewDefaultBrush = _previewStyleRenderer.DefaultSectorBrush;
                _previewHighlightBrush = _previewStyleRenderer.HighlightSectorBrush;
                _previewBorderBrush = _previewStyleRenderer.SectorBorderBrush;
                _previewHighlightBorderBrush = _previewStyleRenderer.HighlightBorderBrush;
                _previewTextBrush = _previewStyleRenderer.TextColorBrush;
                _previewCoreBgBrush = _previewStyleRenderer.CoreBgBrush;
                _previewCoreBorderBrush = _previewStyleRenderer.CoreBorderBrush;

                // Prepare preview core grid and render style decorations
                var previewCoreGrid = new Grid
                {
                    Width = coreR * 2.0,
                    Height = coreR * 2.0
                };
                _previewCoreCircle = new Ellipse
                {
                    Width = coreR * 2.0,
                    Height = coreR * 2.0,
                    Fill = _previewCoreBgBrush,
                    Stroke = _previewCoreBorderBrush,
                    StrokeThickness = 1.5
                };
                previewCoreGrid.Children.Add(_previewCoreCircle);

                double exitSize = Math.Max(12, coreR * 0.42);
                string coreType = Vm.CoreIconType;

                if (coreType == "Image")
                {
                    double imgSize = coreR * 1.6;
                    var coreImg = new System.Windows.Controls.Image
                    {
                        Width = imgSize,
                        Height = imgSize,
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Clip = new EllipseGeometry(new Point(imgSize / 2, imgSize / 2), imgSize / 2, imgSize / 2),
                        Visibility = (Vm.ShowCoreIcon && Vm.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    if (!string.IsNullOrEmpty(Vm.CoreCustomImagePath) && File.Exists(Vm.CoreCustomImagePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(Vm.CoreCustomImagePath, UriKind.Absolute);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            coreImg.Source = bmp;
                        }
                        catch { }
                    }
                    previewCoreGrid.Children.Add(coreImg);
                }
                else
                {
                    _previewExitIcon = new System.Windows.Shapes.Path
                    {
                        Name = "CoreExitIcon",
                        Data = IconHelper.GetCoreIconGeometry(
                            coreType,
                            Vm.CoreCustomIconKey,
                            Vm.CoreCustomIconSvg),
                        Fill = _previewTextBrush,
                        Width = exitSize,
                        Height = exitSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Visibility = (Vm.ShowCoreIcon && Vm.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    previewCoreGrid.Children.Add(_previewExitIcon);
                }

                _previewStyleRenderer.RenderDecorations(LiveWheelPreviewCanvas, previewCoreGrid, cx, cy, outerR, coreR, 1, Vm.ShowCoreIcon);

                var profile = Vm.ProfileList.SelectedProfile?.Model
                    ?? Vm.ProfileList.Profiles.FirstOrDefault()?.Model
                    ?? new WheelProfile { SectorCount = 8, Actions = new List<ActionItem>() };
                int n = profile.SectorCount > 0 ? profile.SectorCount : 8;
                double sectorSize = 360.0 / n;

                // Draw sectors
                for (int i = 0; i < n; i++)
                {
                    double midAngle = i * sectorSize;
                    double startAngle = midAngle - (sectorSize / 2.0);
                    double endAngle = midAngle + (sectorSize / 2.0);
                    double midAngleRad = midAngle * (Math.PI / 180.0);

                    double layoutR = (innerR + outerR) / 2.0;
                    double lx = cx + Math.Cos(midAngleRad) * layoutR;
                    double ly = cy + Math.Sin(midAngleRad) * layoutR;

                    Geometry geom = IconHelper.CreateAdvancedSectorGeometry(
                        cx, cy, startAngle, endAngle, innerR, outerR, shape, gap, cornerRadius);

                    var transform = new TranslateTransform(0, 0);
                    var path = new System.Windows.Shapes.Path
                    {
                        Data = geom,
                        Fill = _previewDefaultBrush,
                        Stroke = _previewBorderBrush,
                        StrokeThickness = _previewStyleRenderer.BorderThickness,
                        RenderTransform = transform,
                        Tag = i
                    };

                    LiveWheelPreviewCanvas.Children.Add(path);
                    _previewStyleRenderer?.ApplySectorHighlight(path, false);
                    _previewSectorPaths.Add(path);
                    _previewTransforms.Add(transform);
                    _previewAngles.Add(midAngleRad);

                    // Add icon & mini text inside sector
                    var sp = new StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        RenderTransform = transform
                    };

                    string actionName = "";
                    string iconKey = "";
                    string actionType = "Hotkey";
                    string parameter = "";

                    if (profile.Actions != null && i < profile.Actions.Count && profile.Actions[i] != null)
                    {
                        actionName = profile.Actions[i].Name ?? "";
                        iconKey = profile.Actions[i].IconKey ?? "";
                        actionType = profile.Actions[i].Type ?? "Hotkey";
                        parameter = profile.Actions[i].Parameter ?? "";
                    }

                    if (layoutMode != "TextOnly")
                    {
                        string? customSvg = (profile.Actions != null && i < profile.Actions.Count) ? profile.Actions[i]?.CustomIconSvg : null;
                        string? svgData = null;

                        IconHelper.CustomIconItem? customItem = null;
                        if (!string.IsNullOrEmpty(iconKey) && iconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                        {
                            customItem = IconHelper.GetCustomIcons().FirstOrDefault(c => c.Key == iconKey);
                        }

                        if (!string.IsNullOrEmpty(customSvg)) svgData = customSvg;
                        else if (customItem != null && customItem.IsSvg) svgData = customItem.SvgData;
                        else if (!string.IsNullOrEmpty(iconKey) && customItem == null) svgData = IconHelper.GetSvgPathByKey(iconKey);
                        else if (actionType == "Folder" || actionType == "OpenFolder") svgData = IconHelper.GetSvgPathByKey("Folder");
                        else if (actionType == "System" && !string.IsNullOrEmpty(parameter)) svgData = IconHelper.GetSvgPathByKey(parameter);

                        double configuredIconSize = Vm.SectorIconSize > 0 ? Vm.SectorIconSize : 20.0;
                        double scaleFactor = n == 12 ? 0.80 : (n == 4 ? 1.20 : 1.0);
                        double previewIconSize = ((layoutMode == "IconOnly") ? configuredIconSize * 1.35 : configuredIconSize) * 0.72 * scaleFactor;

                        if (!string.IsNullOrEmpty(svgData))
                        {
                            try
                            {
                                var iconPath = new System.Windows.Shapes.Path
                                {
                                    Data = Geometry.Parse(svgData),
                                    Fill = _previewTextBrush,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(iconPath);
                            }
                            catch { }
                        }
                        else if (customItem != null && !customItem.IsSvg)
                        {
                            var iconSrc = IconHelper.GetCustomImageSource(customItem.FilePath);
                            if (iconSrc != null)
                            {
                                var img = new System.Windows.Controls.Image
                                {
                                    Source = iconSrc,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(img);
                            }
                        }
                        else if (actionType == "Launch" && !string.IsNullOrEmpty(parameter))
                        {
                            var iconSrc = IconHelper.GetIcon(parameter);
                            if (iconSrc != null)
                            {
                                var img = new System.Windows.Controls.Image
                                {
                                    Source = iconSrc,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(img);
                            }
                        }
                        else
                        {
                            // Hotkey keyboard fallback icon
                            try
                            {
                                var iconPath = new System.Windows.Shapes.Path
                                {
                                    Data = Geometry.Parse("M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z"),
                                    Fill = _previewTextBrush,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(iconPath);
                            }
                            catch { }
                        }
                    }

                    if (showText && !string.IsNullOrEmpty(actionName))
                    {
                        double baseFontSize = Vm.SectorFontSize > 0 ? Vm.SectorFontSize : 10.5;
                        double scaleFactor = n == 12 ? 0.80 : (n == 4 ? 1.20 : 1.0);
                        double previewFs = ((layoutMode == "TextOnly") ? baseFontSize + 1.0 : baseFontSize) * 0.85 * scaleFactor;
                        double textMaxW = n == 12 ? 44.0 : (n == 4 ? 76.0 : 64.0);

                        var tb = new TextBlock
                        {
                            Text = actionName,
                            FontSize = Math.Max(6.5, previewFs),
                            Foreground = _previewTextBrush,
                            FontWeight = FontWeights.Medium,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxWidth = textMaxW
                        };
                        sp.Children.Add(tb);
                    }

                    double containerW = n == 12 ? 46.0 : (n == 4 ? 76.0 : 60.0);
                    double containerH = n == 12 ? 38.0 : (n == 4 ? 54.0 : 44.0);

                    var container = new Grid
                    {
                        Width = containerW,
                        Height = containerH,
                        IsHitTestVisible = false,
                        RenderTransform = transform
                    };
                    container.Children.Add(sp);
                    Canvas.SetLeft(container, lx - containerW / 2.0);
                    Canvas.SetTop(container, ly - containerH / 2.0);
                    System.Windows.Controls.Panel.SetZIndex(container, 10);
                    LiveWheelPreviewCanvas.Children.Add(container);
                }

                // Position and add preview core grid
                Canvas.SetLeft(previewCoreGrid, cx - coreR);
                Canvas.SetTop(previewCoreGrid, cy - coreR);
                System.Windows.Controls.Panel.SetZIndex(previewCoreGrid, 15);
                LiveWheelPreviewCanvas.Children.Add(previewCoreGrid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderLiveWheelPreview Error]: {ex}");
            }
            finally
            {
                _isRenderingPreview = false;
            }
        }

        private void LiveWheelPreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_previewSectorPaths.Count == 0) return;

            try
            {
                Point p = e.GetPosition(LiveWheelPreviewCanvas);
                double dx = p.X - 150.0;
                double dy = p.Y - 150.0;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                double maxR = Math.Max(80.0, Vm.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);
                double outerR = Vm.WheelRadius * scale;
                double innerR = Vm.InnerRadius * scale;
                double coreR = Vm.CoreRadius * scale;

                int hoveredIndex = -2;

                if (dist <= coreR)
                {
                    hoveredIndex = -1; // Core hovered
                }
                else if (dist >= innerR * 0.75 && dist <= outerR * 1.2)
                {
                    double angleDeg = (Math.Atan2(dy, dx) * (180.0 / Math.PI) + 360.0) % 360.0;
                    double sectorSize = 360.0 / _previewSectorPaths.Count;
                    hoveredIndex = (int)Math.Round(angleDeg / sectorSize) % _previewSectorPaths.Count;
                }

                if (hoveredIndex == _lastHoveredSector) return;
                _lastHoveredSector = hoveredIndex;

                // Update sector highlights & magnetic pop-out
                for (int i = 0; i < _previewSectorPaths.Count; i++)
                {
                    var path = _previewSectorPaths[i];
                    var trans = _previewTransforms[i];
                    double angleRad = _previewAngles[i];

                    if (i == hoveredIndex)
                    {
                        path.Fill = _previewHighlightBrush;
                        path.Stroke = _previewHighlightBorderBrush;
                        path.StrokeThickness = (_previewStyleRenderer?.HighlightBorderThickness ?? 2.0);
                        _previewStyleRenderer?.ApplySectorHighlight(path, true);

                        // Pop out
                        trans.X = Math.Cos(angleRad) * 4.5;
                        trans.Y = Math.Sin(angleRad) * 4.5;
                    }
                    else
                    {
                        path.Fill = _previewDefaultBrush;
                        path.Stroke = _previewBorderBrush;
                        path.StrokeThickness = (_previewStyleRenderer?.BorderThickness ?? 1.5);
                        _previewStyleRenderer?.ApplySectorHighlight(path, false);

                        trans.X = 0;
                        trans.Y = 0;
                    }
                }

                // Core highlight
                if (_previewCoreCircle != null)
                {
                    if (hoveredIndex == -1)
                    {
                        _previewCoreCircle.Fill = new SolidColorBrush(Color.FromArgb(220, 244, 63, 94));
                        if (_previewExitIcon != null)
                        {
                            _previewExitIcon.Fill = Brushes.White;
                            _previewStyleRenderer?.ApplyExitHighlight(_previewExitIcon, true);
                        }
                    }
                    else
                    {
                        _previewCoreCircle.Fill = _previewCoreBgBrush;
                        if (_previewExitIcon != null)
                        {
                            _previewExitIcon.Fill = _previewTextBrush;
                            _previewStyleRenderer?.ApplyExitHighlight(_previewExitIcon, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Canvas MouseMove Error]: {ex}");
            }
        }

        private void LiveWheelPreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                _lastHoveredSector = -2;
                for (int i = 0; i < _previewSectorPaths.Count; i++)
                {
                    var path = _previewSectorPaths[i];
                    var trans = _previewTransforms[i];

                    path.Fill = _previewDefaultBrush;
                    path.Stroke = _previewBorderBrush;
                    path.StrokeThickness = (_previewStyleRenderer?.BorderThickness ?? 1.5);
                    _previewStyleRenderer?.ApplySectorHighlight(path, false);
                    trans.X = 0;
                    trans.Y = 0;
                }

                if (_previewCoreCircle != null) _previewCoreCircle.Fill = _previewCoreBgBrush;
                if (_previewExitIcon != null) _previewExitIcon.Fill = _previewTextBrush;
            }
            catch { }
        }

        #endregion
    }
}
