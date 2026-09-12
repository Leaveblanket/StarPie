namespace StarPie.Abstractions
{
    /// <summary>
    /// 插件入口契约：宿主装载入口类型后调用其生命周期方法。
    /// </summary>
    /// <remarks>
    /// 入口类型必须实现本接口且有公开无参构造函数；<see cref="StartAsync"/> 抛出异常即判定
    /// 装载失败，插件进入隔离而不影响宿主启动。实例在插件自己的 ALC 内执行，与宿主只经
    /// 本契约与 SDK 类型交互。
    /// </remarks>
    public interface IPlugin
    {
        /// <summary>启动插件：能力注册、订阅等准备动作在此完成，正常返回后插件进入活动态。</summary>
        /// <param name="context">宿主交给本插件的上下文（插件全部宿主可达面）。</param>
        /// <param name="cancellationToken">宿主取消装载的令牌。</param>
        Task StartAsync(IPluginContext context, CancellationToken cancellationToken);

        /// <summary>停止插件：宿主在安全点调用，插件应释放自身持有的资源。</summary>
        /// <param name="cancellationToken">宿主取消停止流程的令牌。</param>
        Task StopAsync(CancellationToken cancellationToken);
    }
}
