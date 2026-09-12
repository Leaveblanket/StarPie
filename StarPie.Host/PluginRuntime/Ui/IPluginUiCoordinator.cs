using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace StarPie.PluginRuntime.Ui
{
    /// <summary>
    /// 宿主侧 UI 托管端口：Host 零 WPF，UI 插件的入口调用、资产清理与泄漏验证经本端口封送到 Ui 层执行。
    /// </summary>
    /// <remarks>
    /// 端口是 WPF-free 的：调用方（装载管线与安全点卸载编排）在任意线程发起，实现方负责封送到
    /// UI 线程执行，并把结果降级为纯数据返回。装载期调 <see cref="AttachAsync"/> 完成
    /// <c>IPluginUiModule.RegisterUi</c>（只在 UI 线程注册，不创建窗口/资源）；卸载期调
    /// <see cref="ReleaseAsync"/> 执行"清视图 → 关窗 → 摘资源根 → 注销注册项 → 停定时器/摘动画 →
    /// 断订阅 → 泄漏验证"完整编排。未注册 UI 资产的插件（headless 或未初始化 UI）释放返回成功且无残留。
    /// </remarks>
    public interface IPluginUiCoordinator
    {
        /// <summary>
        /// 注册该插件的 UI 资产：校验 UI 侧 ABI 后解析清单 <c>ui.entryType</c> 并在 UI 线程调用一次
        /// <c>IPluginUiModule.RegisterUi</c>，注册产物随资源根并入宿主资源树。
        /// </summary>
        /// <param name="request">装载结果交接的 UI 装载请求（插件 id、入口程序集、ui 段声明）。</param>
        /// <param name="cancellationToken">取消令牌；已开始的注册不因取消而中断。</param>
        /// <returns>注册结果；失败时为可读原因，调用方据此判定装载失败并进入隔离。</returns>
        Task<PluginUiAttachResult> AttachAsync(
            PluginUiAttachRequest request,
            CancellationToken cancellationToken);

        /// <summary>释放该插件的全部 UI 资产并验证现场。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">取消令牌；已开始的清理不因取消而中断（清理必须收口）。</param>
        /// <returns>释放结果；失败时携带可定位残留清单。</returns>
        Task<PluginUiReleaseResult> ReleaseAsync(string pluginId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 一次 UI 装载请求：插件 id、已装载的入口程序集与清单 ui 段声明。
    /// </summary>
    /// <remarks>
    /// 入口程序集由装载管线在插件的可回收上下文中加载，UI 侧据此解析 <c>ui.entryType</c>；程序集本体
    /// 不经本请求的所有权转移——装载结果与卸载请求仍是插件对象所有权的唯一路径。
    /// </remarks>
    /// <param name="PluginId">插件 id。</param>
    /// <param name="EntryAssembly">已载入的入口程序集。</param>
    /// <param name="UiSdk">清单 <c>ui.sdk</c> 声明（"主.次"；兼容判定归 UI 侧）。</param>
    /// <param name="UiEntryType">清单 <c>ui.entryType</c> 声明的 UI 入口类型全名。</param>
    public sealed record PluginUiAttachRequest(
        string PluginId,
        Assembly EntryAssembly,
        string? UiSdk,
        string UiEntryType);

    /// <summary>UI 装载结果：是否注册完成 + 可读失败原因（装载隔离诊断的事实来源）。</summary>
    /// <param name="Succeeded">UI 入口已解析并在 UI 线程注册完成。</param>
    /// <param name="FailureReason">失败原因（ABI 不兼容 / 入口类型非法 / 注册抛异常）；成功时为 null。</param>
    public sealed record PluginUiAttachResult(bool Succeeded, string? FailureReason)
    {
        /// <summary>注册完成的成功结果。</summary>
        public static PluginUiAttachResult Success { get; } = new(true, null);

        /// <summary>带原因的失败结果。</summary>
        /// <param name="reason">可读失败原因（非空）。</param>
        public static PluginUiAttachResult Failed(string reason) => new(false, reason);
    }
}
