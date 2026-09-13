using System;
using System.Windows;
using StarPie.Services.Shell;

namespace StarPie.Tests;

/// <summary>
/// 轮盘核心路径启动预热测试（#150）：离屏渲染产物在放弃引用后可回收（WeakReference 判定，
/// 无滞留）；预热在裸 <see cref="Application"/>（StaTestHarness）下执行，失败以异常表达，
/// 由 AppHost 调用方吞异常（try/catch 结构，预热失败不影响启动）。
/// </summary>
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
            var viewModel = new WheelViewModel(new GesturePoint(200, 200), profile, config, Localization);

            return WheelWarmup.Run(viewModel, new FakeThemeService(), Localization, new TestIconAssetService());
        });

        // 两轮 GC + finalizer 排空：终结器可能复活对象产生新垃圾，排空后再收一次
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.IsAlive, "预热产物（RadialWindow）放弃引用后应可回收，出现滞留");
    }

    /// <summary><see cref="IThemeService"/> 测试替身：状态无操作，深色探测返回 false。</summary>
    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentEffectiveTheme => "Light";

        public void SetTheme(string themeName) { }

        public void ApplyWindowTheme(FrameworkElement? rootElement) { }

        public string ResolveEffectiveTheme(string themeName) => string.IsNullOrEmpty(themeName) || themeName == "System" ? "Light" : themeName;

        public bool IsWindowsInDarkTheme() => false;
    }
}
