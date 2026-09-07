namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 每次手势创建全新瞬态轮盘（视图模型 + 窗口）的工厂接口。
    /// </summary>
    /// <remarks>
    /// 实现 <see cref="WheelFactory"/>；实现方负责 UI 线程调度，调用方可能位于钩子线程。
    /// 手势侧只经本接口消费，不反向组装瞬态轮盘。
    /// </remarks>
    public interface IWheelFactory
    {
        IWheelViewModel Create(GesturePoint center, WheelProfile profile);
    }
}
