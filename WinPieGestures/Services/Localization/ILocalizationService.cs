using System;
using System.Collections.Generic;

namespace WinPieGestures.Services.Localization
{
    /// <summary>界面语言码（四语言；"Auto" 由 <see cref="ILocalizationService.SetLanguage(string)"/> 解析）。</summary>
    public enum LanguageCode
    {
        ZhCn, // 简体中文
        ZhTw, // 繁體中文
        En,   // English
        Ja    // 日本語
    }

    /// <summary>
    /// 本地化服务门面（ADR-0013/#44）：语言状态的单一来源与取词 API，取代静态 I18n
    /// 作为唯一变更源；声明式文案仍经 AppHost 投影为运行时语言字典（DynamicResource）。
    /// 回退链：目标语言 → zh-CN → 键名。
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>当前语言码。</summary>
        LanguageCode CurrentLanguage { get; }

        /// <summary>当前语言的 BCP-47 码（"zh-CN"/"zh-TW"/"en"/"ja"）。</summary>
        string CurrentLanguageCode { get; }

        /// <summary>语言实际变化后触发（订阅者必须成对退订）。</summary>
        event Action? LanguageChanged;

        /// <summary>按当前语言取词；缺语言回退 zh-CN，再缺回退键名。</summary>
        string GetString(string key);

        /// <summary>语言切换入口："Auto" 按 CurrentUICulture 解析，其余按已知码/别名解析。</summary>
        void SetLanguage(string code);

        /// <summary>直接设置语言码（等价于 <see cref="SetLanguage(string)"/> 的解析结果）。</summary>
        void SetLanguage(LanguageCode language);

        /// <summary>枚举当前语言全部键值（运行时语言字典投影桥的数据源；取值与 GetString 同兜底语义）。</summary>
        IEnumerable<KeyValuePair<string, string>> EnumerateCurrentEntries();
    }
}
