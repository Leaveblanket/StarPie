namespace StarPie.Services.Icons
{
    /// <summary>
    /// Windows 快捷方式（.lnk）目标解析契约：实现驻宿主内核（<c>StarPie.Host</c> 的程序扫描件），
    /// 供程序扫描与图标资产实例服务在解析 .lnk 目标/图标时经契约边消费。
    /// </summary>
    public interface IShortcutTargetResolver
    {
        /// <summary>解析快捷方式真实目标路径与图标位置；快捷方式不存在或解析失败返回 false
        /// （目标与图标均可能为空）。</summary>
        bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex);
    }
}
