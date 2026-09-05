using System;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace WinPieGestures.Services.Wheel
{
    /// <summary>
    /// 轮盘视觉几何出口（模块 M2「轮盘与渲染」，R6/ADR-0015 三分）：扇区切削几何
    /// （<c>CreateAdvancedSectorGeometry</c>）与中心核图标几何（<c>GetCoreIconGeometry</c>）。
    /// 几何成员唯一消费方是轮盘 RadialWindow / WheelPreviewRenderer / CoreIconGeometryConverter，
    /// 自 T3b/#66 起直连本出口；旧入口自 T3a 扩展阶段委托本出口并保留至 T3d/#68 收口删除。
    /// 核图标 Custom 分支按 SVG 键回退取值时消费共享「图标资产」（S1）目录。
    /// </summary>
    public static class WheelGeometry
    {
        #region Geometry Creation Helpers for Advanced Shapes

        public static Geometry CreateAdvancedSectorGeometry(
            double cx, double cy,
            double startAngle, double endAngle,
            double innerR, double outerR,
            string shape, double gap = 0, double cornerRadius = 0)
        {
            double midAngle = (startAngle + endAngle) / 2.0;
            double midAngleRad = midAngle * (Math.PI / 180.0);
            double layoutR = (innerR + outerR) / 2.0;
            double lx = cx + Math.Cos(midAngleRad) * layoutR;
            double ly = cy + Math.Sin(midAngleRad) * layoutR;
            double sectorAngleSpan = Math.Abs(endAngle - startAngle);
            double sectorHalfSpanRad = (sectorAngleSpan / 2.0) * (Math.PI / 180.0);

            // Distance between adjacent sector centers along the chord
            double chord = 2.0 * layoutR * Math.Sin(sectorHalfSpanRad);
            double radialSpan = Math.Max(12.0, (outerR - innerR) - gap);
            double tangentialSpan = Math.Max(12.0, chord - gap);

            if (shape == "Circle")
            {
                double diameter = Math.Max(12.0, Math.Min(radialSpan, tangentialSpan) * 0.94);
                return new EllipseGeometry(new Point(lx, ly), diameter / 2.0, diameter / 2.0);
            }
            else if (shape == "HexagonHive")
            {
                double hexRadius = Math.Max(8.0, Math.Min(radialSpan / 2.0, (tangentialSpan / Math.Sqrt(3.0))) * 0.94);
                var hexGeom = CreateHexagonGeometry(lx, ly, hexRadius);
                hexGeom.Transform = new RotateTransform(midAngle, lx, ly);
                return hexGeom;
            }
            else if (shape == "RoundedCapsule" || shape == "FloatingCapsules" || shape == "RoundedRect" || shape == "Capsule")
            {
                double w = Math.Max(16.0, radialSpan * 0.96);
                double h = Math.Max(16.0, Math.Min(w * 0.82, tangentialSpan * 0.88));
                double r = cornerRadius > 0 ? Math.Min(h / 2.0, cornerRadius + 2.0) : Math.Min(h / 2.0, 10.0);
                var rectGeom = new RectangleGeometry(new Rect(lx - w / 2.0, ly - h / 2.0, w, h), r, r);
                rectGeom.Transform = new RotateTransform(midAngle, lx, ly);
                return rectGeom;
            }
            else
            {
                // Standard or Optical Gap/Fillet Sector
                double effStartAngle = startAngle;
                double effEndAngle = endAngle;

                if (gap > 0 && layoutR > 0)
                {
                    double angularGap = (gap / layoutR) * (180.0 / Math.PI);
                    if (angularGap < sectorAngleSpan * 0.6)
                    {
                        effStartAngle += angularGap / 2.0;
                        effEndAngle -= angularGap / 2.0;
                    }
                }

                return CreateStandardSectorGeometry(cx, cy, effStartAngle, effEndAngle, innerR, outerR);
            }
        }

        private static Geometry CreateHexagonGeometry(double cx, double cy, double radius)
        {
            var figure = new PathFigure { IsClosed = true, IsFilled = true };
            for (int i = 0; i < 6; i++)
            {
                double a = i * 60.0 * (Math.PI / 180.0);
                Point pt = new Point(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a));
                if (i == 0) figure.StartPoint = pt;
                else figure.Segments.Add(new LineSegment(pt, true));
            }
            var geom = new PathGeometry();
            geom.Figures.Add(figure);
            return geom;
        }

        private static Geometry CreateStandardSectorGeometry(double cx, double cy, double startAngle, double endAngle, double innerRadius, double outerRadius)
        {
            double startRad = startAngle * (Math.PI / 180.0);
            double endRad = endAngle * (Math.PI / 180.0);

            Point p1 = new Point(cx + Math.Cos(startRad) * outerRadius, cy + Math.Sin(startRad) * outerRadius);
            Point p2 = new Point(cx + Math.Cos(endRad) * outerRadius, cy + Math.Sin(endRad) * outerRadius);
            Point p3 = new Point(cx + Math.Cos(endRad) * innerRadius, cy + Math.Sin(endRad) * innerRadius);
            Point p4 = new Point(cx + Math.Cos(startRad) * innerRadius, cy + Math.Sin(startRad) * innerRadius);

            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180.0;

            var figure = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
            figure.Segments.Add(new ArcSegment(p2, new Size(Math.Max(1.0, outerRadius), Math.Max(1.0, outerRadius)), 0, isLargeArc, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(p3, true));
            figure.Segments.Add(new ArcSegment(p4, new Size(Math.Max(1.0, innerRadius), Math.Max(1.0, innerRadius)), 0, isLargeArc, SweepDirection.Counterclockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        #endregion

        #region Center Core Icon Geometries

        public static Geometry GetCoreIconGeometry(string? coreIconType, string? customKey = null, string? customSvg = null)
        {
            string type = string.IsNullOrEmpty(coreIconType) ? "Exit" : coreIconType;

            switch (type)
            {
                case "Crosshair":
                    return Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M11,6V11H6V13H11V18H13V13H18V11H13V6H11Z");

                case "Windows":
                    return Geometry.Parse("M3,12V6.75L9,5.92V12M20,3V12H10V5.78L20,3M3,13H9V18.08L3,17.25M10,13H20V21L10,18.22");

                case "Dot":
                case "Bullseye":
                    return Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7Z");

                case "Home":
                    return Geometry.Parse("M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z");

                case "Power":
                    return Geometry.Parse("M16.56,5.44L15.11,6.89C16.84,8.14 18,10.16 18,12.5A6,6 0 0,1 12,18.5A6,6 0 0,1 6,12.5C6,10.16 7.16,8.14 8.89,6.89L7.44,5.44C5.36,6.99 4,9.59 4,12.5A8,8 0 0,0 12,20.5A8,8 0 0,0 20,12.5C20,9.59 18.64,6.99 16.56,5.44M13,3H11V13H13V3Z");

                case "Compass":
                    return Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M14.19,14.19L6,18L9.81,9.81L18,6L14.19,14.19M12,10.9A1.1,1.1 0 0,0 10.9,12A1.1,1.1 0 0,0 12,13.1A1.1,1.1 0 0,0 13.1,12A1.1,1.1 0 0,0 12,10.9Z");

                case "CatPaw":
                    return Geometry.Parse("M12,14.5C10.5,14.5 9,15.5 8.5,17C8,18.5 9,20 10.5,20.5C11.5,20.8 12.5,20.8 13.5,20.5C15,20 16,18.5 15.5,17C15,15.5 13.5,14.5 12,14.5M6,12.5A2,2 0 0,0 4,14.5A2,2 0 0,0 6,16.5A2,2 0 0,0 8,14.5A2,2 0 0,0 6,12.5M18,12.5A2,2 0 0,0 16,14.5A2,2 0 0,0 18,16.5A2,2 0 0,0 20,14.5A2,2 0 0,0 18,12.5M9.5,8.5A2,2 0 0,0 7.5,10.5A2,2 0 0,0 9.5,12.5A2,2 0 0,0 11.5,10.5A2,2 0 0,0 9.5,8.5M14.5,8.5A2,2 0 0,0 12.5,10.5A2,2 0 0,0 14.5,12.5A2,2 0 0,0 16.5,10.5A2,2 0 0,0 14.5,8.5Z");

                case "Custom":
                    if (!string.IsNullOrEmpty(customSvg))
                    {
                        try { return Geometry.Parse(customSvg); } catch { }
                    }
                    if (!string.IsNullOrEmpty(customKey))
                    {
                        string? data = IconAssets.GetSvgPathByKey(customKey);
                        if (!string.IsNullOrEmpty(data))
                        {
                            try { return Geometry.Parse(data); } catch { }
                        }
                    }
                    return Geometry.Parse("M12,2L15.09,8.26L22,9.27L17,14.14L18.18,21.02L12,17.77L5.82,21.02L7,14.14L2,9.27L8.91,8.26L12,2Z"); // Star

                case "Exit":
                default:
                    return Geometry.Parse("M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z");
            }
        }

        #endregion
    }
}
