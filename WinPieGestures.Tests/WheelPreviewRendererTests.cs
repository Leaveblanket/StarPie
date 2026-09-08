using System.Windows.Controls;
using System.Windows.Input;

namespace StarPie.Tests;

/// <summary>
/// 外观页实时预览渲染器的输入契约覆盖：渲染器只依赖轮盘模块只读状态接口
/// <see cref="IWheelAppearanceState"/>。WPF 元素实例化需要 STA 且画面断言属视觉冒烟
/// （仓库 xUnit 惯例留给 pywinauto e2e），此处用方法组转换在编译期钉住公开签名——
/// 若渲染器参数回退为具体外观聚合 VM 类型（<c>AppearanceSettingsViewModel</c>），
/// 本文件将无法编译。
/// </summary>
public sealed class WheelPreviewRendererTests
{
    [Fact]
    public void RenderAndMouseMove_AcceptIWheelAppearanceState_NotConcreteAggregateVm()
    {
        var renderer = new WheelPreviewRenderer(new TestIconAssetService());

        // 渲染器不反向引用宿主：深浅色探测由调用方以 bool 传入；
        // 方法组转换继续在编译期钉住公开签名。
        Action<Canvas, IWheelAppearanceState, bool> render = renderer.Render;
        Action<Canvas, MouseEventArgs, IWheelAppearanceState> hover = renderer.HandleMouseMove;

        Assert.NotNull(render);
        Assert.NotNull(hover);
    }
}
