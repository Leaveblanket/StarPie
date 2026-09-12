using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using StarPie.Abstractions;
using StarPie.Abstractions.Ui;

namespace StarPie.Tests;

/// <summary>
/// 负对照夹具（卸载矩阵用）：绕过宿主契约的 UI 插件——直并全局资源字典 + 自建窗口直接显示。
/// </summary>
/// <remarks>
/// 两类越权都是 plugins.md §5.2 不支持列表里的行为：不在受支持特性白名单内，宿主不予背书；
/// 泄漏验证器必须按 plugin id 定位命中并把卸载收口成隔离，而不是谎报成功。
/// 夹具随测试程序集装载进插件 ALC（类型身份落在插件上下文内，全局根扫描才判得出归属）。
/// </remarks>
public sealed class BypassUiTestPlugin : IPlugin
{
    /// <inheritdoc/>
    public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>绕过契约的 UI 入口：注册期即直并全局资源并自建窗口。</summary>
public sealed class BypassUiTestModule : IPluginUiModule
{
    /// <summary>越权并入全局资源的字典键（测试据此定位该字典）。</summary>
    public const string LeakedDictionaryKey = "bypass.template";

    /// <inheritdoc/>
    public void RegisterUi(IPluginUiContext context)
    {
        // 越权 1：不经 MergeResourceDictionary/插件资源根，直并 Application 全局资源。
        var leaked = new ResourceDictionary();
        leaked[LeakedDictionaryKey] = new DataTemplate { DataType = typeof(BypassUiMarker) };
        Application.Current!.Resources.MergedDictionaries.Add(leaked);

        // 越权 2：不经 RegisterWindow/ShowWindow，自建窗口直接显示（不经宿主登记）。
        new BypassUiTestWindow().Show();
    }
}

/// <summary>越权字典的模板目标类型：字典内容归属判定按此命中插件 ALC。</summary>
public sealed class BypassUiMarker
{
}

/// <summary>越权自建窗口：类型定义在测试程序集里，随插件 ALC 装载后窗口归属可被扫描命中。</summary>
public sealed class BypassUiTestWindow : Window
{
    /// <summary>构造越权窗口（不取焦点、不进任务栏，尽量减少对测试现场的干扰）。</summary>
    public BypassUiTestWindow()
    {
        Title = "bypass-ui-leak";
        Width = 10;
        Height = 10;
        WindowStyle = WindowStyle.None;
        ShowActivated = false;
        ShowInTaskbar = false;
    }
}
