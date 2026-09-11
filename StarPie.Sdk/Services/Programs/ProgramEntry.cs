namespace StarPie.Services.Programs
{
    /// <summary>程序选择器的一条候选程序（不可变记录）：纯数据（零 WPF），
    /// 图标由 UI 消费方按 <see cref="Path"/> 经图标资产服务装配。</summary>
    public sealed record ProgramEntry(string Name, string Path, string FriendlyPath);
}
