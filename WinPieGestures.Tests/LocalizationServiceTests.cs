using WinPieGestures.Services.Localization;

namespace WinPieGestures.Tests;

/// <summary>
/// LocalizationService 单测（ADR-0013/#44）：resx 数据源取词、语言解析、事件与投影枚举。
/// 语义与旧 I18n C# 键表等价，全部经服务实例（不依赖静态门面）。
/// </summary>
public class LocalizationServiceTests
{
    [Fact]
    public void SetLanguage_ParsesKnownCodes()
    {
        var service = new LocalizationService();

        service.SetLanguage("zh-TW");
        Assert.Equal(LanguageCode.ZhTw, service.CurrentLanguage);
        Assert.Equal("zh-TW", service.CurrentLanguageCode);

        service.SetLanguage("en-US");
        Assert.Equal(LanguageCode.En, service.CurrentLanguage);

        service.SetLanguage("ja-JP");
        Assert.Equal(LanguageCode.Ja, service.CurrentLanguage);

        service.SetLanguage("unknown-code");
        Assert.Equal(LanguageCode.ZhCn, service.CurrentLanguage);
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
