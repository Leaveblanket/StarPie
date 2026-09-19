namespace StarPie.Sdk.ViewModels.Wheel
{
    /// <summary>
    /// 轮盘 ViewModel 对外表面（ADR-0023；驻 <c>StarPie.Sdk</c>；由轮盘手势引擎驱动）：
    /// 显示、扇区高亮与外围逃逸都作为状态变更传入，由轮盘窗口反映——引擎从不直接调用窗口
    /// 方法。
    /// </summary>
    /// <remarks>
    /// 四个操作都由实现方异步投放到 UI 线程（调用方位于钩子线程，ADR-0052），调用即返回；
    /// 同一轮盘内按调用顺序生效：构建先于显示，显示先于各次状态变更。
    /// <see cref="HighlightSector"/> 传入 -1 表示清除选中。
    /// </remarks>
    public interface IWheelViewModel
    {
        void Show();

        void HighlightSector(int sectorIndex);

        void SetOuterEscapeState(bool isEscaped);

        void Close();
    }
}
