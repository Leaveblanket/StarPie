namespace StarPie.Services.Wheel
{
    /// <summary>
    /// Creates a transient wheel (view-model plus its window) per gesture — every
    /// gesture gets a fresh pair (ADR-0002). Implementations own the UI-thread
    /// marshaling; callers may be on the hook thread.
    /// M2 侧轮盘工厂接口（B8/#81，D5/ADR-0016 决策 11）：实现
    /// <see cref="WheelFactory"/> 随 M2 收编进 StarPie.Wheel；M1 手势侧（GestureEngine 等，
    /// B9/#82 起随 StarPie.Gestures 成集）只经本接口消费，不反向组装 M2 瞬态轮盘。
    /// </summary>
    public interface IWheelFactory
    {
        IWheelViewModel Create(GesturePoint center, WheelProfile profile);
    }
}
