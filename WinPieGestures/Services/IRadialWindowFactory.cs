namespace WinPieGestures
{
    /// <summary>
    /// The wheel surface the gesture engine interacts with (ADR-0002). The engine
    /// drives it through the factory per gesture; the first-version implementation
    /// wraps the existing RadialWindow. Index -1 for <see cref="HighlightSector"/>
    /// clears the highlight.
    /// </summary>
    public interface IRadialWindow
    {
        void Show();
        void HighlightSector(int sectorIndex);
        void SetOuterEscapeState(bool isEscaped);
        void Close();
    }

    /// <summary>
    /// Creates a transient wheel per gesture (every gesture gets a fresh one).
    /// Implementations own the UI-thread marshaling; callers may be on the hook thread.
    /// </summary>
    public interface IRadialWindowFactory
    {
        IRadialWindow Create(GesturePoint center, WheelProfile profile);
    }
}
