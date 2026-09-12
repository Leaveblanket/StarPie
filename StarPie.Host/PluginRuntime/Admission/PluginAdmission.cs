namespace StarPie.PluginRuntime.Admission
{
    /// <summary>插件准入结果四态：插件被允许装载的依据，或拒绝。</summary>
    public enum PluginAdmission
    {
        /// <summary>内置：随宿主分发并登记在宿主内置清单的第一方插件。</summary>
        BuiltIn,

        /// <summary>已审核：命中审核清单（版本级撤销随清单判定）。</summary>
        Reviewed,

        /// <summary>开发者模式：开发者模式开启时放行的未审核插件。</summary>
        DeveloperMode,

        /// <summary>拒绝：未命中审核清单且开发者模式未开启，或校验/冲突失败（附原因）。</summary>
        Rejected,
    }
}
