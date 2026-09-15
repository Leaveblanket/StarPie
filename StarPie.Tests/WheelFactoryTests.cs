using System.Windows;
using StarPie.Services.Shell;
using StarPie.Services.Wheel;

namespace StarPie.Tests;

/// <summary>
/// 轮盘工厂预热契约覆盖：Profile 查找与离屏预热的装配收在实现内，壳层只经
/// <see cref="IWheelFactory.Warmup"/> 触发；两条路径（配置里有全局方案 / 没有）都不抛，
/// 失败交由调用方决定是否吞掉。
/// </summary>
public sealed class WheelFactoryTests
{
    private static readonly LocalizationService Localization = new();

    private static WheelFactory Create(TestConfigService config, TestThemeService theme)
        => new(config, theme, Localization, new TestIconAssetService());

    [Fact]
    public void Warmup_WithGlobalProfileInConfig_AssemblesAndRendersOffscreen()
    {
        var config = new TestConfigService();
        config.Current.Profiles.Add(new WheelProfile { ProcessName = "Global", SectorCount = 4 });
        config.Current.Profiles.Add(new WheelProfile { ProcessName = "chrome.exe", SectorCount = 12 });
        var theme = new TestThemeService();

        var factory = Create(config, theme);

        StaTestHarness.Run(() =>
        {
            factory.Warmup();
            return true;
        });

        // 预热确实走完了「构造窗口 → 初始化样式渲染器」的装配，而不只是空跑。
        Assert.True(theme.DarkModeProbeCalls > 0, "预热应构造轮盘窗口并经主题服务探测深浅色");
    }

    [Fact]
    public void Warmup_WithoutGlobalProfile_StillAssemblesAndRendersOffscreen()
    {
        var config = new TestConfigService();
        var theme = new TestThemeService();

        var factory = Create(config, theme);

        StaTestHarness.Run(() =>
        {
            factory.Warmup();
            return true;
        });

        // 配置里没有全局方案：Warmup 以空方案兜底装配，预热路径与有方案时同形。
        Assert.True(theme.DarkModeProbeCalls > 0, "无全局方案也应完成离屏预热并经主题服务探测深浅色");
    }
}
