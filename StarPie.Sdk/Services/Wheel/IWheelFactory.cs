using StarPie.Models;
using StarPie.ViewModels.Wheel;

namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 每次手势创建全新瞬态轮盘（视图模型 + 窗口）的工厂接口（随实现方 M2 下沉，
    /// ADR-0023/#97；#112 收口入 <c>StarPie.Sdk</c>）。
    /// </summary>
    /// <remarks>
    /// 实现 <c>WheelFactory</c>（驻 StarPie.Wheel runtime）；实现方负责 UI 线程调度，
    /// 调用方可能位于钩子线程。手势侧（Gestures runtime）只经本接口消费，不反向组装
    /// 瞬态轮盘（M1→M2 runtime 允许边清零，改经本契约边）。
    /// </remarks>
    public interface IWheelFactory
    {
        IWheelViewModel Create(GesturePoint center, WheelProfile profile);
    }
}
