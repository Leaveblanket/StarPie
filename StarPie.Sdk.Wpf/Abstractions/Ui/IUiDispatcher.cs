using System;
using System.Threading.Tasks;

namespace StarPie.Abstractions.Ui
{
    /// <summary>
    /// 宿主 UI 线程端口：插件的后台线程只经本端口触碰 WPF 对象，不直接调用 Dispatcher。
    /// </summary>
    /// <remarks>
    /// 端口按插件作用域提供：宿主在中介里捕获插件自身行为，插件停用后投递的调用被拒绝而不是落到
    /// 已释放的宿主资源上。
    /// </remarks>
    public interface IUiDispatcher
    {
        /// <summary>当前线程是否是宿主 UI 线程。</summary>
        bool IsOnUiThread { get; }

        /// <summary>在 UI 线程执行动作；已在 UI 线程时同步执行，否则排队等待。</summary>
        /// <param name="action">要执行的动作（非 null）。</param>
        /// <returns>动作执行完成后完成的任务。</returns>
        Task InvokeAsync(Action action);

        /// <summary>在 UI 线程执行返回值工厂。</summary>
        /// <typeparam name="T">返回类型。</typeparam>
        /// <param name="callback">要执行的工厂（非 null）。</param>
        /// <returns>携带工厂结果的已排队任务。</returns>
        Task<T> InvokeAsync<T>(Func<T> callback);
    }
}
