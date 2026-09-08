namespace StarPie.Services.Icons
{
    /// <summary>
    /// Windows 快捷方式（.lnk）目标解析契约：由程序模块（StarPie.Programs）实现，
    /// 供共享内核「图标资产」实例服务在提取 .lnk 图标时消费——共享内核不反向依赖业务模块。
    /// </summary>
    public interface IShortcutTargetResolver
    {
        /// <summary>解析快捷方式真实目标路径与图标位置；快捷方式不存在或解析失败返回 false
        /// （目标与图标均可能为空）。</summary>
        bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex);
    }
}
