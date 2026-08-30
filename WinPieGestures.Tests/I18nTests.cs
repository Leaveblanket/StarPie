using WinPieGestures;

namespace WinPieGestures.Tests;

public class I18nTests
{
    [Fact]
    public void LanguageChanged_Fires_WhenLanguageActuallyChanges()
    {
        var original = I18n.CurrentLanguage;
        var fired = 0;
        void Handler() => fired++;
        I18n.LanguageChanged += Handler;
        try
        {
            var target = original == LanguageCode.En ? LanguageCode.Ja : LanguageCode.En;
            I18n.SetLanguage(target == LanguageCode.En ? "en" : "ja");

            Assert.Equal(1, fired);
            Assert.Equal(target, I18n.CurrentLanguage);
        }
        finally
        {
            I18n.LanguageChanged -= Handler;
            I18n.CurrentLanguage = original;
        }
    }

    [Fact]
    public void LanguageChanged_DoesNotFire_WhenLanguageUnchanged()
    {
        var original = I18n.CurrentLanguage;
        var fired = 0;
        void Handler() => fired++;
        I18n.LanguageChanged += Handler;
        try
        {
            // 静态语言状态可能被其他测试改变，先基准化并重置计数
            I18n.SetLanguage("en");
            fired = 0;

            I18n.SetLanguage("en-US");

            Assert.Equal(0, fired);
        }
        finally
        {
            I18n.LanguageChanged -= Handler;
            I18n.CurrentLanguage = original;
        }
    }

    [Theory]
    [InlineData("zh-TW", LanguageCode.ZhTw)]
    [InlineData("en", LanguageCode.En)]
    [InlineData("ja", LanguageCode.Ja)]
    [InlineData("zh-CN", LanguageCode.ZhCn)]
    [InlineData("unknown-code", LanguageCode.ZhCn)]
    public void SetLanguage_ParsesKnownCodes(string code, LanguageCode expected)
    {
        var original = I18n.CurrentLanguage;
        try
        {
            I18n.SetLanguage(code);
            Assert.Equal(expected, I18n.CurrentLanguage);
        }
        finally
        {
            I18n.CurrentLanguage = original;
        }
    }
}
