using System.Text.RegularExpressions;
using StarPie.Compatibility;
using StarPie.Manifest;

namespace StarPie.PluginRuntime.Manifest
{
    /// <summary>
    /// 清单与包的语义校验：字段规则、SDK ABI 兼容、包目录一致性、清单声明文件的存在性。
    /// </summary>
    /// <remarks>
    /// 返回全部违规（空列表 = 通过），由上层汇总成拒绝原因——不做「首个错误即返回」，
    /// 便于一次性给出可处理的原因清单。ui 段只做格式校验：UI 侧 ABI 兼容判定归 Ui 侧插件托管层。
    /// </remarks>
    public static class PluginManifestValidator
    {
        /// <summary>本校验接受的清单格式版本。</summary>
        private const int SupportedSchemaVersion = 1;

        /// <summary>id：反向域名——小写字母/数字/连字符，至少两段。</summary>
        private static readonly Regex IdPattern = new(
            @"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$",
            RegexOptions.Compiled);

        /// <summary>SemVer：主.次.修订，可带预发布与构建后缀。</summary>
        private static readonly Regex SemVerPattern = new(
            @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$",
            RegexOptions.Compiled);

        /// <summary>类型全名：命名空间限定（至少一段点号分隔）。</summary>
        private static readonly Regex TypeNamePattern = new(
            @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+$",
            RegexOptions.Compiled);

        /// <summary>能力 id：单段小写标识。</summary>
        private static readonly Regex CapabilityIdPattern = new(
            @"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$",
            RegexOptions.Compiled);

        /// <summary>
        /// 校验清单与包目录；返回全部违规原因（空列表表示通过）。
        /// </summary>
        /// <param name="manifest">已解析清单；null 表示解析失败，直接判为违规。</param>
        /// <param name="packageDirectory">包目录（用于目录名一致性与声明文件存在性判定）。</param>
        public static IReadOnlyList<string> Validate(PluginManifest? manifest, string packageDirectory)
        {
            var errors = new List<string>();
            if (manifest is null)
            {
                errors.Add("清单缺失或无法解析");
                return errors;
            }

            if (manifest.SchemaVersion != SupportedSchemaVersion)
            {
                errors.Add($"schemaVersion 只支持 {SupportedSchemaVersion}（实际 {manifest.SchemaVersion}）");
            }

            ValidateId(manifest.Id, packageDirectory, errors);

            if (string.IsNullOrWhiteSpace(manifest.Name))
            {
                errors.Add("缺少必填字段 name");
            }

            if (string.IsNullOrWhiteSpace(manifest.Version))
            {
                errors.Add("缺少必填字段 version");
            }
            else if (!SemVerPattern.IsMatch(manifest.Version))
            {
                errors.Add($"version 不是合法 SemVer：{manifest.Version}");
            }

            ValidateSdkAbi(manifest.Sdk, errors);
            ValidateEntryAssembly(manifest.EntryAssembly, packageDirectory, errors);

            if (string.IsNullOrWhiteSpace(manifest.EntryType))
            {
                errors.Add("缺少必填字段 entryType");
            }
            else if (!TypeNamePattern.IsMatch(manifest.EntryType))
            {
                errors.Add($"entryType 不是合法类型全名：{manifest.EntryType}");
            }

            ValidateUiSection(manifest.Ui, errors);
            ValidateCapabilities(manifest.Capabilities, errors);
            ValidateDeclaredFile(manifest.SettingsSchema, "settingsSchema", packageDirectory, errors);
            return errors;
        }

        private static void ValidateId(string? id, string packageDirectory, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add("缺少必填字段 id");
                return;
            }

            if (!IdPattern.IsMatch(id))
            {
                errors.Add($"id 必须是反向域名（小写字母/数字/连字符，至少两段）：{id}");
                return;
            }

            string directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDirectory));
            if (!string.Equals(directoryName, id, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"包目录名须等于 id：目录 {directoryName} / id {id}");
            }
        }

        private static void ValidateSdkAbi(string? sdk, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(sdk))
            {
                errors.Add("缺少必填字段 sdk");
                return;
            }

            if (!SdkAbi.TryParseVersion(sdk, out int major, out int minor))
            {
                errors.Add($"sdk 必须是「主.次」版本号：{sdk}");
                return;
            }

            if (!SdkAbi.IsCompatibleWithCurrentHost(major, minor))
            {
                errors.Add($"SDK ABI 不兼容：清单声明 {sdk}，宿主为 {SdkAbi.Version}");
            }
        }

        private static void ValidateEntryAssembly(string? entryAssembly, string packageDirectory, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(entryAssembly))
            {
                errors.Add("缺少必填字段 entryAssembly");
                return;
            }

            if (!IsBareFileName(entryAssembly) || !entryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"entryAssembly 必须是包内 .dll 裸文件名：{entryAssembly}");
                return;
            }

            if (!File.Exists(Path.Combine(packageDirectory, entryAssembly)))
            {
                errors.Add($"entryAssembly 文件不存在于包内：{entryAssembly}");
            }
        }

        private static void ValidateUiSection(PluginUiManifest? ui, List<string> errors)
        {
            // 无 ui 段即 headless 插件，是合法形态。
            if (ui is null)
            {
                return;
            }

            if (!AbiVersion.TryParse(ui.Sdk, out _, out _))
            {
                errors.Add($"ui.sdk 必须是「主.次」版本号：{ui.Sdk}");
            }

            if (string.IsNullOrWhiteSpace(ui.EntryType) || !TypeNamePattern.IsMatch(ui.EntryType))
            {
                errors.Add($"ui.entryType 必须是类型全名：{ui.EntryType}");
            }
        }

        private static void ValidateCapabilities(List<PluginCapabilityReference>? capabilities, List<string> errors)
        {
            if (capabilities is null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PluginCapabilityReference capability in capabilities)
            {
                if (string.IsNullOrWhiteSpace(capability.Id) || !CapabilityIdPattern.IsMatch(capability.Id))
                {
                    errors.Add($"capabilities[].id 非法：{capability.Id}");
                }
                else if (!seen.Add(capability.Id))
                {
                    errors.Add($"capabilities 重复声明能力：{capability.Id}");
                }

                if (capability.Abi < 1)
                {
                    errors.Add($"capabilities[{capability.Id}].abi 必须从 1 起");
                }
            }
        }

        private static void ValidateDeclaredFile(string? fileName, string fieldName, string packageDirectory, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            if (!IsBareFileName(fileName))
            {
                errors.Add($"{fieldName} 必须是包内裸文件名：{fileName}");
                return;
            }

            if (!File.Exists(Path.Combine(packageDirectory, fileName)))
            {
                errors.Add($"{fieldName} 文件不存在于包内：{fileName}");
            }
        }

        /// <summary>裸文件名判定：不含目录分隔符与非法字符，杜绝清单声明的路径逃逸出包目录。</summary>
        private static bool IsBareFileName(string value)
            => !string.IsNullOrWhiteSpace(value)
                && !value.Contains('/')
                && !value.Contains('\\')
                && value is not "." and not ".."
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}
