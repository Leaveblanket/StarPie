namespace WinPieGestures.Services.Gestures
{
    /// <summary>
    /// Creates a transient wheel (view-model plus its window) per gesture — every
    /// gesture gets a fresh pair (ADR-0002). Implementations own the UI-thread
    /// marshaling; callers may be on the hook thread.
    /// </summary>
    public interface IWheelFactory
    {
        IWheelViewModel Create(GesturePoint center, WheelProfile profile);
    }
}
