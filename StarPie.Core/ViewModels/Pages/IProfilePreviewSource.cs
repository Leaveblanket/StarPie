namespace StarPie.ViewModels.Pages
{
    /// <summary>
    /// 只读「预览 Profile 来源」契约：配置方案设置面（手势与动作模块的实现方）对外提供
    /// 轮盘预览所用的 Profile，供轮盘外观设置的预览渲染消费。
    /// </summary>
    /// <remarks>
    /// 轮盘外观设置子 VM 经本接口取预览 Profile，再经其轮盘只读状态接口暴露给预览渲染器。
    /// 接口只读：不暴露选中写入口/事件/命令/列表集合，预览方不得反向牵动配置方案编辑实现；
    /// 实现方与消费方分属不同模块，均只依赖本共享内核契约（不引用具体方案列表 VM 类型）。
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
