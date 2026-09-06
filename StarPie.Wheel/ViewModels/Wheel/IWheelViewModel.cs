namespace WinPieGestures.ViewModels.Wheel
{
    /// <summary>
    /// The wheel ViewModel surface the gesture engine drives (T05, ADR-0001): show,
    /// sector highlight and outer escape arrive as state mutations and the wheel
    /// window reflects them — the engine never calls window methods. Index -1 for
    /// <see cref="HighlightSector"/> clears the selection.
    /// </summary>
    public interface IWheelViewModel
    {
        void Show();

        void HighlightSector(int sectorIndex);

        void SetOuterEscapeState(bool isEscaped);

        void Close();
    }
}
