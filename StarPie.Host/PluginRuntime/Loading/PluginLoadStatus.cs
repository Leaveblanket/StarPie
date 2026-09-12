namespace StarPie.PluginRuntime.Loading
{
    /// <summary>装载尝试的结果三态。</summary>
    public enum PluginLoadStatus
    {
        /// <summary>已装载并启动完成：生命周期处于活动态，入口实例可用。</summary>
        Active,

        /// <summary>被拒绝：准入为拒绝或装载前重校验不通过，未创建 ALC。</summary>
        Rejected,

        /// <summary>新版本已就位但本进程不能生效（界面插件）：旧版本已停用，重启后装载新版本。</summary>
        PendingRestart,

        /// <summary>装载或启动失败并已隔离：原因在结果中，宿主启动不受影响。</summary>
        Quarantined,
    }
}
