using StarPie.Models;
using StarPie.ViewModels.Wheel;

namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 每次手势创建全新瞬态轮盘（视图模型 + 窗口）的工厂接口。
    /// </summary>
    /// <remarks>
    /// 实现方负责把创建与显示调度到 UI 线程（调用方可能位于钩子线程）；手势侧只经本接口
    /// 消费轮盘，不反向组装瞬态轮盘。
    /// </remarks>
    public interface IWheelFactory
    {
        IWheelViewModel Create(GesturePoint center, WheelProfile profile);
    }
}
