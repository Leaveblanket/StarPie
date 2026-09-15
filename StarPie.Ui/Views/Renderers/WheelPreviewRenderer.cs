using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StarPie.ViewModels.Wheel;
using StarPie.Services.Wheel;
using StarPie.Wheel;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace StarPie.Views.Renderers
{
    /// <summary>
    /// 绘制 60FPS 实时轮盘预览。页面只持有 Canvas 并转发鼠标事件；全部视觉状态与
    /// 几何构造都留在本视图层渲染器。
    /// </summary>
    /// <remarks>
    /// 纯视觉契约：只读 VM 状态绘制，hover 坐标仅译成高亮；不订阅事件、不写 VM，
    /// 深浅色探测由调用方（外观页）以 bool 传入，模块不反向依赖宿主。
    /// 输入契约收窄为轮盘模块只读接口 <see cref="IWheelAppearanceState"/>，
    /// 不依赖具体外观聚合 VM 类型。
    /// </remarks>
    public sealed class WheelPreviewRenderer
    {
        private readonly IIconAssetService _iconAssets;
        private readonly List<System.Windows.Shapes.Path> _previewSectorPaths = new();
        private readonly List<TranslateTransform> _previewTransforms = new();
        private readonly List<double> _previewAngles = new();

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

        public WheelPreviewRenderer(IIconAssetService iconAssets)
        {
            _iconAssets = iconAssets ?? throw new ArgumentNullException(nameof(iconAssets));
        }

        public void Render(Canvas canvas, IWheelAppearanceState state, bool windowsInDarkMode)
        {
            if (canvas == null) return;

            try
            {
                canvas.Children.Clear();
                _previewSectorPaths.Clear();
                _previewTransforms.Clear();
                _previewAngles.Clear();
                _lastHoveredSector = -2;

                const double canvasSize = 300.0;
                double cx = canvasSize / 2.0;
                double cy = canvasSize / 2.0;

                double maxR = Math.Max(80.0, state.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);

                double outerR = Math.Max(30.0, state.WheelRadius * scale);
                double innerR = Math.Max(15.0, state.InnerRadius * scale);
                double coreR = Math.Max(10.0, state.CoreRadius * scale);
                double gap = Math.Max(0.0, state.SectorGap * scale);
                double cornerRadius = Math.Max(0.0, state.SectorCornerRadius * scale);

                if (innerR >= outerR) innerR = outerR * 0.5;
                if (coreR >= innerR) coreR = innerR * 0.8;

                string wheelStyle = state.WheelStyle ?? WheelStyleNames.Default;
                string palette = state.SelectedPalette ?? WheelPaletteNames.System;
                string shape = state.Shape ?? "Original";

                _previewStyleRenderer = StyleRendererFactory.CreateRenderer(wheelStyle);
                _previewStyleRenderer.Initialize(palette, state.CurrentConfig, windowsInDarkMode);
                _previewDefaultBrush = _previewStyleRenderer.DefaultSectorBrush;
                _previewHighlightBrush = _previewStyleRenderer.HighlightSectorBrush;
                _previewBorderBrush = _previewStyleRenderer.SectorBorderBrush;
                _previewHighlightBorderBrush = _previewStyleRenderer.HighlightBorderBrush;
                _previewTextBrush = _previewStyleRenderer.TextColorBrush;
                _previewCoreBgBrush = _previewStyleRenderer.CoreBgBrush;
                _previewCoreBorderBrush = _previewStyleRenderer.CoreBorderBrush;

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
                string coreType = state.CoreIconType;

                if (coreType == "Image")
                {
                    double imgSize = coreR * 1.6;
                    var coreImg = new Image
                    {
                        Width = imgSize,
                        Height = imgSize,
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Clip = new EllipseGeometry(new Point(imgSize / 2, imgSize / 2), imgSize / 2, imgSize / 2),
                        Visibility = (state.ShowCoreIcon && state.WheelStyle != WheelStyleNames.CatPaw) ? Visibility.Visible : Visibility.Collapsed
                    };
                    if (!string.IsNullOrEmpty(state.CoreCustomImagePath) && File.Exists(state.CoreCustomImagePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(state.CoreCustomImagePath, UriKind.Absolute);
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
                        Data = WheelGeometry.GetCoreIconGeometry(coreType, state.CoreCustomIconKey, state.CoreCustomIconSvg),
                        Fill = _previewTextBrush,
                        Width = exitSize,
                        Height = exitSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Visibility = (state.ShowCoreIcon && state.WheelStyle != WheelStyleNames.CatPaw) ? Visibility.Visible : Visibility.Collapsed
                    };
                    previewCoreGrid.Children.Add(_previewExitIcon);
                }

                _previewStyleRenderer.RenderDecorations(canvas, previewCoreGrid, cx, cy, outerR, coreR, 1, state.ShowCoreIcon);

                var profile = state.PreviewProfile
                    ?? new WheelProfile { SectorCount = 8, Actions = new List<ActionItem>() };
                int n = profile.SectorCount > 0 ? profile.SectorCount : 8;
                double sectorSize = 360.0 / n;

                // 排版窄字段与实轮盘同源；内容随几何一同按 scale 缩放，故预览与实轮盘等比一致。
                var layoutSpec = new WheelSectorLayoutSpec(
                    n,
                    state.IconLayoutMode,
                    state.ShowText,
                    state.SectorIconSize,
                    state.SectorFontSize);

                for (int i = 0; i < n; i++)
                {
                    double midAngle = i * sectorSize;
                    double startAngle = midAngle - (sectorSize / 2.0);
                    double endAngle = midAngle + (sectorSize / 2.0);
                    double midAngleRad = midAngle * (Math.PI / 180.0);

                    double layoutR = (innerR + outerR) / 2.0;
                    double lx = cx + Math.Cos(midAngleRad) * layoutR;
                    double ly = cy + Math.Sin(midAngleRad) * layoutR;

                    Geometry geom = WheelGeometry.CreateAdvancedSectorGeometry(
                        cx, cy, startAngle, endAngle, innerR, outerR, shape, gap, cornerRadius);

                    var transform = new TranslateTransform(0, 0);
                    var path = new System.Windows.Shapes.Path
                    {
                        Data = geom,
                        Fill = _previewDefaultBrush,
                        Stroke = _previewBorderBrush,
                        StrokeThickness = _previewStyleRenderer?.BorderThickness ?? 1.5,
                        RenderTransform = transform,
                        Tag = i
                    };

                    canvas.Children.Add(path);
                    _previewStyleRenderer?.ApplySectorHighlight(path, false);
                    _previewSectorPaths.Add(path);
                    _previewTransforms.Add(transform);
                    _previewAngles.Add(midAngleRad);

                    var sp = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        RenderTransform = transform
                    };

                    string actionName = "";
                    string iconKey = "";
                    string actionType = "Hotkey";
                    string parameter = "";
                    string customSvg = "";

                    if (profile.Actions != null && i < profile.Actions.Count && profile.Actions[i] != null)
                    {
                        actionName = profile.Actions[i].Name ?? "";
                        iconKey = profile.Actions[i].IconKey ?? "";
                        actionType = profile.Actions[i].Type ?? "Hotkey";
                        parameter = profile.Actions[i].Parameter ?? "";
                        customSvg = profile.Actions[i].CustomIconSvg ?? "";
                    }

                    // 内容与实轮盘同源：图标五级回退链、排版缩放、内置向量一律取内核输出，
                    // 预览只按自己的画布倍率等比缩放后画成元素（WYSIWYG 由这条同源路径保证）。
                    WheelSectorContent content = WheelSectorContentKernel.Build(
                        new WheelSectorInput(
                            actionName,
                            actionType,
                            parameter,
                            iconKey,
                            customSvg,
                            ResolveCustomIcon(iconKey)),
                        layoutSpec,
                        scale,
                        WheelGeometry.IsParsablePathData);

                    // 顺序契约：先图标元素、后 TextBlock，与实轮盘一致。
                    FrameworkElement? iconElement = CreateIconElement(content, _previewTextBrush);
                    if (iconElement != null)
                    {
                        sp.Children.Add(iconElement);
                    }

                    if (content.ShowText)
                    {
                        sp.Children.Add(new TextBlock
                        {
                            Text = content.Text,
                            FontSize = content.FontSize,
                            Foreground = _previewTextBrush,
                            FontWeight = FontWeights.Medium,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxWidth = content.TextMaxWidth,
                            Margin = new Thickness(0, content.TextTopMargin, 0, 0)
                        });
                    }

                    var container = new Grid
                    {
                        Width = content.ContainerWidth,
                        Height = content.ContainerHeight,
                        IsHitTestVisible = false,
                        RenderTransform = transform
                    };
                    container.Children.Add(sp);
                    Canvas.SetLeft(container, lx - container.Width / 2.0);
                    Canvas.SetTop(container, ly - container.Height / 2.0);
                    Panel.SetZIndex(container, 10);
                    canvas.Children.Add(container);
                }

                Canvas.SetLeft(previewCoreGrid, cx - coreR);
                Canvas.SetTop(previewCoreGrid, cy - coreR);
                Panel.SetZIndex(previewCoreGrid, 15);
                canvas.Children.Add(previewCoreGrid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderLiveWheelPreview Error]: {ex}");
            }
        }

        public void HandleMouseMove(Canvas canvas, MouseEventArgs e, IWheelAppearanceState state)
        {
            if (_previewSectorPaths.Count == 0) return;

            try
            {
                Point p = e.GetPosition(canvas);
                double dx = p.X - 150.0;
                double dy = p.Y - 150.0;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                double maxR = Math.Max(80.0, state.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);
                double outerR = state.WheelRadius * scale;
                double innerR = state.InnerRadius * scale;
                double coreR = state.CoreRadius * scale;

                int hoveredIndex = -2;
                if (dist <= coreR)
                {
                    hoveredIndex = -1;
                }
                else if (dist >= innerR * 0.75 && dist <= outerR * 1.2)
                {
                    double angleDeg = (Math.Atan2(dy, dx) * (180.0 / Math.PI) + 360.0) % 360.0;
                    double sectorSize = 360.0 / _previewSectorPaths.Count;
                    hoveredIndex = (int)Math.Round(angleDeg / sectorSize) % _previewSectorPaths.Count;
                }

                if (hoveredIndex == _lastHoveredSector) return;
                _lastHoveredSector = hoveredIndex;

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

        /// <summary>把内核给出的图标内容画成元素：矢量画 Path、自定义位图与程序图标画 Image。
        /// 内容为 None 或程序图标取不到时返回 null（该扇区不画图标元素）。</summary>
        private FrameworkElement? CreateIconElement(WheelSectorContent content, Brush? brush)
        {
            double bottomMargin = content.IconBottomMargin;

            switch (content.Icon.Kind)
            {
                case WheelIconKind.SvgPath:
                    return new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse(content.Icon.Data),
                        Fill = brush,
                        Width = content.IconSize,
                        Height = content.IconSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, bottomMargin)
                    };

                case WheelIconKind.CustomImageFile:
                    return new Image
                    {
                        Source = _iconAssets.GetCustomImageSource(content.Icon.Data),
                        Width = content.IconSize,
                        Height = content.IconSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, bottomMargin)
                    };

                case WheelIconKind.ProgramIcon:
                    BitmapSource? iconSource = _iconAssets.GetIcon(content.Icon.Data);
                    if (iconSource == null)
                    {
                        return null;
                    }
                    return new Image
                    {
                        Source = iconSource,
                        Width = content.IconSize,
                        Height = content.IconSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, bottomMargin)
                    };

                default:
                    return null;
            }
        }

        /// <summary>图标键命中自定义图标目录时的条目；只有 <c>custom:</c> 前缀才查目录
        /// （目录查询是服务调用，归属消费方；键前缀规则在内核）。</summary>
        private CustomIconItem? ResolveCustomIcon(string? iconKey)
            => WheelSectorContentKernel.IsCustomIconKey(iconKey)
                ? _iconAssets.GetCustomIcons().FirstOrDefault(c => c.Key == iconKey)
                : null;

        public void HandleMouseLeave()
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
    }
}
