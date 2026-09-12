namespace StarPie.PluginHosting
{
    /// <summary>UI 资产类别：决定清理编排的执行顺序与诊断文案。</summary>
    /// <remarks>
    /// 顺序即安全点卸载的固定顺序：视图 → 窗口 → 资源根 → 注册项（页面/设置区/菜单/命令）→
    /// 定时器 → 动画 → 订阅；<see cref="PluginUiAssetRegistry"/> 按本顺序执行摘除。
    /// </remarks>
    public enum PluginUiAssetKind
    {
        /// <summary>插件视图与宿主容器（先清容器 Content，再断绑定）。</summary>
        View = 0,

        /// <summary>插件窗口（关闭并等待 Closed，清 Owner/DataContext）。</summary>
        Window = 1,

        /// <summary>插件资源根（从宿主资源合并表整体摘除）。</summary>
        ResourceRoot = 2,

        /// <summary>导航页注册项（纯数据）。</summary>
        Page = 3,

        /// <summary>设置页区块注册项（纯数据）。</summary>
        SettingsSection = 4,

        /// <summary>菜单项注册项（纯数据）。</summary>
        MenuItem = 5,

        /// <summary>命令注册项。</summary>
        Command = 6,

        /// <summary>宿主签发的 DispatcherTimer。</summary>
        Timer = 7,

        /// <summary>宿主中介的动画（摘除必须是 Remove，不是 Stop）。</summary>
        Animation = 8,

        /// <summary>宿主中介事件订阅。</summary>
        Subscription = 9,
    }
}
