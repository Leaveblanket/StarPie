using System;
using System.Windows;
using System.Windows.Controls;
using P0.Contracts;
using P0.Plugin.Models;
using P0.Plugin.Views;

namespace P0.Plugin.Probes;

/// <summary>负对照：插件静态缓存。期望结果 = 未回收（判定器必须能看见"假泄漏"之外的真泄漏）。</summary>
public sealed class NegStaticCacheProbe : IProbeEntry
{
    private static object? _cache;

    public void Configure(IProbeContext context)
        => context.RegisterAsset(ProbeAssetKind.View, "neg-static-view",
            () => _cache = new ProbeView { DataContext = new ProbeVm { Title = "neg-static-cache" } });

    public void Stop()
    {
    }
}

/// <summary>负对照：绕过宿主契约，直接把资源字典并进 Application.Current.Resources。</summary>
public sealed class NegAppResourcesProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
    {
        // 注意：Application.LoadComponent(绝对 pack URI) 在 .NET Core 会抛「无法使用绝对 URI」。
        // 这里用插件自有 ResourceDictionary 类型（BAML），宿主才可能在全局根里认出它。
        var dictionary = new P0.Plugin.Dictionaries.ProbeDictionary();
        Application.Current.Resources.MergedDictionaries.Add(dictionary);
        context.Report("bypass", $"直接并入全局资源：{dictionary.GetType().FullName}（宿主登记表不可见）");
    }

    public void Stop()
    {
    }
}

/// <summary>负对照：插件自建 DependencyProperty（DP 注册表按 ownerType 记账）。</summary>
public sealed class NegDependencyPropertyProbe : IProbeEntry
{
    public sealed class ProbeControl : Control
    {
        public static readonly DependencyProperty ProbeTagProperty =
            DependencyProperty.Register(nameof(ProbeTag), typeof(string), typeof(ProbeControl), new PropertyMetadata("probe"));

        public string ProbeTag
        {
            get => (string)GetValue(ProbeTagProperty);
            set => SetValue(ProbeTagProperty, value);
        }
    }

    public void Configure(IProbeContext context)
        => context.RegisterAsset(ProbeAssetKind.View, "neg-dp-view", () =>
        {
            var control = new ProbeControl();
            control.SetValue(ProbeControl.ProbeTagProperty, "neg");
            return control;
        });

    public void Stop()
    {
    }
}
