namespace StarPie.PluginRuntime.Lifecycle
{
    /// <summary>
    /// 插件实例的生命周期状态：从发现到活动的装载链、从停止到已卸载的卸载链，
    /// 以及失败后的隔离终态。
    /// </summary>
    public enum PluginLifecycleState
    {
        /// <summary>已发现：清单与包内容尚未校验；被拒绝的包停在此态，不进入装载。</summary>
        Discovered,

        /// <summary>已校验：清单与包内容通过校验，等待装载。</summary>
        Validated,

        /// <summary>装载中：正在创建 ALC 并载入入口程序集。</summary>
        Loading,

        /// <summary>启动中：入口实例已创建，启动方法执行中。</summary>
        Starting,

        /// <summary>活动：启动完成，允许执行插件代码。</summary>
        Active,

        /// <summary>停止中：拒绝新调用并在途调用排空。</summary>
        Stopping,

        /// <summary>UI 释放中：仅 UI 插件进入，在 UI 线程清理资产并验证无泄漏。</summary>
        ReleasingUi,

        /// <summary>卸载中：宿主侧清理完成，等待 ALC 卸载与回收判定。</summary>
        Unloading,

        /// <summary>已卸载：卸载流程完成（终态）。</summary>
        Unloaded,

        /// <summary>已隔离：装载/启动/调用/卸载失败后的终态；重启不自动重试装载。</summary>
        Quarantined,
    }
}
