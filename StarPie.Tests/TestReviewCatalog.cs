using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using StarPie.PluginRuntime.Admission;

namespace StarPie.Tests;

/// <summary>审核清单测试替身：按插件 id 命中固定集合，版本维度不参与判定。</summary>
internal sealed class TestReviewCatalog : IPluginReviewCatalog
{
    private readonly HashSet<string> _reviewed;

    internal TestReviewCatalog(params string[] reviewedPluginIds)
    {
        _reviewed = new HashSet<string>(reviewedPluginIds, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsReviewed(string pluginId, string version) => _reviewed.Contains(pluginId);
}

/// <summary>签名校验测试替身：对所有文件返回同一份固定结论（CI 无真实 Authenticode 签发能力）。</summary>
internal sealed class TestSignatureVerifier : IPluginSignatureVerifier
{
    private readonly PluginSignatureCheck _result;

    internal TestSignatureVerifier(
        PluginSignatureStatus status = PluginSignatureStatus.Valid,
        string? subject = "CN=StarPie Test Publisher",
        string? publisherHash = "AA BB CC")
        => _result = new PluginSignatureCheck(status, subject, publisherHash, ContentMismatch: false, null);

    public PluginSignatureCheck Verify(string filePath) => _result;
}

/// <summary>
/// 签名清单测试夹具：临时 RSA 密钥对签出真实可验的清单文件（CI 无 Authenticode 签发能力，
/// 清单签名走 RSA-SHA256 分离签名，与生产 SignedPluginReviewCatalog 同一验签路径）。
/// 同一实例持有同一密钥对：模拟「清单更新」时多次 Write 仍可与同一公钥 pin 配对。
/// </summary>
internal sealed class TestSignedCatalog : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);

    /// <summary>把 JSON 写到指定路径并附分离签名，返回公钥 PEM（供构造清单验签用）。</summary>
    internal string Write(string catalogPath, string json)
    {
        byte[] document = Encoding.UTF8.GetBytes(json);
        byte[] signature = _rsa.SignData(document, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        File.WriteAllBytes(catalogPath, document);
        File.WriteAllText(catalogPath + ".sig", Convert.ToBase64String(signature));
        return _rsa.ExportRSAPublicKeyPem();
    }

    public void Dispose() => _rsa.Dispose();
}
