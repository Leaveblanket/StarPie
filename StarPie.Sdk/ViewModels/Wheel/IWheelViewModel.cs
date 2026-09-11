namespace StarPie.ViewModels.Wheel
{
    /// <summary>
    /// 轮盘 ViewModel 对外表面（随实现方 M2 下沉，ADR-0023/#97；#112 收口入
    /// <c>StarPie.Sdk</c>；由手势引擎驱动）：显示、扇区高亮与外围逃逸都作为状态变更传入，
    /// 由轮盘窗口反映——引擎从不直接调用窗口方法。
    /// </summary>
    /// <remarks><see cref="HighlightSector"/> 传入 -1 表示清除选中。</remarks>
    public interface IWheelViewModel
    {
        void Show();

        void HighlightSector(int sectorIndex);

        void SetOuterEscapeState(bool isEscaped);

        void Close();
    }
}
