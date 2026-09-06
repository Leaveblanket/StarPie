namespace WinPieGestures.ViewModels.Pages
{
    /// <summary>
    /// 只读「预览 Profile 来源」契约（#69，模块地图 B2；B8/#81 上提共享内核 Core，D5/
    /// ADR-0016 决策 11）：配置方案设置面（M1 手势与动作，实现方为配置方案列表 VM
    /// ProfileListViewModel，仍驻 Host）对外提供的轮盘预览 Profile 上下文只读契约，被 M2
    /// 轮盘与渲染消费——轮盘外观设置子 VM WheelAppearanceSettingsViewModel 经本接口取预览
    /// 所用 Profile，随其实现的轮盘只读状态接口 PreviewProfile 成员暴露给预览渲染器。
    /// 接口只读：不暴露选中写入口/事件/命令/列表集合，轮盘侧不得反向牵动配置方案编辑实现。
    /// 实现方与消费方分属 M1/M2，均只依赖本 Core 契约（不引用具体方案列表 VM 类型）。
    /// </summary>
    public interface IProfilePreviewSource
    {
        /// <summary>
        /// 预览渲染所用 Profile：优先当前选中方案，无选中时回落列表首项——与配置方案设置面
        /// “选中/首项回落”语义一致；空列表为 null，兜底仍留在消费方（预览渲染器）。
        /// </summary>
        WheelProfile? PreviewProfile { get; }
    }
}
