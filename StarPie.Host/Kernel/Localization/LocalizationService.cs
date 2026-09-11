using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace StarPie.Kernel.Localization
{
    /// <summary>
    /// resx 数据源实现：四语言资产（Strings.resx 中性 = zh-CN + 卫星 zh-TW/en/ja）
    /// 经 <see cref="Strings.ResourceManager"/> 取词。
    /// </summary>
    /// <remarks>
    /// 语言状态以规范 BCP-47 码（"zh-CN"/"zh-TW"/"en"/"ja"）为单一表示：
    /// <see cref="SetLanguage(string)"/> 先解析 "Auto"，再把任意别名/区域码经
    /// <see cref="AliasToCanonical"/> 表折叠为规范码，未知码兜底 zh-CN。
    /// ResourceManager 回退链使“目标语言缺键 → zh-CN 中性”自动成立；
    /// GetString 返回 null 时回退键名。
    /// </remarks>
    public sealed class LocalizationService : ILocalizationService
    {
        /// <summary>系统跟随（非 BCP-47；由 <see cref="SetLanguage"/> 解析为当前 UI 文化对应的规范码）。</summary>
        private const string Auto = "Auto";

        /// <summary>默认/兜底语言的规范 BCP-47 码（对应中性 resx 内容）。</summary>
        private const string DefaultLanguage = "zh-CN";

        /// <summary>
        /// 系统 UI 文化前缀 → 规范码（"Auto" 解析用；顺序敏感：繁体中文地区须先于 "zh" 匹配）。
        /// 未命中前缀时兜底 "en"（与原 Auto 解析语义一致）。
        /// </summary>
        private static readonly (string Prefix, string Language)[] AutoCultureRules =
        {
            ("zh-TW", "zh-TW"),
            ("zh-HK", "zh-TW"),
            ("zh-MO", "zh-TW"),
            ("zh-Hant", "zh-TW"),
            ("zh", "zh-CN"),
            ("ja", "ja"),
        };

        /// <summary>
        /// 已知区域/别名 → 规范 BCP-47 码（大小写不敏感）。新语言只需新增卫星 resx 与本表条目。
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> AliasToCanonical =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = "zh-CN",
                ["zh-Hans"] = "zh-CN",
                ["zh-TW"] = "zh-TW",
                ["zh-HK"] = "zh-TW",
                ["zh-MO"] = "zh-TW",
                ["zh-Hant"] = "zh-TW",
                ["en"] = "en",
                ["en-US"] = "en",
                ["en-GB"] = "en",
                ["ja"] = "ja",
                ["ja-JP"] = "ja",
            };

        private string _currentLanguage = DefaultLanguage;

        public event Action? LanguageChanged;

        public string CurrentLanguage => _currentLanguage;

        /// <summary>
        /// 语言切换入口："Auto"→按当前系统 UI 文化（<see cref="CultureInfo.CurrentUICulture"/>）
        /// 前缀规则解析为对应的规范码，未命中前缀时兜底 "en"；别名/区域码→经
        /// <see cref="AliasToCanonical"/> 折叠为规范码；未知/空→zh-CN 兜底。
        /// 语言实际变化后触发 <see cref="LanguageChanged"/>。
        /// </summary>
        public void SetLanguage(string code)
        {
            string language;
            if (string.Equals(code, Auto, StringComparison.OrdinalIgnoreCase))
            {
                language = "en"; // 默认兜底
                string culture = CultureInfo.CurrentUICulture.Name;
                foreach (var (prefix, canonical) in AutoCultureRules)
                {
                    if (culture.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        language = canonical;
                        break;
                    }
                }
            }
            else
            {
                language = code != null && AliasToCanonical.TryGetValue(code, out string? canonical)
                    ? canonical
                    : DefaultLanguage;
            }

            if (string.Equals(_currentLanguage, language, StringComparison.Ordinal)) return;
            _currentLanguage = language;
            LanguageChanged?.Invoke();
        }

        public string GetString(string key)
        {
            string? value = Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(_currentLanguage));
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
