using System;
using System.Collections.Generic;
using StarPie.PluginRuntime.Admission;

namespace StarPie.Tests;

/// <summary>
/// 准入判定缝：内置 / 审核清单 / 开发者模式 / 拒绝四态的判定顺序与原因。
/// </summary>
public sealed class PluginAdmissionPolicyTests
{
    private static PluginSignatureCheck Trusted()
        => new(PluginSignatureStatus.Valid, "CN=Trusted", "AA BB CC", ContentMismatch: false, null);

    private static PluginSignatureCheck Untrusted()
        => new(PluginSignatureStatus.Invalid, "CN=SelfSigned", "DD EE FF", ContentMismatch: false, "存在签名但未通过可信链校验");

    private static PluginSignatureCheck Tampered()
        => new(PluginSignatureStatus.Invalid, "CN=Trusted", "DD EE FF", ContentMismatch: true, "内容摘要与签名不符：文件在签名后被篡改");

    [Fact]
    public void 命中内置清单_内置()
    {
        var policy = new PluginAdmissionPolicy(new[] { "com.example.builtin" });

        PluginAdmissionDecision decision = policy.Decide(
            "com.example.builtin", "1.0.0", developerModeEnabled: false, signature: Untrusted());

        // 内置不走签名闸：随包第一方插件在开发构建里没有 Authenticode 签名。
        Assert.Equal(PluginAdmission.BuiltIn, decision.Status);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void 命中审核清单且签名可信_已审核()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>(), new TestReviewCatalog("com.example.reviewed"));

        PluginAdmissionDecision decision = policy.Decide(
            "com.example.reviewed", "1.0.0", developerModeEnabled: false, signature: Trusted());

        Assert.Equal(PluginAdmission.Reviewed, decision.Status);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void 命中审核清单但签名不可信_拒绝并给原因()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>(), new TestReviewCatalog("com.example.reviewed"));

        // 无签名（Unsigned）与无校验结果（null）同等不可信：清单与签名属同一道闸。
        Assert.Equal(
            PluginAdmission.Rejected,
            policy.Decide("com.example.reviewed", "1.0.0", developerModeEnabled: false, signature: Untrusted()).Status);
        Assert.Equal(
            PluginAdmission.Rejected,
            policy.Decide("com.example.reviewed", "1.0.0", developerModeEnabled: false, signature: null).Status);
        PluginAdmissionDecision unsigned = policy.Decide(
            "com.example.reviewed", "1.0.0", developerModeEnabled: false,
            signature: new PluginSignatureCheck(PluginSignatureStatus.Unsigned, null, null, ContentMismatch: false, "文件无数字签名"));
        Assert.Equal(PluginAdmission.Rejected, unsigned.Status);
        Assert.Contains("签名不可信", unsigned.Reason);
    }

    [Fact]
    public void 签名链不可信但发布者指纹被pin_已审核()
    {
        var policy = new PluginAdmissionPolicy(
            Array.Empty<string>(),
            new TestReviewCatalog("com.example.reviewed"),
            pinnedPublisherHashes: new[] { "dd ee ff" });

        PluginAdmissionDecision decision = policy.Decide(
            "com.example.reviewed", "1.0.0", developerModeEnabled: false, signature: Untrusted());

        Assert.Equal(PluginAdmission.Reviewed, decision.Status);
    }

    [Fact]
    public void 内容被篡改_即使指纹被pin_仍拒绝()
    {
        // 发布者可信不等于文件可信：签名后的内容摘要失配（伪造/篡改）不能经 pin 放行。
        var policy = new PluginAdmissionPolicy(
            Array.Empty<string>(),
            new TestReviewCatalog("com.example.reviewed"),
            pinnedPublisherHashes: new[] { "dd ee ff" });

        PluginAdmissionDecision decision = policy.Decide(
            "com.example.reviewed", "1.0.0", developerModeEnabled: false, signature: Tampered());

        Assert.Equal(PluginAdmission.Rejected, decision.Status);
        Assert.Contains("签名不可信", decision.Reason);
    }

    [Fact]
    public void 命中版本级撤销_拒绝并给撤销原因()
    {
        var policy = new PluginAdmissionPolicy(
            Array.Empty<string>(), new RevokedOnlyReviewCatalog("com.example.revoked", "1.0.0"));

        PluginAdmissionDecision decision = policy.Decide(
            "com.example.revoked", "1.0.0", developerModeEnabled: false, signature: Trusted());

        Assert.Equal(PluginAdmission.Rejected, decision.Status);
        Assert.Contains("已撤销", decision.Reason);
        // 撤销优先于开发者模式：显式全信任也不能救回被撤销的版本。
        Assert.Equal(
            PluginAdmission.Rejected,
            policy.Decide("com.example.revoked", "1.0.0", developerModeEnabled: true, signature: Trusted()).Status);
    }

    [Fact]
    public void 开发者模式开启且未命中清单_开发者模式()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>());

        PluginAdmissionDecision decision = policy.Decide(
            "com.example.side", "1.0.0", developerModeEnabled: true, signature: null);

        Assert.Equal(PluginAdmission.DeveloperMode, decision.Status);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void 开发者模式关闭且未命中清单_拒绝并给原因()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>());

        PluginAdmissionDecision decision = policy.Decide(
            "com.example.side", "1.0.0", developerModeEnabled: false, signature: null);

        Assert.Equal(PluginAdmission.Rejected, decision.Status);
        Assert.Contains("开发者模式", decision.Reason);
    }

    [Fact]
    public void 判定顺序_内置优先于审核清单()
    {
        var policy = new PluginAdmissionPolicy(new[] { "com.example.a" }, new TestReviewCatalog("com.example.a"));

        Assert.Equal(
            PluginAdmission.BuiltIn,
            policy.Decide("com.example.a", "1.0.0", developerModeEnabled: true, signature: null).Status);
    }

    [Fact]
    public void 判定顺序_审核清单优先于开发者模式()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>(), new TestReviewCatalog("com.example.a"));

        Assert.Equal(
            PluginAdmission.Reviewed,
            policy.Decide("com.example.a", "1.0.0", developerModeEnabled: true, signature: Trusted()).Status);
    }

    [Fact]
    public void 默认策略_无内置无审核清单_一律拒绝()
    {
        var policy = new PluginAdmissionPolicy(PluginAdmissionPolicy.DefaultBuiltInPluginIds);

        Assert.Equal(
            PluginAdmission.Rejected,
            policy.Decide("com.example.any", "1.0.0", developerModeEnabled: false, signature: Trusted()).Status);
    }
}

/// <summary>撤销清单测试替身：对指定 (id, version) 给出版本级撤销裁决，其余未列入。</summary>
internal sealed class RevokedOnlyReviewCatalog : IPluginReviewCatalog
{
    private readonly string _pluginId;
    private readonly string _revokedVersion;

    internal RevokedOnlyReviewCatalog(string pluginId, string revokedVersion)
    {
        _pluginId = pluginId;
        _revokedVersion = revokedVersion;
    }

    public bool IsReviewed(string pluginId, string version) => false;

    public PluginReviewDecision Review(string pluginId, string version)
        => pluginId == _pluginId && version == _revokedVersion
            ? new PluginReviewDecision(PluginReviewOutcome.Revoked, $"版本 {version} 在审核清单黑名单中")
            : new PluginReviewDecision(PluginReviewOutcome.NotListed, null);
}
