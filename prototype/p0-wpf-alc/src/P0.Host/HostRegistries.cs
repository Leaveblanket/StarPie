using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using P0.Contracts;

namespace P0.Host;

/// <summary>插件提交的资产（Descriptor 语义：只带工厂，宿主决定创建时机）。</summary>
internal sealed record PendingAsset(ProbeAssetKind Kind, string Name, Func<object> Factory);

/// <summary>资产登记表（对应 P3 <c>PluginUiAssetRegistry</c>）：卸载枚举与清零断言的唯一依据。</summary>
internal sealed class AssetRegistry
{
    /// <summary>已登记、待宿主创建的资产。</summary>
    public List<PendingAsset> Pending { get; } = new();

    /// <summary>宿主已创建的资产强引用（清理时整体清空）。</summary>
    public List<object> Live { get; } = new();

    /// <summary>宿主签发的句柄（定时器 / 订阅）。</summary>
    public List<IDisposable> Handles { get; } = new();

    /// <summary>插件窗口。</summary>
    public List<Window> PluginWindows { get; } = new();

    /// <summary>插件资源根（一次 merge、一次摘除）。</summary>
    public ResourceDictionary? PluginResourceRoot { get; set; }

    /// <summary>是否已经通过 pack URI 成功并入资源字典。</summary>
    public bool MergedViaUri { get; set; }

    /// <summary>探针回执：只允许字符串。</summary>
    public Dictionary<string, string> Reports { get; } = new(StringComparer.Ordinal);

    public void ClearAll()
    {
        Pending.Clear();
        Live.Clear();
        Handles.Clear();
        PluginWindows.Clear();
        PluginResourceRoot = null;
        MergedViaUri = false;
    }
}

/// <summary>宿主事件源（进程单例）：插件只能经宿主中介订阅，卸载即断。</summary>
internal static class HostEventHub
{
    private static event Action<string>? Ping;

    public static int SubscriberCount => Ping?.GetInvocationList().Length ?? 0;

    public static void Publish(string topic) => Ping?.Invoke(topic);

    /// <summary>宿主中介订阅：原样挂上插件委托，保证可按同一实例摘除。</summary>
    public static IDisposable Subscribe(Action<string> handler)
    {
        Ping += handler;
        return new SubscriptionHandle(handler);
    }

    private static void Unsubscribe(Action<string> handler) => Ping -= handler;

    private sealed class SubscriptionHandle : IDisposable
    {
        private Action<string>? _handler;
        private bool _disposed;

        public SubscriptionHandle(Action<string> handler) => _handler = handler;

        public void Dispose()
        {
            if (_disposed || _handler is null)
            {
                return;
            }

            _disposed = true;
            Unsubscribe(_handler);
            _handler = null; // 宿主侧缓存/委托不持有插件对象（§6.1 约束 6）
        }
    }
}

/// <summary>宿主签发的定时器句柄：可整体停止并断开回调。</summary>
internal sealed class TimerHandle : IDisposable
{
    private readonly DispatcherTimer _timer;
    private EventHandler? _handler;

    public TimerHandle(DispatcherTimer timer, EventHandler handler, Action tick)
    {
        _timer = timer;
        _handler = handler;
        Tick = tick;
    }

    /// <summary>插件侧回调（弱引用判定用）。</summary>
    public Action Tick { get; }

    public void Dispose()
    {
        _timer.Stop();
        if (_handler is not null)
        {
            _timer.Tick -= _handler;
            _handler = null;
        }
    }
}

/// <summary>宿主门面实现（对应 P3 <c>PluginUiHost</c>）。</summary>
internal sealed class ProbeContext : IProbeContext
{
    private readonly ProbeRun _run;

    public ProbeContext(ProbeRun run) => _run = run;

    public string Probe => _run.Name;

    private AssetRegistry Registry => _run.Registry;

    public void RegisterAsset(ProbeAssetKind kind, string name, Func<object> factory)
        => Registry.Pending.Add(new PendingAsset(kind, name, factory));

    public void MergeResourceDictionary(Uri packUri)
    {
        // 走 pack URI 路径：正是 P0 要验证的「插件程序集在 collectible ALC 内，pack URI 能否解析」。
        var dictionary = new ResourceDictionary { Source = packUri };
        MergeIntoPluginRoot(dictionary);
        Registry.MergedViaUri = true;
    }

    public void MergeIntoPluginRoot(ResourceDictionary dictionary)
    {
        Registry.PluginResourceRoot ??= new ResourceDictionary();
        Registry.PluginResourceRoot.MergedDictionaries.Add(dictionary);
        if (!Application.Current.Resources.MergedDictionaries.Contains(Registry.PluginResourceRoot))
        {
            Application.Current.Resources.MergedDictionaries.Add(Registry.PluginResourceRoot);
        }
    }

    public IDisposable CreateTimer(TimeSpan interval, Action tick)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher)
        {
            Interval = interval
        };
        EventHandler handler = (_, _) => tick();
        timer.Tick += handler;
        var handle = new TimerHandle(timer, handler, tick);
        Registry.Handles.Add(handle);
        _run.ObjectRefs.Add(new WeakReference(tick));
        timer.Start();
        return handle;
    }

    public IDisposable Subscribe(string topic, Action<string> handler)
    {
        var subscription = HostEventHub.Subscribe(handler);
        Registry.Handles.Add(subscription);
        _run.ObjectRefs.Add(new WeakReference(handler));
        return subscription;
    }

    public void Report(string key, string value) => Registry.Reports[key] = value;
}
