using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Discovery;
using StarPie.PluginRuntime.State;

namespace StarPie.Tests;

/// <summary>
/// 启动扫描缝：发现 → 校验 → 准入 → 状态刷新 → 启动报告，含同 id 冲突、包内副本与
/// 四态在报告中的可见性。
/// </summary>
public sealed class PluginStartupScannerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _installRoot;
    private readonly string _userRoot;
    private readonly string _statePath;
    private readonly string _reportPath;

    public PluginStartupScannerTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-scan-tests").FullName;
        _installRoot = Path.Combine(_tempRoot, "install", "plugins");
        _userRoot = Path.Combine(_tempRoot, "user", "plugins");
        _statePath = Path.Combine(_tempRoot, "data", "plugin-state.json");
        _reportPath = Path.Combine(_tempRoot, "data", "plugin-startup-report.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void 插件缺席_报告为空且四态计数全零_状态与报告仍落盘()
    {
        PluginStartupReport report = CreateScanner().Scan();

        Assert.Empty(report.Plugins);
        Assert.False(report.DeveloperModeEnabled);
        Assert.Equal(4, report.AdmissionCounts.Count);
        Assert.All(report.AdmissionCounts.Values, count => Assert.Equal(0, count));
        Assert.True(File.Exists(_statePath));
        Assert.True(File.Exists(_reportPath));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_reportPath));
        JsonElement counts = document.RootElement.GetProperty("AdmissionCounts");
        Assert.Equal(0, counts.GetProperty("BuiltIn").GetInt32());
        Assert.Equal(0, counts.GetProperty("Reviewed").GetInt32());
        Assert.Equal(0, counts.GetProperty("DeveloperMode").GetInt32());
        Assert.Equal(0, counts.GetProperty("Rejected").GetInt32());
    }

    [Fact]
    public void 开发者模式关闭_未命中清单的包被拒绝()
    {
        PluginTestPackage.Create(_installRoot, "com.example.side");

        PluginStartupReport report = CreateScanner().Scan();

        PluginStartupReportEntry entry = Assert.Single(report.Plugins);
        Assert.Equal(PluginAdmission.Rejected, entry.Admission);
        Assert.Contains("开发者模式", entry.AdmissionReason);
        Assert.False(report.DeveloperModeEnabled);
    }

    [Fact]
    public void 一次扫描_准入四态在启动报告中全部可见()
    {
        EnableDeveloperMode();
        PluginTestPackage.Create(_installRoot, "com.example.builtin");
        PluginTestPackage.Create(_installRoot, "com.example.reviewed");
        PluginTestPackage.Create(_installRoot, "com.example.side");
        PluginTestPackage.Create(_installRoot, "com.example.broken", "{ \"schemaVersion\": 2 }");

        PluginStartupReport report = CreateScanner(
            builtInIds: new[] { "com.example.builtin" },
            reviewCatalog: new TestReviewCatalog("com.example.reviewed")).Scan();

        Assert.True(report.DeveloperModeEnabled);
        Assert.Equal(4, report.Plugins.Count);
        Assert.Equal(PluginAdmission.BuiltIn, Find(report, "com.example.builtin").Admission);
        Assert.Equal(PluginAdmission.Reviewed, Find(report, "com.example.reviewed").Admission);
        Assert.Equal(PluginAdmission.DeveloperMode, Find(report, "com.example.side").Admission);
        Assert.Equal(PluginAdmission.Rejected, Find(report, "com.example.broken").Admission);
        Assert.Equal(1, report.AdmissionCounts[PluginAdmission.BuiltIn]);
        Assert.Equal(1, report.AdmissionCounts[PluginAdmission.Reviewed]);
        Assert.Equal(1, report.AdmissionCounts[PluginAdmission.DeveloperMode]);
        Assert.Equal(1, report.AdmissionCounts[PluginAdmission.Rejected]);

        // 报告落盘后四态名称可见（诊断面可直接读出当前处于哪一态）。
        string json = File.ReadAllText(_reportPath);
        Assert.Contains("\"BuiltIn\"", json);
        Assert.Contains("\"Reviewed\"", json);
        Assert.Contains("\"DeveloperMode\"", json);
        Assert.Contains("\"Rejected\"", json);
    }

    [Fact]
    public void 同id冲突_两侧均拒_报告与状态列出两份路径()
    {
        string installPackage = PluginTestPackage.Create(_installRoot, "com.example.clash");
        string userPackage = PluginTestPackage.Create(_userRoot, "com.example.clash");

        PluginStartupReport report = CreateScanner(builtInIds: new[] { "com.example.clash" }).Scan();

        PluginStartupReportEntry entry = Assert.Single(report.Plugins);
        Assert.Equal(PluginAdmission.Rejected, entry.Admission);
        Assert.Contains("同 id 冲突", entry.AdmissionReason);
        Assert.Equal(new[] { installPackage, userPackage }, entry.PackagePaths);

        // 宿主状态同样留痕：拒绝原因含双方路径，重启后扫描仍按冲突处理。
        PluginStateEntry state = LoadState("com.example.clash");
        Assert.Equal(PluginAdmission.Rejected, state.Admission);
        Assert.Contains("同 id 冲突", state.AdmissionReason);
        Assert.Contains(userPackage, state.AdmissionReason);
    }

    [Fact]
    public void 包内出现SDK副本_拒绝且原因含副本文件名()
    {
        string package = PluginTestPackage.Create(_installRoot, "com.example.a");
        Directory.CreateDirectory(Path.Combine(package, "lib"));
        File.WriteAllBytes(Path.Combine(package, "lib", "StarPie.Sdk.Wpf.dll"), new byte[] { 0x4D, 0x5A });

        PluginStartupReport report = CreateScanner(
            builtInIds: new[] { "com.example.a" },
            reviewCatalog: new TestReviewCatalog("com.example.a")).Scan();

        PluginStartupReportEntry entry = Assert.Single(report.Plugins);
        Assert.Equal(PluginAdmission.Rejected, entry.Admission);
        Assert.Contains("StarPie.Sdk.Wpf.dll", entry.AdmissionReason);
    }

    [Fact]
    public void 校验失败_拒绝并附具体原因_状态同步准入结果()
    {
        string package = PluginTestPackage.Create(
            _installRoot, "com.example.a", PluginTestPackage.Manifest("com.example.a", sdk: "9.9"));
        EnableDeveloperMode();

        PluginStartupReport report = CreateScanner().Scan();

        PluginStartupReportEntry entry = Assert.Single(report.Plugins);
        Assert.Equal(PluginAdmission.Rejected, entry.Admission);
        Assert.Contains("SDK ABI 不兼容", entry.AdmissionReason);

        PluginStateEntry state = LoadState("com.example.a");
        Assert.Equal(PluginAdmission.Rejected, state.Admission);
        Assert.Equal(package, state.PackagePath);
        Assert.Equal("1.0.0", state.Version);
    }

    [Fact]
    public void 扫描刷新状态_版本路径与准入来源落盘()
    {
        string package = PluginTestPackage.Create(
            _installRoot, "com.example.a", PluginTestPackage.Manifest("com.example.a", version: "2.3.4"));
        EnableDeveloperMode();

        CreateScanner().Scan();

        PluginStateEntry state = LoadState("com.example.a");
        Assert.Equal("2.3.4", state.Version);
        Assert.Equal(package, state.PackagePath);
        Assert.Equal(PluginAdmission.DeveloperMode, state.Admission);
        Assert.False(string.IsNullOrWhiteSpace(state.AdmissionReason));
        Assert.Null(state.Quarantine);
        Assert.True(state.Enabled);
    }

    [Fact]
    public void 停用意图跨扫描保留_不被准入结果冲掉()
    {
        PluginTestPackage.Create(_installRoot, "com.example.a");
        CreateScanner().Scan();
        var store = new PluginStateStore(_statePath);
        store.Load();
        store.Current.Plugins["com.example.a"].Enabled = false;
        store.Save();

        PluginStartupReport report = CreateScanner().Scan();

        PluginStartupReportEntry entry = Assert.Single(report.Plugins);
        Assert.False(entry.Enabled);
        Assert.False(LoadState("com.example.a").Enabled);
    }

    private PluginStartupScanner CreateScanner(
        IEnumerable<string>? builtInIds = null,
        IPluginReviewCatalog? reviewCatalog = null,
        IPluginSignatureVerifier? signatureVerifier = null)
        => new(
            new PluginDiscovery(_installRoot, _userRoot),
            new PluginAdmissionPolicy(builtInIds ?? Array.Empty<string>(), reviewCatalog),
            new PluginStateStore(_statePath),
            new PluginStartupReportWriter(_reportPath),
            signatureVerifier ?? new TestSignatureVerifier());

    [Fact]
    public void 启动重判_命中版本级撤销_拒绝且状态更新()
    {
        string catalogPath = Path.Combine(_tempRoot, "review-catalog.json");
        PluginTestPackage.Create(
            _installRoot, "com.example.a", PluginTestPackage.Manifest("com.example.a", version: "1.0.0"));
        using var signedCatalog = new TestSignedCatalog();
        string publicKeyPem = signedCatalog.Write(
            catalogPath,
            """{ "Entries": [ { "PluginId": "com.example.a", "Versions": ["1.0.0"] } ] }""");

        // 第一次扫描：版本命中审核清单，准予装载。
        PluginStartupReport first = CreateScanner(
            reviewCatalog: new SignedPluginReviewCatalog(catalogPath, publicKeyPem)).Scan();
        Assert.Equal(PluginAdmission.Reviewed, Find(first, "com.example.a").Admission);

        // 清单更新把该版本列入黑名单：下一次启动按当前清单重新判定，命中即停。
        signedCatalog.Write(
            catalogPath,
            """{ "Entries": [ { "PluginId": "com.example.a", "Versions": ["1.0.0"], "RevokedVersions": ["1.0.0"] } ] }""");
        PluginStartupReport second = CreateScanner(
            reviewCatalog: new SignedPluginReviewCatalog(catalogPath, publicKeyPem)).Scan();

        PluginStartupReportEntry entry = Find(second, "com.example.a");
        Assert.Equal(PluginAdmission.Rejected, entry.Admission);
        Assert.Contains("已撤销", entry.AdmissionReason);
        PluginStateEntry state = LoadState("com.example.a");
        Assert.Equal(PluginAdmission.Rejected, state.Admission);
        Assert.Contains("已撤销", state.AdmissionReason);
        // 重判只影响装载资格，不翻转用户启停意图：撤销解除后插件自动回到可装载。
        Assert.True(state.Enabled);

        signedCatalog.Write(
            catalogPath,
            """{ "Entries": [ { "PluginId": "com.example.a", "Versions": ["1.0.0"] } ] }""");
        PluginStartupReport third = CreateScanner(
            reviewCatalog: new SignedPluginReviewCatalog(catalogPath, publicKeyPem)).Scan();

        Assert.Equal(PluginAdmission.Reviewed, Find(third, "com.example.a").Admission);
        Assert.True(LoadState("com.example.a").Enabled);
    }

    [Fact]
    public void 审核清单命中但无签名_拒绝()
    {
        PluginTestPackage.Create(_installRoot, "com.example.a");

        PluginStartupReport report = CreateScanner(
            reviewCatalog: new TestReviewCatalog("com.example.a"),
            signatureVerifier: new TestSignatureVerifier(
                status: PluginSignatureStatus.Unsigned, subject: null, publisherHash: null)).Scan();

        PluginStartupReportEntry entry = Assert.Single(report.Plugins);
        Assert.Equal(PluginAdmission.Rejected, entry.Admission);
        Assert.Contains("签名不可信", entry.AdmissionReason);
    }

    [Fact]
    public void 签名主体进报告与宿主状态()
    {
        PluginTestPackage.Create(_installRoot, "com.example.builtin");

        PluginStartupReport report = CreateScanner(
            builtInIds: new[] { "com.example.builtin" },
            signatureVerifier: new TestSignatureVerifier(subject: "CN=Test Publisher")).Scan();

        Assert.Equal("CN=Test Publisher", Find(report, "com.example.builtin").SignatureSubject);
        Assert.Equal("CN=Test Publisher", LoadState("com.example.builtin").SignatureSubject);
    }

    private void EnableDeveloperMode()
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        store.Current.DeveloperModeEnabled = true;
        store.Save();
    }

    private PluginStateEntry LoadState(string pluginId)
    {
        var store = new PluginStateStore(_statePath);
        store.Load();
        return store.Current.Plugins[pluginId];
    }

    private static PluginStartupReportEntry Find(PluginStartupReport report, string pluginId)
        => report.Plugins.Single(entry => entry.PluginId == pluginId);
}
