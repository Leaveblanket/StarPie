using System.Windows;
using StarPie.Services.Shell;
using StarPie.Services.Wheel;

namespace StarPie.Tests;

/// <summary>
/// 轮盘工厂预热契约覆盖（#177）：Profile 查找与离屏预热的装配收在实现内，壳层只经
/// <see cref="IWheelFactory.Warmup"/> 触发；两条路径（配置里有全局方案 / 没有）都不抛，
/// 失败交由调用方决定是否吞掉。
/// </summary>
public sealed class WheelFactoryTests
{
    private static readonly LocalizationService Localization = new();

    private static WheelFactory Create(TestConfigService config, FakeThemeService theme)
        => new(config, theme, Localization, new TestIconAssetService());

    [Fact]
    public void Warmup_WithGlobalProfileInConfig_AssemblesAndRendersOffscreen()
    {
        var config = new TestConfigService();
        config.Current.Profiles.Add(new WheelProfile { ProcessName = "Global", SectorCount = 4 });
        config.Current.Profiles.Add(new WheelProfile { ProcessName = "chrome.exe", SectorCount = 12 });
        var theme = new FakeThemeService();

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
    public void Warmup_WithoutGlobalProfile_FallsBackToEmptyProfile()
    {
        var config = new TestConfigService();
        var theme = new FakeThemeService();

        var factory = Create(config, theme);

        StaTestHarness.Run(() =>
        {
            factory.Warmup();
            return true;
        });

        Assert.True(theme.DarkModeProbeCalls > 0);
    }

    /// <summary><see cref="IThemeService"/> 测试替身：状态无操作，记录深浅色探测次数。</summary>
    private sealed class FakeThemeService : IThemeService
    {
        public int DarkModeProbeCalls { get; private set; }

        public string CurrentEffectiveTheme => "Light";

        public void SetTheme(string themeName) { }

        public void ApplyWindowTheme(FrameworkElement? rootElement) { }

        public string ResolveEffectiveTheme(string themeName) => string.IsNullOrEmpty(themeName) || themeName == "System" ? "Light" : themeName;

        public bool IsWindowsInDarkTheme()
        {
            DarkModeProbeCalls++;
            return false;
        }
    }
}
