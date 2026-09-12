namespace StarPie.PluginRuntime.Diagnostics
{
    /// <summary>残留类别：定位"哪一类东西还活着"。</summary>
    public enum PluginResidualKind
    {
        /// <summary>插件入口实例（插件对象本身）。</summary>
        PluginObject,

        /// <summary>可回收装载上下文（ALC）。</summary>
        LoadContext,

        /// <summary>程序集。</summary>
        Assembly,

        /// <summary>程序集内的类型。</summary>
        Type,

        /// <summary>服务作用域句柄账本残留。</summary>
        ScopeHandle,

        /// <summary>能力调用仍在途。</summary>
        InFlightCall,
    }

    /// <summary>
    /// 一条可定位的残留：类别 + 具体名称（类型全名 / 程序集全名 / 账本或调用计数）。
    /// 诊断报告只记字符串，不持有插件对象——残留对象本身绝不出现在这里。
    /// </summary>
    public sealed class PluginResidual
    {
        /// <summary>残留类别。</summary>
        public PluginResidualKind Kind { get; set; }

        /// <summary>可定位的具体描述（类型全名、程序集全名或计数）。</summary>
        public string Detail { get; set; } = string.Empty;
    }
}
