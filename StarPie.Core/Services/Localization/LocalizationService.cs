using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace WinPieGestures.Services.Localization
{
    /// <summary>
    /// resx 数据源实现（ADR-0013/#44）：四语言资产（Strings.resx 中性 = zh-CN + 卫星
    /// zh-TW/en/ja）经 <see cref="Strings.ResourceManager"/> 取词。ResourceManager 回退链
    /// 使“目标语言缺键 → zh-CN 中性”自动成立；GetString 返回 null 时回退键名。
    /// 语义与旧 I18n C# 键表等价。
    /// </summary>
    public sealed class LocalizationService : ILocalizationService
    {
        private LanguageCode _currentLanguage = LanguageCode.ZhCn;

        public event Action? LanguageChanged;

        public LanguageCode CurrentLanguage => _currentLanguage;

        public string CurrentLanguageCode => _currentLanguage switch
        {
            LanguageCode.ZhTw => "zh-TW",
            LanguageCode.En => "en",
            LanguageCode.Ja => "ja",
            _ => "zh-CN"
        };

        public void SetLanguage(string code)
        {
            if (string.Equals(code, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                string culture = CultureInfo.CurrentUICulture.Name;
                if (culture.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
                {
                    SetLanguage(LanguageCode.ZhTw);
                }
                else if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    SetLanguage(LanguageCode.ZhCn);
                }
                else if (culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
                {
                    SetLanguage(LanguageCode.Ja);
                }
                else
                {
                    SetLanguage(LanguageCode.En);
                }
                return;
            }

            SetLanguage(code switch
            {
                "zh-TW" or "zh-HK" or "zh-Hant" => LanguageCode.ZhTw,
                "en" or "en-US" or "en-GB" => LanguageCode.En,
                "ja" or "ja-JP" => LanguageCode.Ja,
                _ => LanguageCode.ZhCn
            });
        }

        public void SetLanguage(LanguageCode language)
        {
            if (_currentLanguage == language) return;
            _currentLanguage = language;
            LanguageChanged?.Invoke();
        }

        public string GetString(string key)
        {
            string? value = Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(CurrentLanguageCode));
            return value ?? key;
        }

        public IEnumerable<KeyValuePair<string, string>> EnumerateCurrentEntries()
        {
            var set = Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false);
            if (set == null) yield break;
            foreach (DictionaryEntry entry in set)
            {
                if (entry.Key is string key)
                {
                    yield return new KeyValuePair<string, string>(key, GetString(key));
                }
            }
        }
    }
}
