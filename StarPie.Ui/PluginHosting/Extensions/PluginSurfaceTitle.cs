using System;
using StarPie.Kernel.Localization;

namespace StarPie.PluginHosting.Extensions
{
    /// <summary>
    /// 插件界面标题的解析：显示名字面量与宿主文案键二选一，按单一优先级取词。
    /// </summary>
    /// <remarks>
    /// 插件不持有自己的文案表，也拿不到 <see cref="ILocalizationService"/>（该服务驻宿主集，
    /// 对插件编译期不可见），故标题只有两条来源——描述符给的显示名（字面量、语言无关），
    /// 或宿主文案表的键。注册侧（缺键判定）与渲染侧（取词）同引本类。
    /// </remarks>
    public static class PluginSurfaceTitle
    {
        /// <summary>按「显示名 → 宿主文案键」解析标题。</summary>
        /// <param name="displayName">描述符给的显示名；null 或空白即改用文案键。</param>
        /// <param name="titleKey">宿主文案表的键（非空）。</param>
        /// <param name="localization">宿主文案服务（非 null）。</param>
        /// <returns>展示用标题；文案键不在表内时回退链的末级返回键名本身。</returns>
        public static string Resolve(string? displayName, string titleKey, ILocalizationService localization)
        {
            ArgumentNullException.ThrowIfNull(titleKey);
            ArgumentNullException.ThrowIfNull(localization);

            return string.IsNullOrWhiteSpace(displayName) ? localization.GetString(titleKey) : displayName;
        }

        /// <summary>判定文案键是否落在宿主文案表内。</summary>
        /// <param name="titleKey">待判定的文案键（非空）。</param>
        /// <param name="localization">宿主文案服务（非 null）。</param>
        /// <returns>表内无此键时为 false。</returns>
        /// <remarks>取词结果与键名相同即视为表内无此键——回退链的末级正是返回键名。</remarks>
        public static bool ExistsInHostTable(string titleKey, ILocalizationService localization)
        {
            ArgumentNullException.ThrowIfNull(titleKey);
            ArgumentNullException.ThrowIfNull(localization);

            return !string.Equals(localization.GetString(titleKey), titleKey, StringComparison.Ordinal);
        }
    }
}
