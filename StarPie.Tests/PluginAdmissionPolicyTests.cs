using System;
using System.Collections.Generic;
using StarPie.PluginRuntime.Admission;

namespace StarPie.Tests;

/// <summary>
/// 准入判定缝：内置 / 审核清单 / 开发者模式 / 拒绝四态的判定顺序与原因。
/// </summary>
public sealed class PluginAdmissionPolicyTests
{
    [Fact]
    public void 命中内置清单_内置()
    {
        var policy = new PluginAdmissionPolicy(new[] { "com.example.builtin" });

        PluginAdmissionDecision decision = policy.Decide("com.example.builtin", "1.0.0", developerModeEnabled: false);

        Assert.Equal(PluginAdmission.BuiltIn, decision.Status);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void 命中审核清单_已审核()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>(), new TestReviewCatalog("com.example.reviewed"));

        PluginAdmissionDecision decision = policy.Decide("com.example.reviewed", "1.0.0", developerModeEnabled: false);

        Assert.Equal(PluginAdmission.Reviewed, decision.Status);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void 开发者模式开启且未命中清单_开发者模式()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>());

        PluginAdmissionDecision decision = policy.Decide("com.example.side", "1.0.0", developerModeEnabled: true);

        Assert.Equal(PluginAdmission.DeveloperMode, decision.Status);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void 开发者模式关闭且未命中清单_拒绝并给原因()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>());

        PluginAdmissionDecision decision = policy.Decide("com.example.side", "1.0.0", developerModeEnabled: false);

        Assert.Equal(PluginAdmission.Rejected, decision.Status);
        Assert.Contains("开发者模式", decision.Reason);
    }

    [Fact]
    public void 判定顺序_内置优先于审核清单()
    {
        var policy = new PluginAdmissionPolicy(new[] { "com.example.a" }, new TestReviewCatalog("com.example.a"));

        Assert.Equal(PluginAdmission.BuiltIn, policy.Decide("com.example.a", "1.0.0", developerModeEnabled: true).Status);
    }

    [Fact]
    public void 判定顺序_审核清单优先于开发者模式()
    {
        var policy = new PluginAdmissionPolicy(Array.Empty<string>(), new TestReviewCatalog("com.example.a"));

        Assert.Equal(PluginAdmission.Reviewed, policy.Decide("com.example.a", "1.0.0", developerModeEnabled: true).Status);
    }

    [Fact]
    public void 默认策略_无内置无审核清单_一律拒绝()
    {
        var policy = new PluginAdmissionPolicy(PluginAdmissionPolicy.DefaultBuiltInPluginIds);

        Assert.Equal(PluginAdmission.Rejected, policy.Decide("com.example.any", "1.0.0", developerModeEnabled: false).Status);
    }
}
