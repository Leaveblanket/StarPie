using StarPie.Localization;
using StarPie.ViewModels.Pages;

namespace StarPie.Tests;

/// <summary>
/// 驻留文案件的语言绑定：语言实际变化才重建目录并补发选中通知；
/// 释放后退订（幂等），此后语言变化不再触发。
/// </summary>
public sealed class ResidentOptionRefresherTests
{
    [Fact]
    public void 语言实际变化_重建目录并补发选中通知()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("zh-CN");
        int rebuilds = 0;
        int notifications = 0;
        using var refresher = new ResidentOptionRefresher(
            localization,
            () => rebuilds++,
            () => notifications++);

        localization.SetLanguage("ja");

        Assert.Equal(1, rebuilds);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void 语言未实际变化_不重建也不通知()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("en");
        int rebuilds = 0;
        int notifications = 0;
        using var refresher = new ResidentOptionRefresher(
            localization,
            () => rebuilds++,
            () => notifications++);

        localization.SetLanguage("en");
        localization.SetLanguage("en-US"); // 别名/区域码折叠回 en，不算变化

        Assert.Equal(0, rebuilds);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void 释放后_语言变化不再触发且重复释放安全()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("zh-CN");
        int rebuilds = 0;
        var refresher = new ResidentOptionRefresher(localization, () => rebuilds++, () => { });

        refresher.Dispose();
        refresher.Dispose();
        localization.SetLanguage("ja");

        Assert.Equal(0, rebuilds);
    }
}
