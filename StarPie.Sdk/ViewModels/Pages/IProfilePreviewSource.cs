using StarPie.Models;

namespace StarPie.ViewModels.Pages
{
    /// <summary>
    /// 只读「预览 Profile 来源」契约（随实现方 M1 下沉，ADR-0023/#97 Q4 裁决；#112 收口入
    /// <c>StarPie.Sdk</c>）：配置方案设置面（手势与动作模块的实现方）对外提供轮盘预览
    /// 所用的 Profile，供轮盘外观设置的预览渲染消费。
    /// </summary>
    /// <remarks>
    /// 轮盘外观设置子 VM 经本接口取预览 Profile，再经其轮盘只读状态接口暴露给预览渲染器。
    /// 接口只读：不暴露选中写入口/事件/命令/列表集合，预览方不得反向牵动配置方案编辑实现；
    /// 实现方（ProfileListViewModel，驻 Gestures runtime）与消费方（Wheel runtime）分属
    /// 不同模块，均只依赖本契约程序集（不引用具体方案列表 VM 类型）；契约与实现方同驻
    /// M1 侧可避免 Wheel ↔ Gestures runtime 程序集环。
    /// </remarks>
    public interface IProfilePreviewSource
    {
        /// <summary>
        /// 预览渲染所用 Profile：优先当前选中方案，无选中时回落列表首项——与配置方案设置面
        /// “选中/首项回落”语义一致；空列表为 null，兜底仍留在消费方（预览渲染器）。
        /// </summary>
        WheelProfile? PreviewProfile { get; }
    }
}
