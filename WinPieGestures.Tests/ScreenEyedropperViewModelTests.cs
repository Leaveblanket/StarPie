using System;
using WinPieGestures;
using Color = System.Windows.Media.Color;

namespace WinPieGestures.Tests;

/// <summary>
/// 屏上取色器 ViewModel 的行为覆盖 (T08)：拾取颜色换算、放大镜文案/色块、
/// 确认/取消关闭请求与放大镜定位纯函数。Win32 取像素留在视图，不进 VM。
/// </summary>
public sealed class ScreenEyedropperViewModelTests
{
    [Theory]
    [InlineData(255, 0, 0, "#FFFF0000")]
    [InlineData(18, 52, 86, "#FF123456")]
    [InlineData(0, 0, 0, "#FF000000")]
    public void FormatHex_IsAlwaysOpaqueARGB(byte r, byte g, byte b, string expected)
    {
        Assert.Equal(expected, ScreenEyedropperViewModel.FormatHex(r, g, b));
    }

    [Fact]
    public void TrackColor_UpdatesHexTextAndSwatch()
    {
        var vm = new ScreenEyedropperViewModel();

        vm.TrackColor(0x12, 0x34, 0x56);

        Assert.Equal("#FF123456", vm.HexText);
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), vm.SwatchBrush.Color);
    }

    [Fact]
    public void Capture_SetsCapturedHexColor_AndRaisesCloseRequestedConfirmed()
    {
        var vm = new ScreenEyedropperViewModel();
        bool? received = null;
        vm.CloseRequested += confirmed => received = confirmed;

        vm.Capture(0xAB, 0xCD, 0xEF);

        Assert.Equal("#FFABCDEF", vm.CapturedHexColor);
        Assert.True(received);
    }

    [Fact]
    public void Cancel_RaisesCloseRequestedCancelled_WithoutCapture()
    {
        var vm = new ScreenEyedropperViewModel();
        bool? received = null;
        vm.CloseRequested += confirmed => received = confirmed;

        vm.Cancel();

        Assert.False(received);
        Assert.Null(vm.CapturedHexColor);
    }

    [Fact]
    public void GetLoupePosition_FreeSpace_PlacesAtPointerBottomRight()
    {
        var (x, y) = ScreenEyedropperViewModel.GetLoupePosition(100, 100, 110, 60, 1920, 1080);

        Assert.Equal(120, x);
        Assert.Equal(120, y);
    }

    [Fact]
    public void GetLoupePosition_NearRightEdge_FlipsToLeft()
    {
        // 窗口宽 500：指针 x=490 时右下越界（490+20+110 > 500），翻到指针左上。
        var (x, y) = ScreenEyedropperViewModel.GetLoupePosition(490, 10, 110, 60, 500, 400);

        Assert.Equal(370, x); // 490 - 110 - 10
        Assert.Equal(30, y);  // 纵向未越界，保持右下偏移
    }

    [Fact]
    public void GetLoupePosition_NearBottomEdge_FlipsToTop()
    {
        var (x, y) = ScreenEyedropperViewModel.GetLoupePosition(10, 390, 110, 60, 500, 400);

        Assert.Equal(30, x);   // 横向未越界
        Assert.Equal(320, y);  // 390 - 60 - 10
    }
}
