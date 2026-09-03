using System;
using CommunityToolkit.Mvvm.ComponentModel;
using WinPieGestures.Models;

namespace WinPieGestures.ViewModels.Dialogs
{
    /// <summary>
    /// 屏上取色器 ViewModel (T08, ADR-0004)：拾取颜色 → 十六进制换算、放大镜文案/色块与
    /// 关闭请求在此；Win32 取像素与全屏窗口摆放留在视图（操作系统互操作，不进 VM）。
    /// </summary>
    public partial class ScreenEyedropperViewModel : ObservableObject
    {
        /// <summary>放大镜上的十六进制文案（#FFRRGGBB，始终不透明）。</summary>
        [ObservableProperty]
        private string _hexText = "#FFFFFF";

        /// <summary>放大镜色块颜色（当前像素颜色）；View 经 HexToBrushConverter 转为画刷。</summary>
        [ObservableProperty]
        private string _swatchHex = "#00000000";

        /// <summary>左键捕获的颜色；右键/Esc 取消时保持 null。视图据此落 DialogResult。</summary>
        public string? CapturedHexColor { get; private set; }

        /// <summary>鼠标移动：报告当前像素颜色，更新放大镜文案与色块。</summary>
        public void TrackColor(byte r, byte g, byte b)
        {
            HexText = FormatHex(r, g, b);
            SwatchHex = new RgbColor(255, r, g, b).ToHex();
        }

        /// <summary>左键单击：捕获当前像素颜色并请求确认关闭。</summary>
        public bool Capture(byte r, byte g, byte b)
        {
            CapturedHexColor = FormatHex(r, g, b);
            return true;
        }

        /// <summary>右键 / Esc：取消取色（不产生结果）。</summary>
        public bool Cancel() => false;

        /// <summary>像素 → #FFRRGGBB（与迁移前一致，始终不透明）。</summary>
        public static string FormatHex(byte r, byte g, byte b) => $"#FF{r:X2}{g:X2}{b:X2}";

        /// <summary>
        /// 放大镜位置：优先指针右下 20px，越出窗口则翻到左上（与迁移前一致）。
        /// 指针坐标为窗口相对坐标，窗口尺寸不足时同样按迁移前的翻转规则处理。
        /// </summary>
        public static (double X, double Y) GetLoupePosition(
            double pointerX, double pointerY,
            double loupeWidth, double loupeHeight,
            double windowWidth, double windowHeight)
        {
            double x = pointerX + 20;
            double y = pointerY + 20;

            if (x + loupeWidth > windowWidth) x = pointerX - loupeWidth - 10;
            if (y + loupeHeight > windowHeight) y = pointerY - loupeHeight - 10;

            return (x, y);
        }
    }
}
