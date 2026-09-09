using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace StarPie.Tests;

/// <summary>
/// 设计期字符串字典一致性测试（ADR-0025/#101）：DesignTimeStrings.xaml 是签入生成物，
/// 由 GenerateDesignTimeStrings.ps1 从 Strings.resx（zh-CN 中性）派生——测试锁
/// “键集一致 + 值与 resx 一致”，防止新增/修改文案键后漏再生成。纯文件级断言，不经容器。
/// </summary>
public sealed class DesignTimeStringsConsistencyTests
{
    private const string XamlKeyNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string LocalizationDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 4; i++) dir = dir.Parent!;
            return Path.Combine(dir.FullName, "StarPie.Core", "Services", "Localization");
        }
    }

    private static XmlDocument LoadResx()
    {
        string path = Path.Combine(LocalizationDirectory, "Strings.resx");
        Assert.True(File.Exists(path), $"language resource missing: {path}");

        var doc = new XmlDocument();
        doc.Load(path);
        return doc;
    }

    private static XmlDocument LoadDesignTimeStrings()
    {
        string path = Path.Combine(LocalizationDirectory, "DesignTimeStrings.xaml");
        Assert.True(File.Exists(path), $"design-time dictionary missing: {path}");

        var doc = new XmlDocument();
        doc.Load(path);
        return doc;
    }

    private static (SortedSet<string> Keys, Dictionary<string, string> Values) ReadResx(XmlDocument doc)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XmlNode data in doc.SelectNodes("/root/data")!)
        {
            string key = data.Attributes!["name"]!.Value;
            string value = data.SelectSingleNode("value")?.InnerText ?? "";
            keys.Add(key);
            values[key] = value;
        }
        return (keys, values);
    }

    private static (SortedSet<string> Keys, Dictionary<string, string> Values) ReadDictionary(XmlDocument doc)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XmlNode node in doc.SelectNodes("//*[local-name()='String']")!)
        {
            string key = node.Attributes!.OfType<XmlAttribute>()
                .First(a => a.NamespaceURI == XamlKeyNamespace)
                .Value;
            keys.Add(key);
            values[key] = node.InnerText;
        }
        return (keys, values);
    }

    [Fact]
    public void DesignTimeStrings键集_与StringsResx键集一致()
    {
        var (resxKeys, _) = ReadResx(LoadResx());
        var (dictKeys, _) = ReadDictionary(LoadDesignTimeStrings());

        Assert.True(resxKeys.Count >= 200, $"expected full language table, got {resxKeys.Count}");

        var missing = resxKeys.Except(dictKeys).ToList();
        var extra = dictKeys.Except(resxKeys).ToList();
        Assert.True(missing.Count == 0, $"DesignTimeStrings.xaml missing keys: {string.Join(", ", missing)}");
        Assert.True(extra.Count == 0, $"DesignTimeStrings.xaml has extra keys: {string.Join(", ", extra)}");
    }

    [Fact]
    public void DesignTimeStrings值_与StringsResx的zhCN值逐键一致()
    {
        var (_, resxValues) = ReadResx(LoadResx());
        var (_, dictValues) = ReadDictionary(LoadDesignTimeStrings());

        var mismatches = resxValues
            .Where(pair => !dictValues.TryGetValue(pair.Key, out string? actual) || actual != pair.Value)
            .Select(pair => pair.Key)
            .ToList();
        Assert.True(mismatches.Count == 0, $"DesignTimeStrings.xaml value mismatch for keys: {string.Join(", ", mismatches)}");
    }
}
