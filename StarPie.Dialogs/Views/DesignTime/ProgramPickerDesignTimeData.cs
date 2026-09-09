using System.Collections.ObjectModel;
using StarPie.Services.Programs;

namespace StarPie.Views.DesignTime;

/// <summary>
/// 程序选择器设计期样例数据（ADR-0025/#101，仅被 ProgramPickerWindow 根节点
/// <c>d:DataContext</c> 消费，无条件编译、惰性；运行时代码不得引用）：
/// DisplayedPrograms（6 条 ProgramEntry，IconSource 置空仅展示行结构）。
/// 其余状态/命令绑定按叶子“未样例清单”留空。
/// </summary>
public sealed class ProgramPickerDesignTimeData
{
    public ObservableCollection<ProgramEntry> DisplayedPrograms { get; } = new()
    {
        new("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files\Google\Chrome\Application", null),
        new("Visual Studio Code", @"C:\Users\user\AppData\Local\Programs\Microsoft VS Code\Code.exe", @"C:\Users\user\AppData\Local\Programs\Microsoft VS Code", null),
        new("Windows Terminal", @"C:\Users\user\AppData\Local\Microsoft\WindowsApps\wt.exe", @"C:\Users\user\AppData\Local\Microsoft\WindowsApps", null),
        new("Notepad", @"C:\Windows\System32\notepad.exe", @"C:\Windows\System32", null),
        new("PowerShell 7", @"C:\Program Files\PowerShell\7\pwsh.exe", @"C:\Program Files\PowerShell\7", null),
        new("StarPie", @"C:\Program Files\StarPie\StarPie.exe", @"C:\Program Files\StarPie", null),
    };
}
