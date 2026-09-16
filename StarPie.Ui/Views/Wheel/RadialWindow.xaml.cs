using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StarPie.Localization;
using StarPie.Services.Icons;
using StarPie.Services.Wheel;
using StarPie.Views.Renderers;
using StarPie.Wheel;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Path = System.Windows.Shapes.Path;

namespace StarPie.Views.Wheel
{
    /// <summary>
    /// 轮盘窗口：全部视图状态位于每次手势的 <see cref="WheelViewModel"/>——本类观察其
    /// 变更通知并完成绘制与动画；手势引擎从不直接调用窗口。
    /// </summary>
    public partial class RadialWindow : Window
    {
        private readonly WheelViewModel _viewModel;
        private readonly Func<bool> _windowsInDarkModeProbe;
        private readonly ILocalizationService _localization;
        private readonly IIconAssetService _iconAssets;
        private readonly List<Path> _sectorPaths = new List<Path>();
        private readonly List<StackPanel> _contentPanels = new List<StackPanel>();
        private readonly List<TranslateTransform> _sectorTransforms = new List<TranslateTransform>();
        private readonly List<TranslateTransform> _containerTransforms = new List<TranslateTransform>();
        private readonly List<double> _sectorAngles = new List<double>();
        private IRadialStyleRenderer _styleRenderer = null!;

        // 样式画刷与尺寸（动态实例化）
        private Brush _defaultSectorBrush = Brushes.Transparent;
        private Brush _highlightSectorBrush = Brushes.Transparent;
        private Brush _sectorBorderBrush = Brushes.Transparent;
        private Brush _highlightBorderBrush = Brushes.Transparent;
        private Brush _textColorBrush = Brushes.Transparent;
        private Brush _coreBgBrush = Brushes.Transparent;
        private Brush _coreBorderBrush = Brushes.Transparent;

        // 尺寸在 InitializePaletteAndStyle 由视图模型赋值，此处不留默认值（否则是第二份几何默认值）。
        private double _innerRadius;
        private double _outerRadius;
        private double _borderThickness = 1.0;
        private double _highlightBorderThickness = 1.5;

        public RadialWindow(
            WheelViewModel viewModel,
            Func<bool> windowsInDarkModeProbe,
            ILocalizationService localization,
            IIconAssetService iconAssets)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? throw new ArgumentNullException(nameof(windowsInDarkModeProbe));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _iconAssets = iconAssets ?? throw new ArgumentNullException(nameof(iconAssets));
            DataContext = viewModel;

            // 白名单订阅边界：订阅 VM PropertyChanged 只驱动纯视觉重绘与窗口生命周期动作
            // （IsShown→Show/IsClosed→Close）；在 Closed 成对退订，避免每手势窗口实例
            // 经事件被 VM 侧引用滞留。
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Closed += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            InitializePaletteAndStyle();
            CoreTextPanel.Visibility = Visibility.Collapsed;

            // 白名单（生命周期接线/纯视觉渲染）：Loaded 按 VM 只读状态一次性定位窗口并绘制扇区；
            // 订阅源为窗口自身，Close 后随窗口一起回收，不构成外部泄漏。
            Loaded += RadialWindow_Loaded;
        }

        private void InitializePaletteAndStyle()
        {
            // 经工厂实例化对应样式渲染器
            _styleRenderer = StyleRendererFactory.CreateRenderer(_viewModel.WheelStyle);
            _styleRenderer.Initialize(_viewModel.WheelPalette, _viewModel.ViewData.PaletteInput, _windowsInDarkModeProbe());

            _innerRadius = _viewModel.InnerRadius;
            _outerRadius = _viewModel.OuterRadius;

            // 从样式渲染器取画刷与尺寸
            _defaultSectorBrush = _styleRenderer.DefaultSectorBrush;
            _highlightSectorBrush = _styleRenderer.HighlightSectorBrush;
            _sectorBorderBrush = _styleRenderer.SectorBorderBrush;
            _highlightBorderBrush = _styleRenderer.HighlightBorderBrush;
            _textColorBrush = _styleRenderer.TextColorBrush;
            _coreBgBrush = _styleRenderer.CoreBgBrush;
            _coreBorderBrush = _styleRenderer.CoreBorderBrush;
            _borderThickness = _styleRenderer.BorderThickness;
            _highlightBorderThickness = _styleRenderer.HighlightBorderThickness;
        }

        private void RadialWindow_Loaded(object sender, RoutedEventArgs e)
        {
            double wheelRadius = _viewModel.OuterRadius;
            double coreRadius = _viewModel.CoreRadius;

            // 按外半径动态调整窗口尺寸
            double winSize = wheelRadius * 2.0 + 40.0; // Margin for shadow
            this.Width = winSize;
            this.Height = winSize;

            WheelCanvas.Width = winSize;
            WheelCanvas.Height = winSize;

            // 动态居中核位置
            double coreLeft = (winSize / 2.0) - coreRadius;
            double coreTop = (winSize / 2.0) - coreRadius;
            Canvas.SetLeft(CoreGrid, coreLeft);
            Canvas.SetTop(CoreGrid, coreTop);
            CoreGrid.Width = coreRadius * 2.0;
            CoreGrid.Height = coreRadius * 2.0;
            System.Windows.Controls.Panel.SetZIndex(CoreGrid, 5);

            OuterEllipse.Width = wheelRadius * 2.0 + 8.0;
            OuterEllipse.Height = wheelRadius * 2.0 + 8.0;

            // 以鼠标点击坐标为中心定位窗口（考虑 DPI 缩放）
            double scaleX = 1.0;
            double scaleY = 1.0;

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            this.Left = (_viewModel.Center.X / scaleX) - (this.Width / 2);
            this.Top = (_viewModel.Center.Y / scaleY) - (this.Height / 2);

            CoreEllipse.Fill = _coreBgBrush;
            CoreEllipse.Stroke = _coreBorderBrush;

            // 核背景图（如配置）：存在性检查与解码归图标资产服务，视图不读磁盘
            System.Windows.Media.Imaging.BitmapSource? coreBgImage =
                _iconAssets.LoadBitmap(_viewModel.ViewData.CoreBgImagePath);
            if (coreBgImage != null)
            {
                CoreEllipse.Fill = new ImageBrush(coreBgImage)
                {
                    Stretch = ParseStretch(_viewModel.ViewData.CoreBgStretch),
                    Opacity = _viewModel.ViewData.CoreBgOpacity
                };
            }

            CoreTitle.Foreground = _textColorBrush;
            CoreExitIcon.Fill = _textColorBrush;
            CoreExitIcon.Width = coreRadius * 0.42;
            CoreExitIcon.Height = coreRadius * 0.42;

            CoreTitle.FontSize = Math.Max(8.0, coreRadius / 5.0);
            CoreSubtitle.FontSize = Math.Max(6.0, coreRadius / 7.0);

            bool isCatPaw = _viewModel.WheelStyle == WheelStyleNames.CatPaw;
            bool showCoreIcon = _viewModel.ShowCoreIcon;
            string coreType = _viewModel.ViewData.CoreIconType;

            CoreTitle.Visibility = Visibility.Collapsed;
            CoreSubtitle.Visibility = Visibility.Collapsed;

            if (showCoreIcon && !isCatPaw)
            {
                // 自定义核图的存在性检查与解码同样归图标资产服务；取不到即回落核图标几何。
                System.Windows.Media.Imaging.BitmapSource? coreCustomImage = coreType == "Image"
                    ? _iconAssets.LoadBitmap(_viewModel.ViewData.CoreCustomImagePath)
                    : null;

                if (coreCustomImage != null)
                {
                    double imgSize = coreRadius * 1.6;
                    CoreCustomImage.Source = coreCustomImage;
                    CoreCustomImage.Width = imgSize;
                    CoreCustomImage.Height = imgSize;
                    CoreCustomImage.Clip = new EllipseGeometry(new Point(imgSize / 2, imgSize / 2), imgSize / 2, imgSize / 2);
                    CoreCustomImage.Visibility = Visibility.Visible;
                    CoreExitIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    CoreCustomImage.Visibility = Visibility.Collapsed;
                    var coreGeom = WheelGeometry.GetCoreIconGeometry(
                        coreType,
                        _viewModel.ViewData.CoreCustomIconKey,
                        _viewModel.ViewData.CoreCustomIconSvg);
                    if (coreGeom != null)
                    {
                        CoreExitIcon.Data = coreGeom;
                    }
                    CoreExitIcon.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CoreCustomImage.Visibility = Visibility.Collapsed;
                CoreExitIcon.Visibility = Visibility.Collapsed;
            }

            // 先绘制样式装饰
            RenderStyleDecorations();

            RenderSectors();

            // 播放打开弹性缩入与淡入动画
            var sb = new Storyboard();
            var backEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

            var scaleXAnim = new DoubleAnimation(0.65, 1.0, new Duration(TimeSpan.FromMilliseconds(110)))
            {
                EasingFunction = backEase
            };
            Storyboard.SetTarget(scaleXAnim, MainGrid);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.Children[0].ScaleX"));

            var scaleYAnim = new DoubleAnimation(0.65, 1.0, new Duration(TimeSpan.FromMilliseconds(110)))
            {
                EasingFunction = backEase
            };
            Storyboard.SetTarget(scaleYAnim, MainGrid);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.Children[0].ScaleY"));

            var opacityAnim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(90)));
            Storyboard.SetTarget(opacityAnim, MainGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(Window.OpacityProperty));

            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);
            sb.Children.Add(opacityAnim);
            sb.Begin();
        }

        private void RenderStyleDecorations()
        {
            double winSize = this.Width;
            double cx = winSize / 2.0;
            double cy = winSize / 2.0;
            double wheelRadius = _viewModel.OuterRadius;
            double coreRadius = _viewModel.CoreRadius;

            // 清除上一组样式装饰路径
            var toRemove = new List<UIElement>();
            foreach (UIElement child in WheelCanvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag is { } tag && tag.ToString()?.StartsWith("Deco_") == true)
                {
                    toRemove.Add(child);
                }
            }
            foreach (var elem in toRemove)
            {
                WheelCanvas.Children.Remove(elem);
            }

            // 重置核视觉
            CoreEllipse.Visibility = Visibility.Visible;
            OuterEllipse.Visibility = Visibility.Collapsed;

            // 移除 CoreGrid 内动态添加的网格/路径
            var gear = CoreGrid.Children.OfType<Path>().FirstOrDefault(p => p.Name == "DynamicGearPath");
            if (gear != null) CoreGrid.Children.Remove(gear);

            var paw = CoreGrid.Children.OfType<Grid>().FirstOrDefault(g => g.Name == "DynamicPawGrid");
            if (paw != null) CoreGrid.Children.Remove(paw);

            var tech = CoreGrid.Children.OfType<Grid>().FirstOrDefault(g => g.Name == "DynamicTechGrid");
            if (tech != null) CoreGrid.Children.Remove(tech);

            // 决定插入位置（位于文本面板之后）
            int insertIndex = CoreGrid.Children.IndexOf(CoreTextPanel);
            if (insertIndex < 0) insertIndex = 0;

            if (_styleRenderer != null)
            {
                _styleRenderer.RenderDecorations(WheelCanvas, CoreGrid, cx, cy, wheelRadius, coreRadius, insertIndex, _viewModel.ShowCoreIcon);
            }
        }

        private void RenderSectors()
        {
            int n = _viewModel.SectorCount;
            double sectorSize = 360.0 / n;
            double winSize = this.Width;
            double cx = winSize / 2.0;
            double cy = winSize / 2.0;

            string shape = _viewModel.ViewData.Shape;
            double gap = Math.Max(0.0, _viewModel.ViewData.SectorGap);
            double cornerRadius = Math.Max(0.0, _viewModel.ViewData.SectorCornerRadius);

            // 排版窄字段交给内核；「要不要画文字」也由内核按布局模式判定，本类不再自行组合。
            var layoutSpec = new WheelSectorLayoutSpec(
                n,
                _viewModel.ViewData.IconLayoutMode,
                _viewModel.ViewData.ShowText,
                _viewModel.ViewData.SectorIconSize,
                _viewModel.ViewData.SectorFontSize);

            _sectorPaths.Clear();
            _contentPanels.Clear();
            _sectorTransforms.Clear();
            _containerTransforms.Clear();
            _sectorAngles.Clear();

            // 清除 Canvas 上之前的扇区绘制
            var toRemove = new List<UIElement>();
            foreach (UIElement child in WheelCanvas.Children)
            {
                if (child != CoreGrid && child != OuterEllipse && !(child is FrameworkElement fe && fe.Tag is { } tag && tag.ToString()?.StartsWith("Deco_") == true))
                {
                    toRemove.Add(child);
                }
            }
            foreach (var elem in toRemove)
            {
                WheelCanvas.Children.Remove(elem);
            }

            for (int i = 0; i < n; i++)
            {
                double midAngle = i * sectorSize;
                double startAngle = midAngle - (sectorSize / 2.0);
                double endAngle = midAngle + (sectorSize / 2.0);

                double midAngleRad = midAngle * (Math.PI / 180.0);
                double layoutRadius = (_innerRadius + _outerRadius) / 2.0;
                double lx = cx + Math.Cos(midAngleRad) * layoutRadius;
                double ly = cy + Math.Sin(midAngleRad) * layoutRadius;

                Geometry geometry = WheelGeometry.CreateAdvancedSectorGeometry(
                    cx, cy, startAngle, endAngle, _innerRadius, _outerRadius, shape, gap, cornerRadius);

                var pathTransform = new TranslateTransform(0, 0);
                var path = new Path
                {
                    Data = geometry,
                    Fill = _defaultSectorBrush,
                    Stroke = _sectorBorderBrush,
                    StrokeThickness = _borderThickness,
                    RenderTransform = pathTransform,
                    Tag = i
                };
                System.Windows.Controls.Panel.SetZIndex(path, 1);

                WheelCanvas.Children.Insert(0, path);
                _styleRenderer?.ApplySectorHighlight(path, false);
                _sectorPaths.Add(path);
                _sectorTransforms.Add(pathTransform);
                _sectorAngles.Add(midAngleRad);

                // 扇区内容（图标回退链、排版缩放、内置向量）来自共享内核；本类只把纯数据画成元素。
                WheelSectorViewModel sector = _viewModel.Sectors[i];
                string actionText = sector.HasAction ? sector.Name : _localization.GetString("WheelSectorEmpty");

                WheelSectorContent content = WheelSectorContentKernel.Build(
                    new WheelSectorInput(
                        actionText,
                        sector.Type,
                        sector.Parameter,
                        sector.IconKey,
                        sector.CustomIconSvg,
                        WheelSectorIconFactory.ResolveCustomIcon(sector.IconKey, _iconAssets)),
                    layoutSpec,
                    isParsableSvg: WheelGeometry.IsParsablePathData);

                // 用网格容器保证 StackPanel 绝对居中
                var containerTransform = new TranslateTransform(0, 0);
                var container = new Grid
                {
                    Width = content.ContainerWidth,
                    Height = content.ContainerHeight,
                    RenderTransform = containerTransform
                };

                var stackPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                container.Children.Add(stackPanel);

                // 顺序契约：先图标元素、后 TextBlock——ApplySectorHighlight 按子元素顺序反查这两者，
                // 顺序颠倒会让高亮静默失效。
                FrameworkElement? iconElement = WheelSectorIconFactory.Create(content, _textColorBrush, _iconAssets);
                if (iconElement != null)
                {
                    stackPanel.Children.Add(iconElement);
                }

                if (content.ShowText)
                {
                    var textBlock = new TextBlock
                    {
                        Text = content.Text,
                        Foreground = _textColorBrush,
                        FontSize = content.FontSize,
                        FontWeight = FontWeights.Medium,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = content.TextMaxWidth,
                        MaxHeight = 28,
                        Margin = new Thickness(0, content.TextTopMargin, 0, 0),
                        Effect = (System.Windows.Media.Effects.Effect)Resources["TextShadow"]
                    };
                    stackPanel.Children.Add(textBlock);
                }

                // 网格容器居中于 (lx, ly)
                Canvas.SetLeft(container, lx - container.Width / 2.0);
                Canvas.SetTop(container, ly - container.Height / 2.0);

                System.Windows.Controls.Panel.SetZIndex(container, 10);
                WheelCanvas.Children.Add(container);
                _contentPanels.Add(stackPanel);
                _containerTransforms.Add(containerTransform);
            }
        }

        /// <summary>把引擎驱动的状态变更反映到视图（INPC 订阅边界）：窗口只经
        /// <see cref="WheelViewModel"/> 驱动——每个分支要么重绘纯视觉，要么对窗口应用
        /// 生命周期状态（IsShown/IsClosed）；从不写 VM 状态。</summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(WheelViewModel.SelectedSectorIndex):
                    ApplySectorHighlight(_viewModel.SelectedSectorIndex);
                    break;
                case nameof(WheelViewModel.IsOuterEscaped):
                    ApplyOuterEscapeState(_viewModel.IsOuterEscaped);
                    break;
                case nameof(WheelViewModel.IsShown):
                    if (_viewModel.IsShown)
                    {
                        Show();
                    }
                    break;
                case nameof(WheelViewModel.IsClosed):
                    Close();
                    break;
            }
        }

        private void ApplyOuterEscapeState(bool isEscaped)
        {
            // 视图模型只在真实状态切换时通知，因此每次调用都是状态翻转，变暗/恢复动画总会执行。
            var anim = new DoubleAnimation
            {
                To = isEscaped ? 0.38 : 1.0,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void ApplySectorHighlight(int index)
        {
            // 中心退出悬停反馈
            if (index == -1)
            {
                CoreExitIcon.Fill = new SolidColorBrush(Color.FromRgb(244, 63, 94)); // Warm rose cancel
                if (_styleRenderer != null)
                {
                    _styleRenderer.ApplyExitHighlight(CoreExitIcon, true);
                }

                var scaleAnim = new DoubleAnimation(1.12, new Duration(TimeSpan.FromMilliseconds(90)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
            else
            {
                CoreExitIcon.Fill = _textColorBrush;
                if (_styleRenderer != null)
                {
                    _styleRenderer.ApplyExitHighlight(CoreExitIcon, false);
                }

                var scaleAnim = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(90)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animDuration = new Duration(TimeSpan.FromMilliseconds(80));

            for (int i = 0; i < _sectorPaths.Count; i++)
            {
                var path = _sectorPaths[i];
                var panel = i < _contentPanels.Count ? _contentPanels[i] : null;
                var pTransform = i < _sectorTransforms.Count ? _sectorTransforms[i] : null;
                var cTransform = i < _containerTransforms.Count ? _containerTransforms[i] : null;
                double angleRad = i < _sectorAngles.Count ? _sectorAngles[i] : 0;

                TextBlock? textBlock = panel?.Children.OfType<TextBlock>().FirstOrDefault();
                Path? vectorIcon = panel?.Children.OfType<Path>().FirstOrDefault();

                if (i == index)
                {
                    path.Fill = _highlightSectorBrush;
                    path.Stroke = _highlightBorderBrush;
                    path.StrokeThickness = _highlightBorderThickness;
                    System.Windows.Controls.Panel.SetZIndex(path, 5);

                    // 磁性弹出：沿径向向量向外平移 5.5px
                    double targetX = Math.Cos(angleRad) * 5.5;
                    double targetY = Math.Sin(angleRad) * 5.5;

                    if (pTransform != null)
                    {
                        pTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(targetX, animDuration) { EasingFunction = ease });
                        pTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, animDuration) { EasingFunction = ease });
                    }
                    if (cTransform != null)
                    {
                        cTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(targetX, animDuration) { EasingFunction = ease });
                        cTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, animDuration) { EasingFunction = ease });
                    }

                    if (textBlock != null)
                    {
                        textBlock.Foreground = Brushes.White;
                        textBlock.FontWeight = FontWeights.Bold;
                    }
                    if (vectorIcon != null)
                    {
                        vectorIcon.Fill = Brushes.White;
                    }

                    if (_styleRenderer != null)
                    {
                        _styleRenderer.ApplySectorHighlight(path, true);
                    }
                }
                else
                {
                    path.Fill = _defaultSectorBrush;
                    path.Stroke = _sectorBorderBrush;
                    path.StrokeThickness = _borderThickness;
                    System.Windows.Controls.Panel.SetZIndex(path, 1);

                    // 弹性回到 (0,0)
                    if (pTransform != null)
                    {
                        pTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                        pTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                    }
                    if (cTransform != null)
                    {
                        cTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                        cTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                    }

                    if (_styleRenderer != null)
                    {
                        _styleRenderer.ApplySectorHighlight(path, false);
                    }

                    if (_textColorBrush is SolidColorBrush sc)
                    {
                        var dimColor = new SolidColorBrush(Color.FromArgb(170, sc.Color.R, sc.Color.G, sc.Color.B));
                        if (textBlock != null)
                        {
                            textBlock.Foreground = dimColor;
                            textBlock.FontWeight = FontWeights.Medium;
                        }
                        if (vectorIcon != null)
                        {
                            vectorIcon.Fill = dimColor;
                        }
                    }
                    else
                    {
                        if (textBlock != null)
                        {
                            textBlock.Foreground = _textColorBrush;
                            textBlock.FontWeight = FontWeights.Medium;
                        }
                        if (vectorIcon != null)
                        {
                            vectorIcon.Fill = _textColorBrush;
                        }
                    }
                }
            }
        }

        private static Stretch ParseStretch(string? str)
        {
            if (string.Equals(str, "Uniform", StringComparison.OrdinalIgnoreCase)) return Stretch.Uniform;
            if (string.Equals(str, "Fill", StringComparison.OrdinalIgnoreCase)) return Stretch.Fill;
            if (string.Equals(str, "None", StringComparison.OrdinalIgnoreCase)) return Stretch.None;
            return Stretch.UniformToFill;
        }
    }
}
