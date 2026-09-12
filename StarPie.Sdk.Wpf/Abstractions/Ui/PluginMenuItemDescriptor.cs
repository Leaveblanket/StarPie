namespace StarPie.Abstractions.Ui
{
    /// <summary>
    /// 菜单项描述符：纯数据，宿主在对应菜单入口渲染并把点击路由到命令 id。
    /// </summary>
    /// <param name="ItemKey">菜单项稳定键（插件内唯一）。</param>
    /// <param name="TitleKey">菜单项标题的文案键。</param>
    /// <param name="CommandId">点击时触发的命令 id（须已注册）。</param>
    /// <param name="IconData">可选图标数据（几何路径串）。</param>
    public sealed record PluginMenuItemDescriptor(
        string ItemKey,
        string TitleKey,
        string CommandId,
        string? IconData = null);
}
