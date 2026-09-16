using System;
using System.Windows;

using StarPie.ViewModels.Wheel;

namespace StarPie.Tests;

/// <summary>
/// 轮盘核心路径启动预热测试：离屏渲染产物在放弃引用后无滞留（WeakReference 判定）。
/// </summary>
/// <remarks>
/// 只守可回收性：预热在裸 <see cref="Application"/>（StaTestHarness）下执行；
/// 失败路径与壳层吞异常（AppHost 调用方的 try/catch）无自动覆盖。
/// </remarks>
public sealed class WheelWarmupTests
{
    private static readonly LocalizationService Localization = new();

    [Fact]
    public void Warmup_product_is_collectable_after_reference_drop()
    {
        WeakReference reference = StaTestHarness.Run(() =>
        {
            var config = new AppConfig();
            var profile = new WheelProfile();
            var viewModel = new WheelViewModel(new GesturePoint(200, 200), profile, WheelViewData.FromConfig(config), Localization);

            return WheelWarmup.Run(viewModel, () => false, Localization, new TestIconAssetService());
        });

        // 两轮 GC + finalizer 排空：终结器可能复活对象产生新垃圾，排空后再收一次
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.IsAlive, "预热产物（RadialWindow）放弃引用后应可回收，出现滞留");
    }
}
