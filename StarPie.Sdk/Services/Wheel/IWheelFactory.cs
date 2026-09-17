using StarPie.Models;
using StarPie.ViewModels.Wheel;

namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 每次手势创建全新瞬态轮盘（视图模型 + 窗口）的工厂接口。
    /// </summary>
    /// <remarks>
    /// 实现方负责把创建与显示调度到 UI 线程；调用方就是钩子线程（ADR-0052），因此
    /// <see cref="Create"/> 一律不阻塞，返回的句柄把这轮手势的构建与状态变更按序投放。
    /// 手势侧只经本接口消费轮盘，不反向组装瞬态轮盘。
    /// </remarks>
    public interface IWheelFactory
    {
        IWheelViewModel Create(GesturePoint center, WheelProfile profile);

        /// <summary>启动期轮盘核心路径预热：踩热窗口 BAML、样式渲染器工厂与调色板/画刷构造路径，
        /// 使首次手势弹出不再付这些一次性成本。失败以异常表达，由调用方决定是否吞掉。</summary>
        /// <remarks>
        /// 装配（取哪个 Profile、经什么方式预热）归实现方，调用方不必知道。预热作为本契约的
        /// 单方法扩展承载，不另立预热契约：实现方已持有预热所需的全部依赖。须在 UI 线程调用
        /// ——预热即离屏构造窗口，与经钩子线程调用的 <see cref="Create"/> 不同，本方法不自行封送。
        /// </remarks>
        void Warmup();
    }
}
