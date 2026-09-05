using System;
using System.Windows;
using System.Windows.Media;

namespace WinPieGestures.Tests;

/// <summary>
/// 轮盘视觉几何出口（M2）纯几何推导覆盖（T3a/#65，R6/ADR-0015）：扇区切削形态
/// （Original 标准扇区 / Circle / HexagonHive / RoundedCapsule 系）与中心核图标几何
/// （内置类型、Custom 的 SVG 键回退与星形回落）；旧入口委托一致性随附验证。
/// </summary>
public sealed class WheelGeometryTests
{
    private const double Tolerance = 1e-6;

    private static void AssertPointEqual(double expectedX, double expectedY, Point actual)
    {
        Assert.True(Math.Abs(expectedX - actual.X) <= Tolerance, $"X 期望 {expectedX}，实际 {actual.X}");
        Assert.True(Math.Abs(expectedY - actual.Y) <= Tolerance, $"Y 期望 {expectedY}，实际 {actual.Y}");
    }

    private static void AssertBoundsEqual(string expectedSvg, Geometry actual)
    {
        Rect expected = Geometry.Parse(expectedSvg).Bounds;
        Rect bounds = actual.Bounds;
        Assert.True(Math.Abs(expected.X - bounds.X) <= Tolerance, $"Bounds.X 期望 {expected.X}，实际 {bounds.X}");
        Assert.True(Math.Abs(expected.Y - bounds.Y) <= Tolerance, $"Bounds.Y 期望 {expected.Y}，实际 {bounds.Y}");
        Assert.True(Math.Abs(expected.Width - bounds.Width) <= Tolerance, $"Bounds.Width 期望 {expected.Width}，实际 {bounds.Width}");
        Assert.True(Math.Abs(expected.Height - bounds.Height) <= Tolerance, $"Bounds.Height 期望 {expected.Height}，实际 {bounds.Height}");
    }

    [Fact]
    public void CreateAdvancedSectorGeometry_CircleShape_ReturnsCenteredEllipse()
    {
        // mid=45°、layoutR=50：中心 = (100 + 50·cos45, 100 + 50·sin45)。
        var ellipse = Assert.IsType<EllipseGeometry>(
            WheelGeometry.CreateAdvancedSectorGeometry(100, 100, 0, 90, 40, 60, "Circle"));

        AssertPointEqual(135.35533905932738, 135.35533905932738, ellipse.Center);
        Assert.Equal(9.4, ellipse.RadiusX, 6);
        Assert.Equal(9.4, ellipse.RadiusY, 6);
    }

    [Fact]
    public void CreateAdvancedSectorGeometry_HexagonHive_ReturnsRotatedClosedHexagon()
    {
        var path = Assert.IsType<PathGeometry>(
            WheelGeometry.CreateAdvancedSectorGeometry(100, 100, 0, 90, 40, 60, "HexagonHive"));

        PathFigure figure = Assert.Single(path.Figures);
        Assert.True(figure.IsClosed);
        Assert.Equal(5, figure.Segments.Count);
        Assert.All(figure.Segments, s => Assert.IsType<LineSegment>(s));

        var rotate = Assert.IsType<RotateTransform>(path.Transform);
        Assert.Equal(45.0, rotate.Angle, 6);
    }

    [Fact]
    public void CreateAdvancedSectorGeometry_RoundedRect_ReturnsRoundedRectangle()
    {
        var rect = Assert.IsType<RectangleGeometry>(
            WheelGeometry.CreateAdvancedSectorGeometry(100, 100, 0, 90, 40, 60, "RoundedRect", cornerRadius: 6));

        Assert.True(rect.RadiusX > 0);
        Assert.True(rect.RadiusY > 0);
        Assert.IsType<RotateTransform>(rect.Transform);
    }

    [Fact]
    public void CreateAdvancedSectorGeometry_OriginalShape_ReturnsStandardSectorPath()
    {
        var path = Assert.IsType<PathGeometry>(
            WheelGeometry.CreateAdvancedSectorGeometry(100, 100, 0, 90, 40, 60, "Original"));

        PathFigure figure = Assert.Single(path.Figures);
        Assert.True(figure.IsClosed);
        Assert.Equal(3, figure.Segments.Count);
        Assert.IsType<ArcSegment>(figure.Segments[0]);
        Assert.IsType<LineSegment>(figure.Segments[1]);
        Assert.IsType<ArcSegment>(figure.Segments[2]);

        // start=0°、outerR=60：起点 = (100 + 60, 100)。
        AssertPointEqual(160, 100, figure.StartPoint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetCoreIconGeometry_EmptyType_FallsBackToExit(string? coreIconType)
    {
        AssertBoundsEqual(
            "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z",
            WheelGeometry.GetCoreIconGeometry(coreIconType));
    }

    [Theory]
    [InlineData("Crosshair")]
    [InlineData("Windows")]
    [InlineData("Dot")]
    [InlineData("Bullseye")]
    [InlineData("Home")]
    [InlineData("Power")]
    [InlineData("Compass")]
    [InlineData("CatPaw")]
    [InlineData("Exit")]
    public void GetCoreIconGeometry_BuiltInTypes_ReturnNonEmptyGeometry(string type)
    {
        Geometry geometry = WheelGeometry.GetCoreIconGeometry(type);

        Assert.NotNull(geometry);
        Assert.True(geometry.Bounds.Width > 0);
        Assert.True(geometry.Bounds.Height > 0);
    }

    [Fact]
    public void GetCoreIconGeometry_CustomWithSvg_UsesProvidedPath()
    {
        const string customSvg = "M1,1 L10,1 L10,10 Z";

        AssertBoundsEqual(customSvg, WheelGeometry.GetCoreIconGeometry("Custom", customSvg: customSvg));
    }

    [Fact]
    public void GetCoreIconGeometry_CustomWithKey_FallsBackToCatalogSvg()
    {
        string folderSvg = IconAssets.GetSvgPathByKey("Folder")!;

        AssertBoundsEqual(folderSvg, WheelGeometry.GetCoreIconGeometry("Custom", customKey: "Folder"));
    }

    [Fact]
    public void GetCoreIconGeometry_CustomWithoutMatch_FallsBackToStar()
    {
        const string starSvg = "M12,2L15.09,8.26L22,9.27L17,14.14L18.18,21.02L12,17.77L5.82,21.02L7,14.14L2,9.27L8.91,8.26L12,2Z";

        AssertBoundsEqual(starSvg, WheelGeometry.GetCoreIconGeometry("Custom", customKey: "NoSuchKey", customSvg: "not-a-path"));
    }

    [Fact]
    public void LegacyIconHelper_DelegatesToWheelGeometry()
    {
        Assert.Equal(
            WheelGeometry.CreateAdvancedSectorGeometry(100, 100, 0, 90, 40, 60, "Original").GetType(),
            IconHelper.CreateAdvancedSectorGeometry(100, 100, 0, 90, 40, 60, "Original").GetType());
        AssertBoundsEqual(
            "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z",
            IconHelper.GetCoreIconGeometry(null));
    }
}
