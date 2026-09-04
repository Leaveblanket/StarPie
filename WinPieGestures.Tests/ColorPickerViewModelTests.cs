using System;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 颜色选择器 ViewModel 的行为覆盖 (T08)：HSV/RGB 纯函数换算、十六进制输入解析与规范化、
/// 色盘取点夹紧、确认结果与屏上取色编排（mock 对话框服务）。
/// T20 起颜色值以 <see cref="RgbColor"/> 纯模型表示（View 层经 HexToBrushConverter 转 WPF 画刷）。
/// </summary>
public sealed class ColorPickerViewModelTests
{
    private static RgbColor Rgb(byte r, byte g, byte b) => new(255, r, g, b);

    private static ColorPickerViewModel Create(TestDialogService? dialogs = null, string initialHex = "#FFFF0000")
        => new(dialogs ?? new TestDialogService(), initialHex);

    // --- HSV / RGB 纯函数 ---------------------------------------------------------

    [Theory]
    [InlineData(0, 1, 1, 255, 0, 0)]
    [InlineData(120, 1, 1, 0, 255, 0)]
    [InlineData(240, 1, 1, 0, 0, 255)]
    [InlineData(60, 1, 1, 255, 255, 0)]
    public void HsvToRgb_PrimaryAndSecondaryHues_AreExact(double h, double s, double v, byte r, byte g, byte b)
    {
        var color = ColorPickerViewModel.HsvToRgb(h, s, v);

        Assert.Equal(Rgb(r, g, b), color);
    }

    [Fact]
    public void HsvToRgb_ZeroSaturation_IsGrayOfValue()
    {
        var color = ColorPickerViewModel.HsvToRgb(0, 0, 0.5);

        Assert.Equal(Rgb(127, 127, 127), color);
    }

    [Theory]
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    public void ColorToHsv_PureHues_AreExact(byte r, byte g, byte b, double h, double s, double v)
    {
        var (outH, outS, outV) = ColorPickerViewModel.ColorToHsv(Rgb(r, g, b));

        Assert.Equal(h, outH, 5);
        Assert.Equal(s, outS, 5);
        Assert.Equal(v, outV, 5);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(120, 1, 1)]
    [InlineData(240, 1, 1)]
    public void ColorHsvRoundTrip_ByteExactColors_PreserveValues(double h, double s, double v)
    {
        var rgb = ColorPickerViewModel.HsvToRgb(h, s, v);
        var back = ColorPickerViewModel.ColorToHsv(rgb);

        Assert.Equal(h, back.Hue, 5);
        Assert.Equal(s, back.Saturation, 5);
        Assert.Equal(v, back.Value, 5);
    }

    [Fact]
    public void ColorHsvRoundTrip_NonByteExactColors_DriftStaysWithinQuantization()
    {
        // RGB 字节量化带来的固有舍入（迁移前同一算法，行为不变）：往返后色相偏差应远小于 1 度量级。
        var rgb = ColorPickerViewModel.HsvToRgb(221, 0.8, 0.9);
        var back = ColorPickerViewModel.ColorToHsv(rgb);

        Assert.InRange(back.Hue, 220.5, 221.5);
        Assert.InRange(back.Saturation, 0.79, 0.81);
        Assert.InRange(back.Value, 0.89, 0.91);
    }

    // --- 初始状态 / SetColorFromHex ---------------------------------------------------

    [Fact]
    public void SetColorFromHex_PureGreen_NormalizesToUppercaseARGB()
    {
        var vm = Create();

        vm.SetColorFromHex("#ff00ff00");

        Assert.Equal("#FF00FF00", vm.SelectedHexColor);
        Assert.Equal("#FF00FF00", vm.HexText);
    }

    [Fact]
    public void SetColorFromHex_WithoutHashPrefix_IsAccepted()
    {
        var vm = Create();

        vm.SetColorFromHex("00FF00");

        Assert.Equal("#FF00FF00", vm.SelectedHexColor);
    }

    [Fact]
    public void SetColorFromHex_InvalidText_KeepsPreviousState()
    {
        var vm = Create();
        var before = vm.SelectedHexColor;

        vm.SetColorFromHex("not-a-color");

        Assert.Equal(before, vm.SelectedHexColor);
    }

    [Fact]
    public void SetColorFromHex_Whitespace_NoChange()
    {
        var vm = Create();
        var before = vm.SelectedHexColor;

        vm.SetColorFromHex("   ");

        Assert.Equal(before, vm.SelectedHexColor);
    }

    [Fact]
    public void SetColorFromHex_RefreshesSpectrumHex()
    {
        // 色盘底色（当前色相纯色）经可观察 SpectrumHex 驱动绑定，取代旧 SpectrumChanged 事件。
        var vm = Create(initialHex: "#FF000000");

        vm.SetColorFromHex("#FF00FF00");

        Assert.Equal("#FF00FF00", vm.SpectrumHex);
        Assert.Equal("#FF00FF00", vm.PreviewHex);
    }

    // --- 十六进制输入框 ----------------------------------------------------------------

    [Fact]
    public void HexText_ValidInput_AppliesColorAndNormalizes()
    {
        var vm = Create();

        vm.HexText = "#00ff00"; // 7 位输入（旧输入框同样按长度 7/9 解析）

        Assert.Equal("#FF00FF00", vm.SelectedHexColor);
        Assert.Equal("#FF00FF00", vm.HexText);
    }

    [Fact]
    public void HexText_PartialInput_IsIgnored()
    {
        var vm = Create();
        var before = vm.SelectedHexColor;

        vm.HexText = "#FF25"; // 长度非 7/9，不解析

        Assert.Equal(before, vm.SelectedHexColor);
        Assert.Equal("#FF25", vm.HexText); // 输入框保留用户键入
    }

    // --- 色盘取点 -------------------------------------------------------------------

    [Fact]
    public void SetSpectrumPoint_MidValues_ComputesResult()
    {
        var vm = Create(); // 初始为纯红（h=0）

        vm.SetSpectrumPoint(0.5, 0.5);

        Assert.Equal(0.5, vm.Saturation);
        Assert.Equal(0.5, vm.Value);
        Assert.Equal("#FF7F3F3F", vm.SelectedHexColor);
    }

    [Fact]
    public void SetSpectrumPoint_OutOfRange_IsClampedToUnitSquare()
    {
        var vm = Create(); // 初始为纯红

        vm.SetSpectrumPoint(1.5, -0.2);

        Assert.Equal(1, vm.Saturation);
        Assert.Equal(0, vm.Value);
        Assert.Equal("#FF000000", vm.SelectedHexColor);
    }

    [Fact]
    public void SetSpectrumPointActionCommand_MidValues_ComputesResult()
    {
        // T22：附加行为翻译像素坐标后经 SetSpectrumPointActionCommand 进入（ADR-0009）。
        var vm = Create(); // 初始为纯红（h=0）

        vm.SetSpectrumPointActionCommand.Execute(new SpectrumPoint(0.5, 0.5));

        Assert.Equal(0.5, vm.Saturation);
        Assert.Equal(0.5, vm.Value);
        Assert.Equal("#FF7F3F3F", vm.SelectedHexColor);
    }

    [Fact]
    public void HueChange_UpdatesResultAndHexText()
    {
        var vm = Create(); // 初始为纯红

        vm.Hue = 120;

        Assert.Equal("#FF00FF00", vm.SelectedHexColor);
        Assert.Equal("#FF00FF00", vm.HexText);
    }

    [Fact]
    public void AlphaChange_AppliesToResult()
    {
        var vm = Create(); // 初始为不透明纯红

        vm.Alpha = 128;

        Assert.Equal("#80FF0000", vm.SelectedHexColor);
    }

    // --- 确认结果 -------------------------------------------------------------------

    [Fact]
    public void BuildResult_CarriesSelectedHexColor()
    {
        var vm = Create();

        var result = vm.BuildResult();

        Assert.NotNull(result);
        Assert.Equal(vm.SelectedHexColor, result!.HexColor);
    }

    [Fact]
    public void Confirm_SetsIsCompleted_WithCurrentColor()
    {
        // T22：确认经 ConfirmCommand/IsCompleted 驱动（ADR-0009），视图据此落 DialogResult=true。
        var vm = Create();

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.Equal(vm.SelectedHexColor, vm.BuildResult()!.HexColor);
    }

    // --- 屏上取色（mock 对话框服务） -----------------------------------------------------

    [Fact]
    public void Eyedropper_PickedColor_AppliesToCurrentColor()
    {
        var dialogs = new TestDialogService { EyedropToPick = new EyedropResult("#FF00FF00") };
        var vm = Create(dialogs: dialogs);

        vm.EyedropperCommand.Execute(null);

        Assert.Equal(1, dialogs.EyedropCalls);
        Assert.Equal("#FF00FF00", vm.SelectedHexColor);
        Assert.Equal("#FF00FF00", vm.HexText);
    }

    [Fact]
    public void Eyedropper_Cancelled_KeepsCurrentColor()
    {
        var dialogs = new TestDialogService { EyedropToPick = null };
        var vm = Create(dialogs: dialogs);
        var before = vm.SelectedHexColor;

        vm.EyedropperCommand.Execute(null);

        Assert.Equal(1, dialogs.EyedropCalls);
        Assert.Equal(before, vm.SelectedHexColor);
    }
}
