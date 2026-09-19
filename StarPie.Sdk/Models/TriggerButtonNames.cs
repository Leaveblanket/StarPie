using System;
using System.Collections.Generic;

namespace StarPie.Sdk.Models
{
    /// <summary>
    /// 触发键的配置词表：config.json <c>TriggerButton</c> 键的合法取值，与上游 WinPieGestures
    /// 的 config.json 同名同值域（上游侧配置可平移读入）。取值到 SharpHook 鼠标键的映射
    /// 在捕获侧（StarPie.Ui）解析——模型层不依赖 SharpHook。
    /// </summary>
    public static class TriggerButtonNames
    {
        /// <summary>默认触发键：右键。存量配置缺该键、取值非法时一律回退此值。</summary>
        public const string Default = "RightButton";

        /// <summary>全部合法取值：左 / 右 / 中 / 侧1 / 侧2（鼠标五键；键盘触发键不在词表，见 ADR-0052）。</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            "LeftButton",
            "RightButton",
            "MiddleButton",
            "XButton1",
            "XButton2",
        };

        /// <summary>是否为合法配置取值（序数忽略大小写，容忍首尾空白）。</summary>
        public static bool IsKnown(string? name)
        {
            if (name is null) return false;

            string trimmed = name.Trim();
            foreach (string candidate in All)
            {
                if (string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
