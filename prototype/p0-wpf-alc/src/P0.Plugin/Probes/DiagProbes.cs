using System;
using System.Windows;
using P0.Contracts;
using P0.Plugin.Models;

namespace P0.Plugin.Probes;

/// <summary>诊断用：只实例化一个插件 POCO（无 XAML、无 WPF 类型）。</summary>
public sealed class DiagPocoProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
        => context.RegisterAsset(ProbeAssetKind.View, "diag-poco", () => new ProbeVm { Title = "poco" });

    public void Stop()
    {
    }
}

/// <summary>诊断用：BAML 字典，只含框架类型（用于区分「BAML 机制」与「插件类型注册」）。</summary>
public sealed class DiagFrameworkDictionaryProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
        => context.MergeResourceDictionary(ProbeUris.Pack("Resources/FrameworkOnlyDictionary.xaml"));

    public void Stop()
    {
    }
}

/// <summary>诊断用：BAML 字典，含插件类型（DataTemplate 的 DataType 指向插件 VM）。</summary>
public sealed class DiagBamlDictionaryProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
        => context.MergeResourceDictionary(ProbeUris.Pack(ProbeUris.DictionaryPath));

    public void Stop()
    {
    }
}
