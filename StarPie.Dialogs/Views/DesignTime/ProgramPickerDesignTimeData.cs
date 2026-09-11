using System.Collections.ObjectModel;
using StarPie.Services.Programs;
using StarPie.ViewModels.Dialogs;

namespace StarPie.Views.DesignTime;

/// <summary>
/// 程序选择器设计期样例数据（仅被 ProgramPickerWindow 根节点
/// <c>d:DataContext</c> 消费，无条件编译、惰性；运行时代码不得引用）：
/// DisplayedPrograms（6 条条目，IconSource 置空仅展示行结构）。
/// 其余状态/命令绑定按叶子“未样例清单”留空。
/// </summary>
public sealed class ProgramPickerDesignTimeData
{
    public ObservableCollection<ProgramPickerItem> DisplayedPrograms { get; } = new()
    {
        Item("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files\Google\Chrome\Application"),
        Item("Visual Studio Code", @"C:\Users\user\AppData\Local\Programs\Microsoft VS Code\Code.exe", @"C:\Users\user\AppData\Local\Programs\Microsoft VS Code"),
        Item("Windows Terminal", @"C:\Users\user\AppData\Local\Microsoft\WindowsApps\wt.exe", @"C:\Users\user\AppData\Local\Microsoft\WindowsApps"),
        Item("Notepad", @"C:\Windows\System32\notepad.exe", @"C:\Windows\System32"),
        Item("PowerShell 7", @"C:\Program Files\PowerShell\7\pwsh.exe", @"C:\Program Files\PowerShell\7"),
        Item("StarPie", @"C:\Program Files\StarPie\StarPie.exe", @"C:\Program Files\StarPie"),
    };

    private static ProgramPickerItem Item(string name, string path, string friendlyPath)
        => new(new ProgramEntry(name, path, friendlyPath), IconSource: null);
}
