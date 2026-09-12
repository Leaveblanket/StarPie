using System;
using System.IO;
using StarPie.Compatibility;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.Discovery;

namespace StarPie.Tests;

/// <summary>
/// 清单与包语义校验缝：字段规则、SDK ABI 兼容、包目录一致性、声明文件存在性。
/// </summary>
public sealed class PluginManifestValidatorTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _pluginsRoot;

    public PluginManifestValidatorTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-validator-tests").FullName;
        _pluginsRoot = Path.Combine(_tempRoot, "plugins");
        Directory.CreateDirectory(_pluginsRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Validate_字段齐全且文件就位_零违规()
    {
        string package = PluginTestPackage.Create(_pluginsRoot, "com.example.a");

        IReadOnlyList<string> errors = PluginManifestValidator.Validate(
            PluginManifestParser.Parse(File.ReadAllText(Path.Combine(package, "plugin.json"))).Manifest,
            package);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_空清单_逐项报缺失字段()
    {
        string package = PluginTestPackage.Create(_pluginsRoot, "com.example.a", "{ \"schemaVersion\": 1 }");

        IReadOnlyList<string> errors = PluginManifestValidator.Validate(
            PluginManifestParser.Parse(File.ReadAllText(Path.Combine(package, "plugin.json"))).Manifest,
            package);

        Assert.Contains(errors, error => error.Contains("id"));
        Assert.Contains(errors, error => error.Contains("name"));
        Assert.Contains(errors, error => error.Contains("version"));
        Assert.Contains(errors, error => error.Contains("sdk", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("entryAssembly"));
        Assert.Contains(errors, error => error.Contains("entryType"));
    }

    [Theory]
    [InlineData("ExamplePlugin")]
    [InlineData("com.Example.A")]
    [InlineData("com..a")]
    [InlineData("com.example.a-")]
    public void Validate_id不是合法反向域名_报错(string pluginId)
    {
        string package = PluginTestPackage.Create(_pluginsRoot, pluginId);

        IReadOnlyList<string> errors = Validate(package);

        Assert.Contains(errors, error => error.Contains("id"));
    }

    [Fact]
    public void Validate_包目录名与id不一致_报错()
    {
        string package = PluginTestPackage.Create(_pluginsRoot, "com.example.dirname");
        File.Move(
            Path.Combine(package, "plugin.json"),
            Path.Combine(package, "plugin.json.moved"));
        string renamed = Path.Combine(_pluginsRoot, "com.example.other");
        Directory.Move(package, renamed);
        File.Move(Path.Combine(renamed, "plugin.json.moved"), Path.Combine(renamed, "plugin.json"));

        IReadOnlyList<string> errors = Validate(renamed);

        Assert.Contains(errors, error => error.Contains("目录名"));
    }

    [Theory]
    [MemberData(nameof(AbiViolationDeclarations))]
    public void Validate_SDK声明超出宿主ABI_报错(string sdk)
    {
        string package = PluginTestPackage.Create(
            _pluginsRoot, "com.example.a", PluginTestPackage.Manifest("com.example.a", sdk: sdk));

        IReadOnlyList<string> errors = Validate(package);

        Assert.Contains(errors, error => error.Contains("sdk", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>超出宿主 ABI 的声明：主版本更高、次版本更高与格式非法三类。</summary>
    public static IEnumerable<object[]> AbiViolationDeclarations()
    {
        yield return new object[] { $"{SdkAbi.MajorVersion + 1}.0" };
        yield return new object[] { $"{SdkAbi.MajorVersion}.{SdkAbi.MinorVersion + 1}" };
        yield return new object[] { "abc" };
    }

    [Fact]
    public void Validate_入口程序集缺失_报错()
    {
        string package = PluginTestPackage.Create(_pluginsRoot, "com.example.a", writeEntryAssembly: false);

        IReadOnlyList<string> errors = Validate(package);

        Assert.Contains(errors, error => error.Contains("entryAssembly"));
    }

    [Theory]
    [InlineData("..\\\\evil.dll")]
    [InlineData("/tmp/evil.dll")]
    [InlineData("evil.exe")]
    public void Validate_入口程序集不是包内裸dll名_报错(string entryAssembly)
    {
        string package = PluginTestPackage.Create(
            _pluginsRoot,
            "com.example.a",
            PluginTestPackage.Manifest("com.example.a", entryAssembly: entryAssembly));

        IReadOnlyList<string> errors = Validate(package);

        Assert.Contains(errors, error => error.Contains("entryAssembly"));
    }

    [Fact]
    public void Validate_ui段格式非法_报错()
    {
        string package = PluginTestPackage.Create(
            _pluginsRoot,
            "com.example.a",
            PluginTestPackage.Manifest("com.example.a", uiSection: "\"sdk\": \"v1\", \"entryType\": \"Ui\""));

        IReadOnlyList<string> errors = Validate(package);

        Assert.Contains(errors, error => error.Contains("ui.sdk"));
        Assert.Contains(errors, error => error.Contains("ui.entryType"));
    }

    [Fact]
    public void Validate_ui段ABI兼容判定不归校验层_只判格式()
    {
        // UI 侧 ABI 校验归 Ui 侧插件托管层；宿主只做纯格式校验，故 9.9 格式合法即通过。
        string package = PluginTestPackage.Create(
            _pluginsRoot,
            "com.example.a",
            PluginTestPackage.Manifest("com.example.a", uiSection: "\"sdk\": \"9.9\", \"entryType\": \"Example.UiModule\""));

        Assert.Empty(Validate(package));
    }

    [Fact]
    public void Validate_能力声明违规_逐条报错()
    {
        string manifest = """
            {
              "schemaVersion": 1,
              "id": "com.example.a",
              "name": "A",
              "version": "1.0.0",
              "sdk": "1.0",
              "entryAssembly": "Example.Plugin.dll",
              "entryType": "Example.Plugin",
              "capabilities": [
                { "id": "program-source", "abi": 0 },
                { "id": "Program Source", "abi": 1 },
                { "id": "program-source", "abi": 1 }
              ]
            }
            """;
        string package = PluginTestPackage.Create(_pluginsRoot, "com.example.a", manifest);

        IReadOnlyList<string> errors = Validate(package);

        Assert.Contains(errors, error => error.Contains("abi"));
        Assert.Contains(errors, error => error.Contains("capabilities[].id"));
        Assert.Contains(errors, error => error.Contains("重复"));
    }

    [Fact]
    public void Validate_声明了不存在的设置schema_报错()
    {
        string manifest = """
            {
              "schemaVersion": 1,
              "id": "com.example.a",
              "name": "A",
              "version": "1.0.0",
              "sdk": "1.0",
              "entryAssembly": "Example.Plugin.dll",
              "entryType": "Example.Plugin",
              "settingsSchema": "settings.schema.json"
            }
            """;
        string package = PluginTestPackage.Create(_pluginsRoot, "com.example.a", manifest);

        Assert.Contains(Validate(package), error => error.Contains("settingsSchema"));
    }

    private static IReadOnlyList<string> Validate(string packageDirectory)
        => PluginManifestValidator.Validate(
            PluginManifestParser.Parse(File.ReadAllText(Path.Combine(packageDirectory, "plugin.json"))).Manifest,
            packageDirectory);
}
