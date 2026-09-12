using System;
using System.IO;
using StarPie.Manifest;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Manifest;

namespace StarPie.Tests;

/// <summary>
/// 插件包测试夹具：在给定插件根目录下写出一份「可发现、可通过校验」的最小插件包，
/// 需要违规场景时由用例覆写清单或追加文件。
/// </summary>
internal static class PluginTestPackage
{
    /// <summary>默认入口程序集文件名。</summary>
    internal const string DefaultEntryAssembly = "Example.Plugin.dll";

    /// <summary>程序来源能力声明（借 SDK 的 IProgramScanner 契约做能力夹具）。</summary>
    internal const string ProgramSourceCapabilitiesJson = """[{ "id": "program-source", "abi": 1 }]""";

    /// <summary>建包 + 拷入口程序集 + 解析清单，返回「开发者模式放行」的装载请求。</summary>
    internal static PluginLoadRequest CreateLoadRequest(
        string pluginsRoot,
        string pluginId,
        Type entryType,
        string? entryTypeName = null,
        string? capabilitiesJson = null,
        int priority = 0)
    {
        string entryAssemblyName = Path.GetFileName(entryType.Assembly.Location);
        string packageDirectory = Create(
            pluginsRoot,
            pluginId,
            Manifest(
                pluginId,
                entryAssembly: entryAssemblyName,
                entryType: entryTypeName ?? entryType.FullName!,
                priority: priority,
                capabilitiesJson: capabilitiesJson));
        File.Copy(
            entryType.Assembly.Location,
            Path.Combine(packageDirectory, entryAssemblyName),
            overwrite: true);
        PluginManifest manifest = PluginManifestParser
            .Parse(File.ReadAllText(Path.Combine(packageDirectory, "plugin.json")))
            .Manifest!;
        return new PluginLoadRequest(
            manifest,
            packageDirectory,
            new PluginAdmissionDecision(
                PluginAdmission.DeveloperMode,
                "开发者模式：未命中审核清单，经开发者模式放行"));
    }

    /// <summary>写出插件包目录并返回其路径。</summary>
    internal static string Create(
        string pluginsRoot,
        string pluginId,
        string? manifestJson = null,
        string entryAssembly = DefaultEntryAssembly,
        bool writeEntryAssembly = true)
    {
        string packageDirectory = Path.Combine(pluginsRoot, pluginId);
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(
            Path.Combine(packageDirectory, "plugin.json"),
            manifestJson ?? Manifest(pluginId, entryAssembly: entryAssembly));
        if (writeEntryAssembly)
        {
            File.WriteAllBytes(Path.Combine(packageDirectory, entryAssembly), new byte[] { 0x4D, 0x5A });
        }

        return packageDirectory;
    }

    /// <summary>生成一份字段齐全的清单（可选覆写版本、SDK ABI、priority 与 capabilities 声明）。</summary>
    internal static string Manifest(
        string pluginId,
        string version = "1.0.0",
        string sdk = "1.0",
        string entryAssembly = DefaultEntryAssembly,
        string entryType = "Example.Plugin",
        string? uiSection = null,
        int priority = 0,
        string? capabilitiesJson = null)
    {
        string ui = uiSection is null ? string.Empty : ", \"ui\": { " + uiSection + " }";
        string priorityJson = priority == 0 ? string.Empty : $", \"priority\": {priority}";
        string capabilities = capabilitiesJson is null
            ? string.Empty
            : ", \"capabilities\": " + capabilitiesJson;
        return $$"""
            {
              "schemaVersion": 1,
              "id": "{{pluginId}}",
              "name": "{{pluginId}}",
              "version": "{{version}}",
              "sdk": "{{sdk}}",
              "entryAssembly": "{{entryAssembly}}",
              "entryType": "{{entryType}}"{{priorityJson}}{{capabilities}}{{ui}}
            }
            """;
    }
}
