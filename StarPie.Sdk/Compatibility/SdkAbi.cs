namespace StarPie.Compatibility
{
    /// <summary>
    /// headless SDK（<c>StarPie.Sdk</c>）的 ABI 政策：插件清单 <c>sdk</c> 声明「主.次」版本，
    /// 宿主接受同主版本且次版本不高于宿主的插件；编号与 <c>StarPie.Sdk.Wpf</c> 的 ABI 政策相互独立。
    /// 政策 additive-only——既有导出类型的删除或改名由导出面白名单的边界测试拦截。
    /// </summary>
    public static class SdkAbi
    {
        /// <summary>当前 ABI 主版本；破坏性变更时递增。</summary>
        public const int MajorVersion = 1;

        /// <summary>当前 ABI 次版本；additive 变更时递增。</summary>
        public const int MinorVersion = 0;

        /// <summary>当前 ABI 版本串（插件清单 <c>sdk</c> 的规范形态："主.次"）。</summary>
        public static string Version => $"{MajorVersion}.{MinorVersion}";

        /// <summary>兼容判定：插件与宿主同主版本、且插件次版本不高于宿主才接受。</summary>
        public static bool IsCompatible(int pluginMajor, int pluginMinor, int hostMajor, int hostMinor)
            => pluginMajor == hostMajor && pluginMinor <= hostMinor;

        /// <summary>按当前宿主版本执行兼容判定（校验时以本集的 ABI 常量为宿主版本）。</summary>
        public static bool IsCompatibleWithCurrentHost(int pluginMajor, int pluginMinor)
            => IsCompatible(pluginMajor, pluginMinor, MajorVersion, MinorVersion);

        /// <summary>解析 "主.次" 形态的 ABI 版本串；格式不合法返回 false。</summary>
        public static bool TryParseVersion(string? text, out int major, out int minor)
            => AbiVersion.TryParse(text, out major, out minor);
    }
}
