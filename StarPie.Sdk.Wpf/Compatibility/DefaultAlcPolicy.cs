using System;
using System.Collections.Generic;
using System.Linq;

namespace StarPie.Compatibility
{
    /// <summary>
    /// 共享契约集的装载政策：<c>StarPie.Sdk</c> 与 <c>StarPie.Sdk.Wpf</c> 必须由默认 ALC 统一加载，
    /// 保证跨插件 ALC 的类型身份唯一；插件包内出现二者副本即拒绝装载（拒绝逻辑由装载管线实现，
    /// 本类是判定常量的唯一正典）。
    /// </summary>
    /// <remarks>框架程序集的默认 ALC 回退由装载管线按其既有规则处理，不在本类判定范围。</remarks>
    public static class DefaultAlcPolicy
    {
        /// <summary>headless SDK 程序集名。</summary>
        public const string SdkAssemblyName = "StarPie.Sdk";

        /// <summary>WPF 契约面程序集名。</summary>
        public const string SdkWpfAssemblyName = "StarPie.Sdk.Wpf";

        /// <summary>必须由默认 ALC 解析的共享契约程序集名（装载管线覆写 Load 时按此回退 null，
        /// 插件 ALC 不得私有加载其副本）。</summary>
        public static readonly IReadOnlyList<string> SharedContractAssemblyNames =
            new[] { SdkAssemblyName, SdkWpfAssemblyName };

        /// <summary>判定某程序集名是否必须回退默认 ALC。</summary>
        public static bool MustResolveFromDefaultAlc(string assemblyName)
            => SharedContractAssemblyNames.Contains(assemblyName, StringComparer.Ordinal);
    }
}
