using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Animation;
using P0.Contracts;
using P0.Plugin.Dictionaries;
using P0.Plugin.Models;
using P0.Plugin.Views;

namespace P0.Plugin.Probes;

internal static class ProbeUris
{
    public const string DictionaryPath = "Resources/ProbeDictionary.xaml";

    public static Uri Pack(string relativePath) =>
        new($"pack://application:,,,/{typeof(ProbeUris).Assembly.GetName().Name};component/{relativePath}");
}

/// <summary>把异常链摊平成一行，便于回执里看清真正原因。</summary>
internal static class ProbeErrors
{
    public static string Describe(Exception exception)
    {
        var text = $"{exception.GetType().Name}: {exception.Message}";
        var inner = exception.InnerException;
        var depth = 0;
        while (inner is not null && depth++ < 4)
        {
            text += $" <- {inner.GetType().Name}: {inner.Message}";
            inner = inner.InnerException;
        }

        return text;
    }
}

/// <summary>视图：插件 XAML UserControl + 绑定到插件 VM，进宿主容器。</summary>
public sealed class ViewProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
        => context.RegisterAsset(ProbeAssetKind.View, "view",
            () => new ProbeView { DataContext = new ProbeVm { Title = "view", Badge = "baml" } });

    public void Stop()
    {
    }
}

/// <summary>窗口：插件 XAML Window，宿主创建、显示、关闭。</summary>
public sealed class WindowProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
        => context.RegisterAsset(ProbeAssetKind.Window, "window",
            () => new ProbeWindow { DataContext = new ProbeVm { Title = "window" } });

    public void Stop()
    {
    }
}

/// <summary>资源字典：优先走 pack URI（宿主解析跨 ALC 资源），失败则回退工厂。</summary>
public sealed class ResourceDictionaryProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
    {
        try
        {
            context.MergeResourceDictionary(ProbeUris.Pack(ProbeUris.DictionaryPath));
            context.Report("resdict-uri", "ok");
        }
        catch (Exception ex)
        {
            context.Report("resdict-uri", $"fail: {ProbeErrors.Describe(ex)}");
        }

        context.RegisterAsset(ProbeAssetKind.ResourceDictionary, "dict-fallback", () => new ProbeDictionary());
    }

    public void Stop()
    {
    }
}

/// <summary>DataTemplate：并入宿主资源根后由宿主查找并套用到宿主容器。</summary>
public sealed class DataTemplateProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
    {
        try
        {
            context.MergeResourceDictionary(ProbeUris.Pack(ProbeUris.DictionaryPath));
            context.Report("template-uri", "ok");
        }
        catch (Exception ex)
        {
            context.Report("template-uri", $"fail: {ProbeErrors.Describe(ex)}");
        }

        context.RegisterAsset(ProbeAssetKind.DataTemplate, "template",
            () => Application.Current.TryFindResource("ProbeTemplate") ?? new ProbeDictionary()["ProbeTemplate"]);
    }

    public void Stop()
    {
    }
}

/// <summary>宿主签发的 DispatcherTimer：回调是插件委托。</summary>
public sealed class TimerProbe : IProbeEntry
{
    private int _ticks;

    public void Configure(IProbeContext context)
        => context.CreateTimer(TimeSpan.FromMilliseconds(20), () =>
        {
            _ticks++;
            context.Report("timer-ticks", _ticks.ToString());
        });

    public void Stop()
    {
    }
}

/// <summary>宿主中介的动画：插件构造 Storyboard，宿主在宿主元素上 BeginAnimation。</summary>
public sealed class AnimationProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
        => context.RegisterAsset(ProbeAssetKind.Animation, "animation", () =>
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(300))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(animation);
            return storyboard;
        });

    public void Stop()
    {
    }
}

/// <summary>宿主中介的事件订阅：卸载即断。</summary>
public sealed class SubscriptionProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
    {
        var handle = context.Subscribe("ping", message => context.Report("event-last", message));
        context.RegisterAsset(ProbeAssetKind.Subscription, "subscription-handle", () => handle);
    }

    public void Stop()
    {
    }
}

/// <summary>插件构造的 Binding：源是插件 VM，宿主把它挂到宿主 TextBlock 上。</summary>
public sealed class BindingProbe : IProbeEntry
{
    public void Configure(IProbeContext context)
        => context.RegisterAsset(ProbeAssetKind.Binding, "binding", () =>
        {
            var source = new ProbeVm { Title = "bound", Badge = "binding" };
            return new Binding(nameof(ProbeVm.Title)) { Source = source, Mode = BindingMode.OneWay };
        });

    public void Stop()
    {
    }
}
