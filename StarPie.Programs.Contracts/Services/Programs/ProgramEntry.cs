using System.Windows.Media;

namespace StarPie.Services.Programs
{
    /// <summary>程序选择器的一条候选程序（不可变记录）：扫描候选阶段 <c>IconSource</c>
    /// 为 null，去重定名后再补图标。</summary>
    public sealed record ProgramEntry(string Name, string Path, string FriendlyPath, ImageSource? IconSource);
}
