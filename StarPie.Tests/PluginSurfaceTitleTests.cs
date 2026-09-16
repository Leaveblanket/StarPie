using StarPie.Localization;
using StarPie.PluginHosting.Extensions;

namespace StarPie.Tests;

/// <summary>
/// 插件界面标题的解析优先级：显示名字面量优先，未给显示名时经宿主文案表取词；
/// 文案键不在表内则取词回退成键名，并由注册侧据此告警（本类只判存在性，不含告警）。
/// </summary>
public sealed class PluginSurfaceTitleTests
{
    [Fact]
    public void 显示名非空_字面量优先于文案键()
    {
        var localization = new LocalizationService();

        Assert.Equal("示例页", PluginSurfaceTitle.Resolve("示例页", "ThemeLight", localization));
    }

    [Fact]
    public void 显示名为空或空白_改用宿主文案键()
    {
        var localization = new LocalizationService();
        string expected = localization.GetString("ThemeLight");

        Assert.Equal(expected, PluginSurfaceTitle.Resolve(null, "ThemeLight", localization));
        Assert.Equal(expected, PluginSurfaceTitle.Resolve("   ", "ThemeLight", localization));
    }

    [Fact]
    public void 显示名不受语言切换影响()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("zh-CN");
        string before = PluginSurfaceTitle.Resolve("示例页", "ThemeLight", localization);

        localization.SetLanguage("ja");

        Assert.Equal("示例页", before);
        Assert.Equal(before, PluginSurfaceTitle.Resolve("示例页", "ThemeLight", localization));
    }

    [Fact]
    public void 文案键在宿主文案表_判定为存在()
    {
        Assert.True(PluginSurfaceTitle.ExistsInHostTable("ThemeLight", new LocalizationService()));
    }

    [Fact]
    public void 文案键不在宿主文案表_判定为不存在且取词回退键名()
    {
        var localization = new LocalizationService();

        Assert.False(PluginSurfaceTitle.ExistsInHostTable("NoSuchPluginTitle", localization));
        Assert.Equal("NoSuchPluginTitle", PluginSurfaceTitle.Resolve(null, "NoSuchPluginTitle", localization));
    }
}
