using System.Threading;
using System.Threading.Tasks;

namespace StarPie.PluginRuntime.Ui
{
    /// <summary>
    /// 宿主侧 UI 清理端口：Host 零 WPF，UI 插件的资产清理与泄漏验证经本端口封送到 Ui 层执行。
    /// </summary>
    /// <remarks>
    /// 端口是 WPF-free 的：调用方（安全点卸载编排）在任意线程发起，实现方负责封送到 UI 线程执行
    /// "清视图 → 关窗 → 摘资源根 → 注销注册项 → 停定时器/摘动画 → 断订阅 → 泄漏验证"完整编排，
    /// 并把结果降级为纯数据返回。未注册 UI 资产的插件（headless 或未初始化 UI）返回成功且无残留。
    /// </remarks>
    public interface IPluginUiCoordinator
    {
        /// <summary>释放该插件的全部 UI 资产并验证现场。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">取消令牌；已开始的清理不因取消而中断（清理必须收口）。</param>
        /// <returns>释放结果；失败时携带可定位残留清单。</returns>
        Task<PluginUiReleaseResult> ReleaseAsync(string pluginId, CancellationToken cancellationToken);
    }
}
