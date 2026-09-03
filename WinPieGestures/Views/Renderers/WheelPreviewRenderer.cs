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
using WinPieGestures.ViewModels.Pages;
using WinPieGestures.Views.Navigation;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace WinPieGestures.Views.Renderers
{
    /// <summary>
    /// Draws the 60FPS live wheel preview. The page owns only the Canvas and forwards
    /// mouse events; all visual state and geometry construction stays in this View-layer renderer.
    /// </summary>
    public sealed class WheelPreviewRenderer
    {
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

        public void Render(Canvas canvas, AppearanceSettingsViewModel vm, MainView? shell)
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

                double maxR = Math.Max(80.0, vm.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);

                double outerR = Math.Max(30.0, vm.WheelRadius * scale);
                double innerR = Math.Max(15.0, vm.InnerRadius * scale);
                double coreR = Math.Max(10.0, vm.CoreRadius * scale);
                double gap = Math.Max(0.0, vm.SectorGap * scale);
                double cornerRadius = Math.Max(0.0, vm.SectorCornerRadius * scale);

                if (innerR >= outerR) innerR = outerR * 0.5;
                if (coreR >= innerR) coreR = innerR * 0.8;

                string uiStyle = vm.UiStyle ?? "ClassicRing";
                string theme = vm.SelectedTheme ?? "System";
                string shape = vm.Shape ?? "Original";
                string layoutMode = vm.IconLayoutMode ?? "IconAndText";
                bool showText = vm.ShowText && layoutMode != "IconOnly";

                _previewStyleRenderer = StyleRendererFactory.CreateRenderer(uiStyle);
                _previewStyleRenderer.Initialize(theme, vm.CurrentConfig, shell?.IsWindowsInDarkTheme() ?? false);
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
                string coreType = vm.CoreIconType;

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
                        Visibility = (vm.ShowCoreIcon && vm.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    if (!string.IsNullOrEmpty(vm.CoreCustomImagePath) && File.Exists(vm.CoreCustomImagePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(vm.CoreCustomImagePath, UriKind.Absolute);
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
                        Data = IconHelper.GetCoreIconGeometry(coreType, vm.CoreCustomIconKey, vm.CoreCustomIconSvg),
                        Fill = _previewTextBrush,
                        Width = exitSize,
                        Height = exitSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Visibility = (vm.ShowCoreIcon && vm.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    previewCoreGrid.Children.Add(_previewExitIcon);
                }

                _previewStyleRenderer.RenderDecorations(canvas, previewCoreGrid, cx, cy, outerR, coreR, 1, vm.ShowCoreIcon);

                var profile = vm.ProfileList.SelectedProfile?.Model
                    ?? vm.ProfileList.Profiles.FirstOrDefault()?.Model
                    ?? new WheelProfile { SectorCount = 8, Actions = new List<ActionItem>() };
                int n = profile.SectorCount > 0 ? profile.SectorCount : 8;
                double sectorSize = 360.0 / n;

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

                        double configuredIconSize = vm.SectorIconSize > 0 ? vm.SectorIconSize : 20.0;
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
                                    HorizontalAlignment = HorizontalAlignment.Center,
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
                                var img = new Image
                                {
                                    Source = iconSrc,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = HorizontalAlignment.Center,
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
                                var img = new Image
                                {
                                    Source = iconSrc,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(img);
                            }
                        }
                        else
                        {
                            try
                            {
                                var iconPath = new System.Windows.Shapes.Path
                                {
                                    Data = Geometry.Parse("M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z"),
                                    Fill = _previewTextBrush,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(iconPath);
                            }
                            catch { }
                        }
                    }

                    if (showText && !string.IsNullOrEmpty(actionName))
                    {
                        double baseFontSize = vm.SectorFontSize > 0 ? vm.SectorFontSize : 10.5;
                        double scaleFactor = n == 12 ? 0.80 : (n == 4 ? 1.20 : 1.0);
                        double previewFs = ((layoutMode == "TextOnly") ? baseFontSize + 1.0 : baseFontSize) * 0.85 * scaleFactor;
                        double textMaxW = n == 12 ? 44.0 : (n == 4 ? 76.0 : 64.0);

                        var tb = new TextBlock
                        {
                            Text = actionName,
                            FontSize = Math.Max(6.5, previewFs),
                            Foreground = _previewTextBrush,
                            FontWeight = FontWeights.Medium,
                            HorizontalAlignment = HorizontalAlignment.Center,
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

        public void HandleMouseMove(Canvas canvas, MouseEventArgs e, AppearanceSettingsViewModel vm)
        {
            if (_previewSectorPaths.Count == 0) return;

            try
            {
                Point p = e.GetPosition(canvas);
                double dx = p.X - 150.0;
                double dy = p.Y - 150.0;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                double maxR = Math.Max(80.0, vm.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);
                double outerR = vm.WheelRadius * scale;
                double innerR = vm.InnerRadius * scale;
                double coreR = vm.CoreRadius * scale;

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
