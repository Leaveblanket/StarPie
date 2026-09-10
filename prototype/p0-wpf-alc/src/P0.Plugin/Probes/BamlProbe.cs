using System;
using System.Windows;
using System.Windows.Markup;
using P0.Contracts;
using P0.Plugin.Models;
using P0.Plugin.Views;

namespace P0.Plugin.Probes;

/// <summary>
/// P0 问题 1：插件程序集在 collectible ALC 内时，BAML / pack URI 能否解析。
/// 宿主用同一探针跑两遍：`baml-noctx`（不进上下文反射域）与 `baml-ctx`（进入后）。
/// </summary>
public sealed class BamlProbe : IProbeEntry
{
    /// <summary>松散 XAML：含 clr-namespace + assembly 类型引用，必须经 Assembly.Load 解析。</summary>
    private const string LooseXaml = """
        <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:views="clr-namespace:P0.Plugin.Views;assembly=P0.Plugin">
            <views:ProbeView />
        </StackPanel>
        """;

    public void Configure(IProbeContext context)
    {
        // (1) BAML：插件 XAML 视图（InitializeComponent → Application.LoadComponent(相对 pack URI)）
        context.RegisterAsset(ProbeAssetKind.View, "baml-view",
            () => new ProbeView { DataContext = new ProbeVm { Title = "baml", Badge = "baml-probe" } });

        // (2) pack URI：宿主按 pack URI 解析插件包内资源字典
        try
        {
            context.MergeResourceDictionary(ProbeUris.Pack(ProbeUris.DictionaryPath));
            context.Report("packuri-merge", "ok");
        }
        catch (Exception ex)
        {
            context.Report("packuri-merge", $"fail: {ProbeErrors.Describe(ex)}");
        }

        // (3) 松散 XAML：XamlReader.Parse + clr-namespace/assembly 类型解析
        try
        {
            var parsed = XamlReader.Parse(LooseXaml);
            context.Report("loose-xaml", $"ok: {parsed?.GetType().Name}");
        }
        catch (Exception ex)
        {
            context.Report("loose-xaml", $"fail: {ProbeErrors.Describe(ex)}");
        }
    }

    public void Stop()
    {
    }
}
