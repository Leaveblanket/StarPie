using System.Text.Json;
using StarPie.Manifest;

namespace StarPie.PluginRuntime.Manifest
{
    /// <summary>
    /// plugin.json 的反序列化：JSON 语法与字段类型问题在此收口，语义规则归
    /// <see cref="PluginManifestValidator"/>。
    /// </summary>
    /// <remarks>
    /// 读取语义与 config.json 一致——大小写不敏感、允许注释与尾随逗号，便于手工编辑；
    /// 未知字段忽略（向前兼容）。
    /// </remarks>
    public static class PluginManifestParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>反序列化清单原文；失败时返回错误而非抛出。</summary>
        public static PluginManifestParseResult Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PluginManifestParseResult(null, new[] { "plugin.json 内容为空" });
            }

            try
            {
                PluginManifest? manifest = JsonSerializer.Deserialize<PluginManifest>(json, Options);
                return manifest is null
                    ? new PluginManifestParseResult(null, new[] { "plugin.json 反序列化结果为空" })
                    : new PluginManifestParseResult(manifest, Array.Empty<string>());
            }
            catch (JsonException ex)
            {
                return new PluginManifestParseResult(null, new[] { $"plugin.json 不是合法 JSON：{ex.Message}" });
            }
        }
    }
}
