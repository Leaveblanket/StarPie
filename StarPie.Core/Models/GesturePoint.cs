namespace WinPieGestures.Models
{
    /// <summary>UI-framework-free screen point flowing through the gesture pipeline.</summary>
    public readonly struct GesturePoint
    {
        public double X { get; }
        public double Y { get; }

        public GesturePoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
