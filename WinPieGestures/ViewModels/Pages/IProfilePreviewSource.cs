namespace WinPieGestures.ViewModels.Pages
{
    /// <summary>
    /// 只读「预览 Profile 来源」接口（#69，模块地图 B2）：配置方案设置面（M1 手势与动作）对外提供
    /// 的轮盘预览 Profile 上下文只读契约。语义归属 M1（实现方为同目录配置方案列表 VM
    /// <see cref="ProfileListViewModel"/>），被 M2 轮盘与渲染消费——轮盘外观设置子 VM
    /// <see cref="WheelAppearanceSettingsViewModel"/> 经本接口取预览所用 Profile，随其实现的
    /// <see cref="IWheelAppearanceState.PreviewProfile"/> 暴露给预览渲染器。
    /// 接口只读：不暴露选中写入口/事件/命令/列表集合，轮盘侧不得反向牵动配置方案编辑实现。
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
