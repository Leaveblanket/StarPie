using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StarPie.PluginRuntime.Admission
{
    /// <summary>审核清单对单个 (pluginId, version) 的裁决三态。</summary>
    public enum PluginReviewOutcome
    {
        /// <summary>未命中清单：按未审核处理（开发者模式开启时可经开发者模式放行）。</summary>
        NotListed,

        /// <summary>命中审核白名单。</summary>
        Reviewed,

        /// <summary>命中版本级黑名单（撤销）。</summary>
        Revoked,
    }

    /// <summary>审核清单裁决：三态 + 可读说明（撤销/未命中时给出可定位原因）。</summary>
    /// <param name="Outcome">裁决三态。</param>
    /// <param name="Detail">可读说明；命中撤销时含被撤销版本。</param>
    public sealed record PluginReviewDecision(PluginReviewOutcome Outcome, string? Detail);

    /// <summary>
    /// 签名审核清单：JSON 文档 + 分离 RSA-SHA256 签名（<c>&lt;catalog&gt;.sig</c>，base64），
    /// 公钥 pin 在宿主侧，文件可离线校验。文件缺失、签名校验失败或解析失败一律降级为
    /// 空清单（保守：第三方无命中即拒，宁可拒绝不误放行）。
    /// </summary>
    /// <remarks>
    /// 清单在宿主启动时读取一次并缓存：撤销通道 = 更新清单文件后重启（与"每次启动按当前清单
    /// 重新判定"的语义一致）。白名单按 (pluginId, version) 精确命中——清单未列入的新版本不会
    /// 因旧版本已审核而放行，须重新审核。发布密钥建立前（<see cref="FirstPartyPublicKeyPem"/> 为空）
    /// 任何清单都不予采信。
    /// </remarks>
    public sealed class SignedPluginReviewCatalog : IPluginReviewCatalog
    {
        /// <summary>
        /// 首方发布公钥 pin（PEM）。宿主只认本钥匙签发的清单：更新通道 = 用配对私钥重签清单文件对，
        /// 私钥不入仓库（维护者本机 <c>~/.starpie-keys/review-catalog-private.pem</c>，
        /// 签名工具 <c>scripts/sign-review-catalog.ps1</c>）。随仓库清单自带 selfcheck 条目，
        /// 由 xUnit 验证 pin 与清单未漂移。
        /// </summary>
        public const string FirstPartyPublicKeyPem = """
            -----BEGIN RSA PUBLIC KEY-----
            MIIBCgKCAQEA2LFbSFLuwym/BR5Gr1eU+ozFJZGx0l/qJeSDOzB9o5vZ26X9EeYc
            BH5oFwJwXJE0NilIKU03Kg2m99Balbtv7ZQoswE45jEyVTpI4OSvLbw+49+0oA0k
            O5vm1OZX2mr4Chtj36HsaMHjCxRfCikaM1Z9cABIgdrLlw9m9sKI8uU0ciueJPWA
            zSocFtVL4ZqxptiVPz/izkPWzmzvqRUWBShzHALkRjEGTsOO+BeyiXVTwS5lzeVr
            J7WKTGiaKyabFgJ23GJ/T3FPEL0qcSxbgG9QX7eT8bOzJMrjfGOFGWG72nEFf9hl
            Fi68yesEhkIvWrBEDZCcaIaolJA0D8mgeQIDAQAB
            -----END RSA PUBLIC KEY-----
            """;

        private static readonly JsonSerializerOptions DocumentOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly string _catalogPath;
        private readonly string? _publicKeyPem;
        private readonly Dictionary<string, ReviewCatalogEntry> _entries;

        /// <summary>构造清单：读取、验签并索引；校验失败即空清单，不抛异常。</summary>
        /// <param name="catalogPath">清单 JSON 路径；分离签名为 <c>&lt;catalogPath&gt;.sig</c>。</param>
        /// <param name="publicKeyPem">验签用公钥 PEM；null 或空即不采信任何清单。</param>
        public SignedPluginReviewCatalog(string catalogPath, string? publicKeyPem)
        {
            _catalogPath = catalogPath;
            _publicKeyPem = publicKeyPem;
            _entries = LoadVerifiedEntries(catalogPath, publicKeyPem);
        }

        /// <inheritdoc/>
        public bool IsReviewed(string pluginId, string version)
            => Review(pluginId, version).Outcome == PluginReviewOutcome.Reviewed;

        /// <inheritdoc/>
        public PluginReviewDecision Review(string pluginId, string version)
        {
            if (string.IsNullOrWhiteSpace(pluginId)
                || !_entries.TryGetValue(pluginId, out ReviewCatalogEntry? entry))
            {
                return new PluginReviewDecision(PluginReviewOutcome.NotListed, null);
            }

            if (version is not null
                && entry.RevokedVersions?.Contains(version) == true)
            {
                return new PluginReviewDecision(
                    PluginReviewOutcome.Revoked, $"版本 {version} 在审核清单黑名单中");
            }

            if (version is not null
                && entry.Versions?.Contains(version) == true)
            {
                return new PluginReviewDecision(PluginReviewOutcome.Reviewed, null);
            }

            return new PluginReviewDecision(PluginReviewOutcome.NotListed, "版本未列入清单");
        }

        private static Dictionary<string, ReviewCatalogEntry> LoadVerifiedEntries(
            string catalogPath, string? publicKeyPem)
        {
            if (string.IsNullOrWhiteSpace(publicKeyPem))
            {
                // 发布公钥尚未 pin：任何清单都不予采信（保守拒绝），留调试日志供取证。
                System.Diagnostics.Debug.WriteLine("Plugin review catalog not trusted: no pinned public key");
                return new Dictionary<string, ReviewCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                if (!File.Exists(catalogPath) || !File.Exists(catalogPath + ".sig"))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Plugin review catalog absent: {catalogPath}(+.sig)，按空清单保守拒绝");
                    return new Dictionary<string, ReviewCatalogEntry>(StringComparer.OrdinalIgnoreCase);
                }

                byte[] document = File.ReadAllBytes(catalogPath);
                byte[] signature = Convert.FromBase64String(File.ReadAllText(catalogPath + ".sig").Trim());
                using RSA rsa = RSA.Create();
                rsa.ImportFromPem(publicKeyPem);
                if (!rsa.VerifyData(document, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    return new Dictionary<string, ReviewCatalogEntry>(StringComparer.OrdinalIgnoreCase);
                }

                ReviewCatalogDocument? parsed = JsonSerializer.Deserialize<ReviewCatalogDocument>(
                    document, DocumentOptions);
                var entries = new Dictionary<string, ReviewCatalogEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (ReviewCatalogEntry? entry in parsed?.Entries ?? new List<ReviewCatalogEntry>())
                {
                    if (!string.IsNullOrWhiteSpace(entry?.PluginId))
                    {
                        entries[entry.PluginId] = entry;
                    }
                }

                return entries;
            }
            catch (Exception exception) when (
                exception is IOException or JsonException or FormatException
                    or CryptographicException or ArgumentException)
            {
                // 清单/签名损坏按空清单降级（保守拒绝），原因进调试日志供取证。
                System.Diagnostics.Debug.WriteLine($"Plugin review catalog rejected: {exception.Message}");
                return new Dictionary<string, ReviewCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private sealed record ReviewCatalogDocument(List<ReviewCatalogEntry>? Entries);

        private sealed record ReviewCatalogEntry(
            string? PluginId,
            List<string>? Versions,
            List<string>? RevokedVersions);
    }
}
