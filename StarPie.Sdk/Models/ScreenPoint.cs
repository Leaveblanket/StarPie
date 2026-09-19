namespace StarPie.Sdk.Models
{
    /// <summary>与 UI 框架无关的屏幕坐标点，沿轮盘手势识别管线流转。</summary>
    public readonly struct ScreenPoint
    {
        /// <summary>水平屏幕坐标（像素）。</summary>
        public double X { get; }

        /// <summary>垂直屏幕坐标（像素）。</summary>
        public double Y { get; }

        /// <summary>以指定像素坐标构造一个屏幕点。</summary>
        public ScreenPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
