using System;
using System.IO;
using StarPie.PluginRuntime.Admission;
using Xunit;

namespace StarPie.Tests;

/// <summary>
/// WinTrustSignatureVerifier 的真实路径：无签名 / 文件缺席的负向结论。
/// Authenticode 正向路径需真实可信证书签发，CI 无此基建，由受管环境发布流程覆盖；
/// 判定行为（可信/不可信/pin 的裁决）在 PluginAdmissionPolicyTests 以替身覆盖。
/// </summary>
public sealed class PluginSignatureVerifierTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("starpie-sig-tests").FullName;
    private readonly WinTrustSignatureVerifier _verifier = new();

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void 无签名文件_Unsigned且无签名主体()
    {
        string path = Path.Combine(_tempRoot, "unsigned.dll");
        File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A });

        PluginSignatureCheck check = _verifier.Verify(path);

        Assert.Equal(PluginSignatureStatus.Unsigned, check.Status);
        Assert.Null(check.Subject);
        Assert.Null(check.PublisherHash);
        Assert.False(string.IsNullOrWhiteSpace(check.Detail));
    }

    [Fact]
    public void 文件不存在_Unsigned不抛异常()
    {
        PluginSignatureCheck check = _verifier.Verify(Path.Combine(_tempRoot, "missing.dll"));

        Assert.Equal(PluginSignatureStatus.Unsigned, check.Status);
    }
}
