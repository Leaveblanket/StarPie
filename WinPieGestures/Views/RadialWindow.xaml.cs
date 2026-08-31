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
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Size = System.Windows.Size;
using Brushes = System.Windows.Media.Brushes;
using Path = System.Windows.Shapes.Path;

namespace WinPieGestures.Views
{
    /// <summary>
    /// 轮盘窗口 (T05): all view state lives in the per-gesture <see cref="WheelViewModel"/>
    /// — this class observes its change notifications and performs the drawing and
    /// animations; the gesture engine never calls into it.
    /// </summary>
    public partial class RadialWindow : Window
    {
        private readonly WheelViewModel _viewModel;
        private readonly IThemeService _themeService;
        private readonly List<Path> _sectorPaths = new List<Path>();
        private readonly List<StackPanel> _contentPanels = new List<StackPanel>();
        private readonly List<TranslateTransform> _sectorTransforms = new List<TranslateTransform>();
        private readonly List<TranslateTransform> _containerTransforms = new List<TranslateTransform>();
        private readonly List<double> _sectorAngles = new List<double>();
        private IRadialStyleRenderer _styleRenderer;

        // Styling brushes and dimensions (instantiated dynamically)
        private Brush _defaultSectorBrush;
        private Brush _highlightSectorBrush;
        private Brush _sectorBorderBrush;
        private Brush _highlightBorderBrush;
        private Brush _textColorBrush;
        private Brush _coreBgBrush;
        private Brush _coreBorderBrush;

        private double _innerRadius = 52;
        private double _outerRadius = 138;
        private double _borderThickness = 1.0;
        private double _highlightBorderThickness = 1.5;

        public RadialWindow(WheelViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _themeService = themeService;
            DataContext = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            InitializeThemeAndStyle();
            CoreTextPanel.Visibility = Visibility.Collapsed;

            // Load event to position the window and render sectors
            Loaded += RadialWindow_Loaded;

            CoreTitle.Text = _viewModel.CoreTitle;
            CoreSubtitle.Text = _viewModel.CoreSubtitle;
        }

        private void InitializeThemeAndStyle()
        {
            // Instantiate corresponding style renderer using the factory
            _styleRenderer = StyleRendererFactory.CreateRenderer(_viewModel.UiStyle);
            _styleRenderer.Initialize(_viewModel.Theme, _viewModel.Config, _themeService.IsWindowsInDarkTheme());

            _innerRadius = _viewModel.InnerRadius;
            _outerRadius = _viewModel.OuterRadius;

            // Fetch brushes and dimensions from style renderer
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

            // Adjust window size dynamically based on outer radius
            double winSize = wheelRadius * 2.0 + 40.0; // Margin for shadow
            this.Width = winSize;
            this.Height = winSize;

            WheelCanvas.Width = winSize;
            WheelCanvas.Height = winSize;

            // Center core position dynamically
            double coreLeft = (winSize / 2.0) - coreRadius;
            double coreTop = (winSize / 2.0) - coreRadius;
            Canvas.SetLeft(CoreGrid, coreLeft);
            Canvas.SetTop(CoreGrid, coreTop);
            CoreGrid.Width = coreRadius * 2.0;
            CoreGrid.Height = coreRadius * 2.0;
            System.Windows.Controls.Panel.SetZIndex(CoreGrid, 5);

            // Outer Ellipse size
            OuterEllipse.Width = wheelRadius * 2.0 + 8.0;
            OuterEllipse.Height = wheelRadius * 2.0 + 8.0;

            // Position the window centered on the mouse click coordinates, accounting for DPI scaling
            double scaleX = 1.0;
            double scaleY = 1.0;

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Set window position in WPF units
            this.Left = (_viewModel.Center.X / scaleX) - (this.Width / 2);
            this.Top = (_viewModel.Center.Y / scaleY) - (this.Height / 2);

            // Apply core brushes
            CoreEllipse.Fill = _coreBgBrush;
            CoreEllipse.Stroke = _coreBorderBrush;

            // Render Core Background Image / Avatar (if configured)
            string coreBgPath = _viewModel.Config.CoreBgImagePath ?? "";
            if (!string.IsNullOrEmpty(coreBgPath) && System.IO.File.Exists(coreBgPath))
            {
                try
                {
                    var coreImg = new System.Windows.Media.Imaging.BitmapImage(new Uri(coreBgPath, UriKind.Absolute));
                    CoreEllipse.Fill = new ImageBrush(coreImg)
                    {
                        Stretch = ParseStretch(_viewModel.Config.CoreBgStretch),
                        Opacity = _viewModel.Config.CoreBgOpacity
                    };
                }
                catch { }
            }

            CoreTitle.Foreground = _textColorBrush;
            CoreExitIcon.Fill = _textColorBrush;
            CoreExitIcon.Width = coreRadius * 0.42;
            CoreExitIcon.Height = coreRadius * 0.42;

            CoreTitle.FontSize = Math.Max(8.0, coreRadius / 5.0);
            CoreSubtitle.FontSize = Math.Max(6.0, coreRadius / 7.0);

            bool isCatPaw = _viewModel.UiStyle == "CatPaw";
            bool showCoreIcon = _viewModel.ShowCoreIcon;
            string coreType = _viewModel.Config.CoreIconType ?? "Exit";

            CoreTitle.Visibility = Visibility.Collapsed;
            CoreSubtitle.Visibility = Visibility.Collapsed;

            if (showCoreIcon && !isCatPaw)
            {
                if (coreType == "Image" && !string.IsNullOrEmpty(_viewModel.Config.CoreCustomImagePath) && File.Exists(_viewModel.Config.CoreCustomImagePath))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(_viewModel.Config.CoreCustomImagePath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();

                        double imgSize = coreRadius * 1.6;
                        CoreCustomImage.Source = bmp;
                        CoreCustomImage.Width = imgSize;
                        CoreCustomImage.Height = imgSize;
                        CoreCustomImage.Clip = new EllipseGeometry(new Point(imgSize / 2, imgSize / 2), imgSize / 2, imgSize / 2);
                        CoreCustomImage.Visibility = Visibility.Visible;
                        CoreExitIcon.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        CoreCustomImage.Visibility = Visibility.Collapsed;
                        CoreExitIcon.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    CoreCustomImage.Visibility = Visibility.Collapsed;
                    var coreGeom = IconHelper.GetCoreIconGeometry(
                        coreType,
                        _viewModel.Config.CoreCustomIconKey,
                        _viewModel.Config.CoreCustomIconSvg);
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

            // Render style decorations first
            RenderStyleDecorations();

            RenderSectors();

            // Run open spring scale-in and fade-in animation
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

            // Clear previous style decoration paths
            var toRemove = new List<UIElement>();
            foreach (UIElement child in WheelCanvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag != null && fe.Tag.ToString().StartsWith("Deco_"))
                {
                    toRemove.Add(child);
                }
            }
            foreach (var elem in toRemove)
            {
                WheelCanvas.Children.Remove(elem);
            }

            // Reset core visuals
            CoreEllipse.Visibility = Visibility.Visible;
            OuterEllipse.Visibility = Visibility.Collapsed;

            // Remove any dynamically added grids or paths inside CoreGrid
            var gear = CoreGrid.Children.OfType<Path>().FirstOrDefault(p => p.Name == "DynamicGearPath");
            if (gear != null) CoreGrid.Children.Remove(gear);

            var paw = CoreGrid.Children.OfType<Grid>().FirstOrDefault(g => g.Name == "DynamicPawGrid");
            if (paw != null) CoreGrid.Children.Remove(paw);

            var tech = CoreGrid.Children.OfType<Grid>().FirstOrDefault(g => g.Name == "DynamicTechGrid");
            if (tech != null) CoreGrid.Children.Remove(tech);

            // Determine insert position behind text panel
            int insertIndex = CoreGrid.Children.IndexOf(CoreTextPanel);
            if (insertIndex < 0) insertIndex = 0;

            // Render style decorations via the style renderer
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

            string shape = _viewModel.Config.Shape ?? "Original";
            double gap = Math.Max(0.0, _viewModel.Config.SectorGap);
            double cornerRadius = Math.Max(0.0, _viewModel.Config.SectorCornerRadius);
            string layoutMode = _viewModel.Config.IconLayoutMode ?? "IconAndText";
            bool showText = _viewModel.Config.ShowText && layoutMode != "IconOnly";

            _sectorPaths.Clear();
            _contentPanels.Clear();
            _sectorTransforms.Clear();
            _containerTransforms.Clear();
            _sectorAngles.Clear();

            // Clear previous sector drawings from Canvas
            var toRemove = new List<UIElement>();
            foreach (UIElement child in WheelCanvas.Children)
            {
                if (child != CoreGrid && child != OuterEllipse && !(child is FrameworkElement fe && fe.Tag != null && fe.Tag.ToString().StartsWith("Deco_")))
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

                Geometry geometry = IconHelper.CreateAdvancedSectorGeometry(
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

                // Grid Container to ensure absolute centering of StackPanel
                double containerW = n == 12 ? 58.0 : (n == 4 ? 96.0 : 84.0);
                double containerH = n == 12 ? 48.0 : (n == 4 ? 72.0 : 64.0);

                var containerTransform = new TranslateTransform(0, 0);
                var container = new Grid
                {
                    Width = containerW,
                    Height = containerH,
                    RenderTransform = containerTransform
                };

                var stackPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                container.Children.Add(stackPanel);

                WheelSectorViewModel sector = _viewModel.Sectors[i];
                string actionText = sector.HasAction ? sector.Name : "未设置";
                string actionType = sector.Type;
                string parameter = sector.Parameter;
                string iconKey = sector.IconKey;
                string customSvg = sector.CustomIconSvg;

                FrameworkElement? iconElement = null;

                if (layoutMode != "TextOnly")
                {
                    double configuredIconSize = _viewModel.Config.SectorIconSize > 0 ? _viewModel.Config.SectorIconSize : 20.0;
                    double scaleFactor = n == 12 ? 0.82 : (n == 4 ? 1.20 : 1.0);
                    double baseIconSize = (layoutMode == "IconOnly") ? configuredIconSize * 1.35 : configuredIconSize;
                    double iconSize = baseIconSize * scaleFactor;

                    if (!string.IsNullOrEmpty(customSvg))
                    {
                        try
                        {
                            iconElement = new Path
                            {
                                Data = Geometry.Parse(customSvg),
                                Fill = _textColorBrush,
                                Stretch = Stretch.Uniform,
                                Width = iconSize,
                                Height = iconSize,
                                Margin = new Thickness(0, 0, 0, showText ? 2 : 0),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                            };
                        }
                        catch { }
                    }

                    if (iconElement == null && !string.IsNullOrEmpty(iconKey))
                    {
                        if (iconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                        {
                            var custom = IconHelper.GetCustomIcons().FirstOrDefault(c => c.Key == iconKey);
                            if (custom != null)
                            {
                                if (custom.IsSvg)
                                {
                                    iconElement = new Path
                                    {
                                        Data = Geometry.Parse(custom.SvgData),
                                        Fill = _textColorBrush,
                                        Stretch = Stretch.Uniform,
                                        Width = iconSize,
                                        Height = iconSize,
                                        Margin = new Thickness(0, 0, 0, showText ? 2 : 0),
                                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                                    };
                                }
                                else
                                {
                                    var img = new System.Windows.Controls.Image
                                    {
                                        Width = iconSize,
                                        Height = iconSize,
                                        Stretch = Stretch.Uniform,
                                        Margin = new Thickness(0, 0, 0, showText ? 2 : 0),
                                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                                    };
                                    img.Source = IconHelper.GetCustomImageSource(custom.FilePath);
                                    iconElement = img;
                                }
                            }
                        }
                        else
                        {
                            string? svgData = IconHelper.GetSvgPathByKey(iconKey);
                            if (!string.IsNullOrEmpty(svgData))
                            {
                                iconElement = new Path
                                {
                                    Data = Geometry.Parse(svgData),
                                    Fill = _textColorBrush,
                                    Stretch = Stretch.Uniform,
                                    Width = iconSize,
                                    Height = iconSize,
                                    Margin = new Thickness(0, 0, 0, showText ? 2 : 0),
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                                };
                            }
                        }
                    }

                    if (iconElement == null && actionType == "Launch" && !string.IsNullOrEmpty(parameter))
                    {
                        System.Windows.Media.Imaging.BitmapSource? iconSrc = IconHelper.GetIcon(parameter);
                        if (iconSrc != null)
                        {
                            iconElement = new System.Windows.Controls.Image
                            {
                                Source = iconSrc,
                                Width = iconSize + 4,
                                Height = iconSize + 4,
                                Stretch = Stretch.Uniform,
                                Margin = new Thickness(0, 0, 0, showText ? 2 : 0),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                            };
                        }
                    }

                    if (iconElement == null)
                    {
                        string? pathData = GetVectorIconPath(actionType, parameter);
                        if (!string.IsNullOrEmpty(pathData))
                        {
                            iconElement = new Path
                            {
                                Data = Geometry.Parse(pathData),
                                Fill = _textColorBrush,
                                Stretch = Stretch.Uniform,
                                Width = iconSize,
                                Height = iconSize,
                                Margin = new Thickness(0, 0, 0, showText ? 2 : 0),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                            };
                        }
                    }

                    if (iconElement != null)
                    {
                        stackPanel.Children.Add(iconElement);
                    }
                }

                if (showText && !string.IsNullOrEmpty(actionText))
                {
                    double baseFontSize = _viewModel.Config.SectorFontSize > 0 ? _viewModel.Config.SectorFontSize : 10.5;
                    double actualFontSize = (layoutMode == "TextOnly") ? baseFontSize + 1.0 : baseFontSize;
                    if (n == 12) actualFontSize = Math.Min(actualFontSize, 9.5);
                    else if (n == 4) actualFontSize = Math.Max(actualFontSize, 11.5);

                    double textMaxW = n == 12 ? 50.0 : (n == 4 ? 90.0 : 78.0);

                    var textBlock = new TextBlock
                    {
                        Text = actionText,
                        Foreground = _textColorBrush,
                        FontSize = actualFontSize,
                        FontWeight = FontWeights.Medium,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = textMaxW,
                        MaxHeight = 28,
                        Margin = new Thickness(0, 1, 0, 0),
                        Effect = (System.Windows.Media.Effects.Effect)Resources["TextShadow"]
                    };
                    stackPanel.Children.Add(textBlock);
                }

                // Center Grid Container on (lx, ly)
                Canvas.SetLeft(container, lx - container.Width / 2.0);
                Canvas.SetTop(container, ly - container.Height / 2.0);

                System.Windows.Controls.Panel.SetZIndex(container, 10);
                WheelCanvas.Children.Add(container);
                _contentPanels.Add(stackPanel);
                _containerTransforms.Add(containerTransform);
            }
        }

        private string? GetVectorIconPath(string type, string parameter)
        {
            if (type == "Folder" || type == "OpenFolder")
            {
                return IconHelper.GetSvgPathByKey("Folder");
            }

            if (type == "Hotkey")
            {
                // Keyboard icon
                return "M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z";
            }

            if (type == "System" && !string.IsNullOrEmpty(parameter))
            {
                switch (parameter.Trim().ToLower())
                {
                    case "lock":
                        return IconHelper.GetSvgPathByKey("Lock");
                    case "volumeup":
                        return IconHelper.GetSvgPathByKey("VolumeUp");
                    case "volumedown":
                        return IconHelper.GetSvgPathByKey("VolumeDown");
                    case "volumemute":
                        return IconHelper.GetSvgPathByKey("VolumeMute");
                    case "showdesktop":
                        return IconHelper.GetSvgPathByKey("ShowDesktop");
                    case "screenshot":
                        return IconHelper.GetSvgPathByKey("Screenshot");
                }
            }

            return null;
        }

        private Geometry CreateSectorGeometry(double startAngleDegrees, double endAngleDegrees, double innerRadius, double outerRadius)
        {
            double startRad = startAngleDegrees * (Math.PI / 180.0);
            double endRad = endAngleDegrees * (Math.PI / 180.0);

            double cx = this.Width / 2.0;
            double cy = this.Height / 2.0;

            Point p1 = new Point(cx + Math.Cos(startRad) * outerRadius, cy + Math.Sin(startRad) * outerRadius);
            Point p2 = new Point(cx + Math.Cos(endRad) * outerRadius, cy + Math.Sin(endRad) * outerRadius);
            Point p3 = new Point(cx + Math.Cos(endRad) * innerRadius, cy + Math.Sin(endRad) * innerRadius);
            Point p4 = new Point(cx + Math.Cos(startRad) * innerRadius, cy + Math.Sin(startRad) * innerRadius);

            bool isLargeArc = Math.Abs(endAngleDegrees - startAngleDegrees) > 180.0;

            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(p1, isFilled: true, isClosed: true);
                ctx.ArcTo(p2, new Size(outerRadius, outerRadius), 0, isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
                ctx.LineTo(p3, isStroked: true, isSmoothJoin: false);
                ctx.ArcTo(p4, new Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);
                ctx.LineTo(p1, isStroked: true, isSmoothJoin: false);
            }
            geometry.Freeze();
            return geometry;
        }

        /// <summary>Reflects engine-driven state mutations onto the view (T05): the
        /// window is only ever driven through the <see cref="WheelViewModel"/>.</summary>
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
                    MemoryOptimizer.TrimMemory();
                    break;
            }
        }

        private void ApplyOuterEscapeState(bool isEscaped)
        {
            // The view-model only raises a change on real transitions, so every call
            // here is a state flip and the dim/restored animation always applies.
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
            // Center Exit Hover Feedback
            if (index == -1)
            {
                CoreExitIcon.Fill = new SolidColorBrush(Color.FromRgb(244, 63, 94)); // Warm rose cancel
                if (_styleRenderer != null)
                {
                    _styleRenderer.ApplyExitHighlight(CoreExitIcon, true);
                }

                // Animate CoreScale up
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

                // Animate CoreScale back to normal
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

                TextBlock textBlock = panel?.Children.OfType<TextBlock>().FirstOrDefault();
                Path vectorIcon = panel?.Children.OfType<Path>().FirstOrDefault();

                if (i == index)
                {
                    path.Fill = _highlightSectorBrush;
                    path.Stroke = _highlightBorderBrush;
                    path.StrokeThickness = _highlightBorderThickness;
                    System.Windows.Controls.Panel.SetZIndex(path, 5);

                    // Magnetic pop-out: Translate outward by 5.5px along the radial vector
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

                    // Spring back to 0,0
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
