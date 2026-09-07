using StarPie.Services.Localization;

namespace StarPie.Tests;

/// <summary>
/// LocalizationService 单测：resx 数据源取词、别名表归一化、事件与当前语言投影。
/// 全部经服务实例验证，不依赖静态门面。
/// </summary>
public class LocalizationServiceTests
{
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("zh-HK", "zh-TW")]
    [InlineData("zh-MO", "zh-TW")]
    [InlineData("zh-Hant", "zh-TW")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("ja", "ja")]
    [InlineData("ja-JP", "ja")]
    public void SetLanguage_AliasTable_NormalizesToCanonicalCode(string input, string expected)
    {
        var service = new LocalizationService();

        service.SetLanguage(input);

        Assert.Equal(expected, service.CurrentLanguage);
    }

    [Theory]
    [InlineData("unknown-code")]
    [InlineData("")]
    [InlineData(null)]
    public void SetLanguage_UnknownOrEmpty_FallsBackToZhCn(string? code)
    {
        var service = new LocalizationService();
        service.SetLanguage("en"); // 先离开默认值，验证兜底确实回到 zh-CN

        service.SetLanguage(code!);

        Assert.Equal("zh-CN", service.CurrentLanguage);
    }

    [Fact]
    public void LanguageChanged_FiresOnce_WhenLanguageActuallyChanges()
    {
        var service = new LocalizationService();
        int fired = 0;
        service.LanguageChanged += () => fired++;

        service.SetLanguage("en");
        Assert.Equal(1, fired);

        // 相同语言再次设置不触发
        service.SetLanguage("en-US");
        Assert.Equal(1, fired);
    }

    [Theory]
    [InlineData("zh-CN", "确定")]
    [InlineData("zh-TW", "確定")]
    [InlineData("en", "Confirm")]
    [InlineData("ja", "確定")]
    public void GetString_ReadsResxAssets_ForEachLanguage(string code, string expected)
    {
        var service = new LocalizationService();
        service.SetLanguage(code);

        Assert.Equal(expected, service.GetString("BtnConfirm"));
    }

    [Fact]
    public void GetString_ReturnsKey_WhenKeyMissingEverywhere()
    {
        var service = new LocalizationService();
        service.SetLanguage("en");

        Assert.Equal("NoSuchKey_42", service.GetString("NoSuchKey_42"));
    }

    [Fact]
    public void EnumerateCurrentEntries_ProjectsCurrentLanguageWithFallbackSemantics()
    {
        var service = new LocalizationService();
        service.SetLanguage("en");

        var entries = service.EnumerateCurrentEntries().ToList();

        Assert.True(entries.Count >= 200, $"expected full key table, got {entries.Count}");
        Assert.Equal("Confirm", entries.Single(e => e.Key == "BtnConfirm").Value);
        Assert.All(entries, e => Assert.False(string.IsNullOrEmpty(e.Value)));
    }
}
