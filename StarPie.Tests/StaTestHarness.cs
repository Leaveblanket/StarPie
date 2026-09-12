using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace StarPie.Tests;

/// <summary>
/// STA 测试 harness：进程内唯一的 STA 线程 + <see cref="Application"/> + 消息循环。
/// </summary>
/// <remarks>
/// 插件 UI 托管依赖真实 WPF 全局根（<c>Application.Current</c> 的资源与窗口集合），
/// 单元测试必须跑在 STA 且有消息循环的线程上；WPF 每进程只允许一个 Application 实例，
/// 故本 harness 惰性创建一次并在全部用例间共享（测试进程内串行使用）。
/// </remarks>
internal static class StaTestHarness
{
    private static readonly Lazy<Harness> Shared = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>共享的 WPF 应用实例（进程内唯一）。</summary>
    public static Application Application => Shared.Value.Application;

    /// <summary>共享 UI 线程的调度器。</summary>
    public static Dispatcher Dispatcher => Shared.Value.Dispatcher;

    /// <summary>在 UI 线程上执行异步测试体并回传结果与异常。</summary>
    /// <param name="body">测试体（非 null）。</param>
    public static Task RunAsync(Func<Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Shared.Value.Dispatcher.InvokeAsync(body).Task.Unwrap();
    }

    /// <summary>在 UI 线程上执行同步测试体并回传结果。</summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="body">测试体（非 null）。</param>
    public static T Run<T>(Func<T> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Shared.Value.Dispatcher.Invoke(body);
    }

    /// <summary>在 UI 线程上执行同步测试体。</summary>
    /// <param name="body">测试体（非 null）。</param>
    public static void Run(Action body)
    {
        ArgumentNullException.ThrowIfNull(body);
        Shared.Value.Dispatcher.Invoke(body);
    }

    private static Harness Create()
    {
        var ready = new TaskCompletionSource<Harness>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            // 窗口关闭不结束应用：用例会创建/关闭窗口，ShutdownMode 保持显式关闭。
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ready.SetResult(new Harness(application, Dispatcher.CurrentDispatcher));
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "starpie-sta-test-harness",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }

    private sealed record Harness(Application Application, Dispatcher Dispatcher);
}
