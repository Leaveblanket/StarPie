using System;
using System.IO;
using StarPie.PluginRuntime.Admission;
using Xunit;

namespace StarPie.Tests;

/// <summary>
/// 签名审核清单：白名单与版本级撤销的裁决、篡改/缺签名/缺公钥的保守降级。
/// </summary>
public sealed class PluginReviewCatalogTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("starpie-review-catalog-tests").FullName;
    private readonly TestSignedCatalog _catalog = new();

    public void Dispose()
    {
        _catalog.Dispose();
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void 签名清单_白名单命中_版本级撤销_未列入版本()
    {
        string catalogPath = Path.Combine(_tempRoot, "review-catalog.json");
        string publicKeyPem = _catalog.Write(
            catalogPath,
            """
            { "Entries": [ {
                "PluginId": "com.example.a",
                "Versions": ["1.0.0", "1.0.1"],
                "RevokedVersions": ["2.0.0"]
            } ] }
            """);
        var catalog = new SignedPluginReviewCatalog(catalogPath, publicKeyPem);

        Assert.Equal(PluginReviewOutcome.Reviewed, catalog.Review("com.example.a", "1.0.0").Outcome);
        Assert.True(catalog.IsReviewed("com.example.a", "1.0.1"));
        Assert.Equal(PluginReviewOutcome.Revoked, catalog.Review("com.example.a", "2.0.0").Outcome);
        Assert.Contains("2.0.0", catalog.Review("com.example.a", "2.0.0").Detail);

        // 未列入的新版本不因旧版本已审核而放行，须重新审核。
        Assert.Equal(PluginReviewOutcome.NotListed, catalog.Review("com.example.a", "9.9.9").Outcome);
        Assert.Equal(PluginReviewOutcome.NotListed, catalog.Review("com.example.other", "1.0.0").Outcome);
    }

    [Fact]
    public void 清单被篡改_验签失败_降级空清单()
    {
        string catalogPath = Path.Combine(_tempRoot, "review-catalog.json");
        string publicKeyPem = _catalog.Write(
            catalogPath,
            """{ "Entries": [ { "PluginId": "com.example.a", "Versions": ["1.0.0"] } ] }""");
        File.WriteAllText(catalogPath, File.ReadAllText(catalogPath).Replace("1.0.0", "9.9.9"));

        var catalog = new SignedPluginReviewCatalog(catalogPath, publicKeyPem);

        Assert.Equal(PluginReviewOutcome.NotListed, catalog.Review("com.example.a", "9.9.9").Outcome);
        Assert.False(catalog.IsReviewed("com.example.a", "1.0.0"));
    }

    [Fact]
    public void 签名文件缺失_降级空清单()
    {
        string catalogPath = Path.Combine(_tempRoot, "review-catalog.json");
        string publicKeyPem = _catalog.Write(
            catalogPath,
            """{ "Entries": [ { "PluginId": "com.example.a", "Versions": ["1.0.0"] } ] }""");
        File.Delete(catalogPath + ".sig");

        var catalog = new SignedPluginReviewCatalog(catalogPath, publicKeyPem);

        Assert.False(catalog.IsReviewed("com.example.a", "1.0.0"));
    }

    [Fact]
    public void 公钥pin为空_不采信任何清单()
    {
        string catalogPath = Path.Combine(_tempRoot, "review-catalog.json");
        _catalog.Write(
            catalogPath,
            """{ "Entries": [ { "PluginId": "com.example.a", "Versions": ["1.0.0"] } ] }""");

        var catalog = new SignedPluginReviewCatalog(catalogPath, SignedPluginReviewCatalog.FirstPartyPublicKeyPem);

        Assert.False(catalog.IsReviewed("com.example.a", "1.0.0"));
    }

    [Fact]
    public void 清单文件缺失_空清单()
    {
        var catalog = new SignedPluginReviewCatalog(
            Path.Combine(_tempRoot, "absent", "review-catalog.json"), TestPem());

        Assert.False(catalog.IsReviewed("com.example.a", "1.0.0"));
    }

    [Fact]
    public void 清单JSON损坏_降级空清单()
    {
        string catalogPath = Path.Combine(_tempRoot, "review-catalog.json");
        string publicKeyPem = _catalog.Write(catalogPath, "{ not json");

        var catalog = new SignedPluginReviewCatalog(catalogPath, publicKeyPem);

        Assert.False(catalog.IsReviewed("com.example.a", "1.0.0"));
    }

    private static string TestPem()
    {
        using System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create(2048);
        return rsa.ExportRSAPublicKeyPem();
    }
}
