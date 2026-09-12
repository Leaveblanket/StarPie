using System;
using System.IO;
using System.Security.Cryptography;
using StarPie.PluginRuntime.Admission;
using Xunit;

namespace StarPie.Tests;

/// <summary>
/// 首方审核清单的 pin 与随仓库清单的漂移守护：公钥 pin 必须是可用 RSA 公钥，
/// 随仓库清单必须能用该 pin 验签并命中 selfcheck 条目——清单被篡改或钥匙对漂移时在此失败。
/// </summary>
public sealed class ReviewCatalogPinTests
{
    [Fact]
    public void 首方公钥pin_是可导入的RSA公钥()
    {
        Assert.Contains("BEGIN", SignedPluginReviewCatalog.FirstPartyPublicKeyPem);

        using var rsa = RSA.Create();
        var exception = Record.Exception(
            () => rsa.ImportFromPem(SignedPluginReviewCatalog.FirstPartyPublicKeyPem));
        Assert.Null(exception);
    }

    [Fact]
    public void 随仓库签名清单_经pin验签_命中selfcheck条目()
    {
        string repoRoot = FindRepoRoot();
        string catalogPath = Path.Combine(repoRoot, "plugins", "review-catalog.json");
        Assert.True(File.Exists(catalogPath), $"随仓库清单缺失：{catalogPath}");
        Assert.True(File.Exists(catalogPath + ".sig"), $"随仓库清单分离签名缺失：{catalogPath}.sig");

        var catalog = new SignedPluginReviewCatalog(catalogPath, SignedPluginReviewCatalog.FirstPartyPublicKeyPem);

        // 验签失败会降级为空清单（NotListed），因此命中即证明 pin 与签名有效。
        Assert.Equal(
            PluginReviewOutcome.Reviewed,
            catalog.Review("starpie.catalog.selfcheck", "1.0.0").Outcome);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StarPie.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
