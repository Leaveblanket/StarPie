namespace StarPie.PluginRuntime.Unloading
{
    /// <summary>
    /// 卸载回收判定策略：判据随宿主环境分级，而不是随插件形态分级。
    /// </summary>
    /// <remarks>
    /// 硬判适用于纯 headless 宿主：ALC 与入口程序集必须被回收，存活即隔离。
    /// 降级适用于 WPF 宿主：宿主框架（System.Xaml/WPF 的 BAML 架构上下文与程序集缓存）在
    /// <c>AppDomain.AssemblyLoad</c> 处收拢全部已加载程序集并强引用，插件程序集因此必然留在进程内；
    /// 此时只硬判插件自有对象可回收，ALC 与程序集存活记诊断，不判隔离。
    /// </remarks>
    public enum PluginReclaimPolicy
    {
        /// <summary>ALC 与入口程序集必须回收，任一存活即隔离（纯 headless 宿主）。</summary>
        Hard,

        /// <summary>ALC 与入口程序集存活只记诊断（WPF 宿主：宿主框架缓存程序集，重启后释放）。</summary>
        Diagnostic,
    }
}
