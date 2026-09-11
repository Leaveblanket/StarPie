using System;

namespace StarPie.Compatibility
{
    /// <summary>
    /// UI SDK（`StarPie.Sdk.Wpf`）ABI 政策骨架（P1.4/#113；plugins.md §2/§5.1 约束 8/§11，ADR-0028）：
    /// 该集与 `StarPie.Sdk` 同政策、独立编号——插件清单 <c>ui.sdk</c> 声明「主.次」版本，宿主接受
    /// 同主版本且次版本不高于宿主的插件；接口 additive-only，破坏性变更以新增接口/描述符表达。
    /// 导出面白名单在 <c>StarPie.Tests/SdkWpfBoundaryTests</c>，删除或改名既有导出类型即失败。
    /// </summary>
    public static class UiSdkAbi
    {
        /// <summary>当前 UI SDK ABI 主版本（破坏性变更递增；与 StarPie.Sdk 的编号相互独立）。</summary>
        public const int MajorVersion = 1;

        /// <summary>当前 UI SDK ABI 次版本（additive 变更递增；插件次版本不高于宿主才被接受）。</summary>
        public const int MinorVersion = 0;

        /// <summary>当前 ABI 版本串（插件清单 <c>ui.sdk</c> 的规范形态："主.次"）。</summary>
        public static string Version => $"{MajorVersion}.{MinorVersion}";

        /// <summary>兼容判定（§11）：插件与宿主同主版本、且插件次版本不高于宿主才接受。</summary>
        public static bool IsCompatible(int pluginMajor, int pluginMinor, int hostMajor, int hostMinor)
            => pluginMajor == hostMajor && pluginMinor <= hostMinor;

        /// <summary>按当前宿主版本执行兼容判定（装载/校验时以本集的 ABI 常量为宿主版本）。</summary>
        public static bool IsCompatibleWithCurrentHost(int pluginMajor, int pluginMinor)
            => IsCompatible(pluginMajor, pluginMinor, MajorVersion, MinorVersion);

        /// <summary>解析 "主.次" 形态的 ABI 版本串；格式不合法返回 false（不抛异常，失败时
        /// 输出参数归零）。</summary>
        public static bool TryParseVersion(string? text, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] parts = text.Trim().Split('.');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out major)
                || !int.TryParse(parts[1], out minor)
                || major < 0
                || minor < 0)
            {
                major = 0;
                minor = 0;
                return false;
            }

            return true;
        }
    }
}
