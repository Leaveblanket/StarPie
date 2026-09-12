namespace StarPie.PluginRuntime.Unloading
{
    /// <summary>一次卸载尝试的结果。</summary>
    public enum PluginUnloadStatus
    {
        /// <summary>已卸载：安全点各步完成且回收判定通过（终态 Unloaded）。</summary>
        Unloaded,

        /// <summary>隔离：任一安全点步骤失败或回收判定未通过；不得谎报成功。</summary>
        Quarantined,
    }
}
