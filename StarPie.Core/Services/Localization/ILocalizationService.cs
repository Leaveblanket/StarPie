using System;
using System.Collections.Generic;

namespace StarPie.Services.Localization
{
    /// <summary>
    /// 本地化服务门面：语言状态与取词 API。
    /// </summary>
    /// <remarks>
    /// 语言状态统一为规范 BCP-47 码（"zh-CN"/"zh-TW"/"en"/"ja"），不再保留
    /// 自定义枚举中间表示；"Auto" 仅在 <see cref="SetLanguage(string)"/> 入口解析。
    /// 语言切换是文案的唯一变更源；声明式文案经 AppHost 投影为运行时语言字典（DynamicResource）。
    /// 回退链：目标语言 → zh-CN → 键名。
    /// </remarks>
    public interface ILocalizationService
    {
        /// <summary>当前语言的规范 BCP-47 码（"zh-CN"/"zh-TW"/"en"/"ja"）。</summary>
        string CurrentLanguage { get; }

        /// <summary>语言实际变化后触发（订阅者必须成对退订）。</summary>
        event Action? LanguageChanged;

        /// <summary>按当前语言取词；缺语言回退 zh-CN，再缺回退键名。</summary>
        string GetString(string key);

        /// <summary>语言切换入口："Auto" 按 CurrentUICulture 解析，其余按已知码/别名解析为规范码；未知码兜底 zh-CN。</summary>
        void SetLanguage(string code);

        /// <summary>枚举当前语言全部键值（运行时语言字典投影桥的数据源；取值与 GetString 同兜底语义）。</summary>
        IEnumerable<KeyValuePair<string, string>> EnumerateCurrentEntries();
    }
}
