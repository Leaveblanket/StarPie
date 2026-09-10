using System;
using System.Windows;

namespace P0.Contracts;

/// <summary>
/// 打样用资产类别，对应 ADR-0028 决策 5 首版白名单与 plugins.md §8 测试矩阵的八个条目。
/// </summary>
public enum ProbeAssetKind
{
    /// <summary>插件视图（UserControl / XAML）。</summary>
    View,

    /// <summary>插件窗口（Window / XAML）。</summary>
    Window,

    /// <summary>插件资源字典（XAML / pack URI）。</summary>
    ResourceDictionary,

    /// <summary>插件 DataTemplate。</summary>
    DataTemplate,

    /// <summary>宿主签发的 DispatcherTimer。</summary>
    Timer,

    /// <summary>宿主中介的动画（Storyboard）。</summary>
    Animation,

    /// <summary>宿主中介的事件订阅。</summary>
    Subscription,

    /// <summary>插件构造的 Binding。</summary>
    Binding
}

/// <summary>
/// 插件 UI 入口（打样版）。语义对齐 <c>IPluginUiModule.RegisterUi</c>：只注册、不创建。
/// </summary>
public interface IProbeEntry
{
    /// <summary>注册本探针的全部 UI 资产；宿主在此之后才创建实例。</summary>
    void Configure(IProbeContext context);

    /// <summary>等价于 <c>IPlugin.StopAsync</c> 的插件侧收尾。</summary>
    void Stop();
}

/// <summary>
/// 宿主门面（打样版）。对齐 <c>IPluginUiContext</c>：资产只经此登记，宿主是生命周期的唯一所有者。
/// </summary>
public interface IProbeContext
{
    /// <summary>本次探针名（宿主驱动，用于同一插件程序集内的多场景复用）。</summary>
    string Probe { get; }

    /// <summary>
    /// 提交资产工厂（Descriptor 只带工厂，宿主决定创建时机）。
    /// 宿主在 UI 线程调用 <paramref name="factory"/>，产物先入资产登记表再进视觉树。
    /// </summary>
    void RegisterAsset(ProbeAssetKind kind, string name, Func<object> factory);

    /// <summary>请求宿主把插件资源字典并入插件资源根（一次 merge、一次摘除）。</summary>
    void MergeResourceDictionary(Uri packUri);

    /// <summary>宿主签发的定时器，可整体停止。</summary>
    IDisposable CreateTimer(TimeSpan interval, Action tick);

    /// <summary>宿主中介的事件订阅，卸载即断。</summary>
    IDisposable Subscribe(string topic, Action<string> handler);

    /// <summary>探针回执（键值字符串，只用于报告，不得持有插件对象）。</summary>
    void Report(string key, string value);
}
