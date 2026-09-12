using System.Linq;
using StarPie.PluginRuntime.Manifest;

namespace StarPie.Tests;

/// <summary>
/// 清单解析缝：JSON 语法与字段类型由解析器收口，语义规则归校验器——本文件只锁前者。
/// </summary>
public sealed class PluginManifestParserTests
{
    [Fact]
    public void Parse_完整清单_读出全部字段()
    {
        string json = """
            {
              "schemaVersion": 1,
              "id": "com.example.program-source",
              "name": "Example Program Source",
              "version": "1.2.3-beta.1",
              "sdk": "1.0",
              "ui": { "sdk": "1.0", "entryType": "Example.UiModule" },
              "entryAssembly": "StarPie.Plugin.Example.dll",
              "entryType": "Example.Plugin",
              "priority": -5,
              "capabilities": [ { "id": "program-source", "abi": 1 }, { "id": "icon-source", "abi": 2 } ],
              "settingsSchema": "settings.schema.json"
            }
            """;

        PluginManifestParseResult result = PluginManifestParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Manifest);
        Assert.Equal(1, result.Manifest!.SchemaVersion);
        Assert.Equal("com.example.program-source", result.Manifest.Id);
        Assert.Equal("Example Program Source", result.Manifest.Name);
        Assert.Equal("1.2.3-beta.1", result.Manifest.Version);
        Assert.Equal("1.0", result.Manifest.Sdk);
        Assert.Equal("StarPie.Plugin.Example.dll", result.Manifest.EntryAssembly);
        Assert.Equal("Example.Plugin", result.Manifest.EntryType);
        Assert.Equal(-5, result.Manifest.Priority);
        Assert.Equal("settings.schema.json", result.Manifest.SettingsSchema);
        Assert.Equal("Example.UiModule", result.Manifest.Ui!.EntryType);
        Assert.Equal(new[] { "program-source", "icon-source" }, result.Manifest.Capabilities!.Select(c => c.Id));
        Assert.Equal(new[] { 1, 2 }, result.Manifest.Capabilities!.Select(c => c.Abi));
    }

    [Fact]
    public void Parse_容忍大小写注释与尾随逗号()
    {
        string json = """
            {
              // 手改清单
              "SchemaVersion": 1,
              "ID": "com.example.a",
              "Name": "A",
              "VERSION": "1.0.0",
              "SDK": "1.0",
              "ENTRYASSEMBLY": "a.dll",
              "entrytype": "A.Plugin",
            }
            """;

        PluginManifestParseResult result = PluginManifestParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.Equal("com.example.a", result.Manifest!.Id);
        Assert.Equal("A.Plugin", result.Manifest.EntryType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    public void Parse_非法或空内容_返回错误且不抛(string json)
    {
        PluginManifestParseResult result = PluginManifestParser.Parse(json);

        Assert.Null(result.Manifest);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_缺字段_由校验层判定而不是解析层()
    {
        PluginManifestParseResult result = PluginManifestParser.Parse("{ \"schemaVersion\": 1 }");

        Assert.Empty(result.Errors);
        Assert.Null(result.Manifest!.Id);
        Assert.Null(result.Manifest.EntryAssembly);
        Assert.Equal(0, result.Manifest.Priority);
    }
}
