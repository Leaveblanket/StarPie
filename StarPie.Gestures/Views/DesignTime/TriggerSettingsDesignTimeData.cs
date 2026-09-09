using System.Collections.ObjectModel;

namespace StarPie.Views.DesignTime;

/// <summary>
/// 触发与场景页设计期样例数据（ADR-0025/#101，仅被 TriggerSettingsPage 根节点
/// <c>d:DataContext</c> 消费，无条件编译、惰性；运行时代码不得引用）：
/// Blacklist（4 条进程名）。其余单值/命令绑定按叶子“未样例清单”留空。
/// </summary>
public sealed class TriggerSettingsDesignTimeData
{
    public ObservableCollection<string> BlacklistProcesses { get; } = new()
    {
        "chrome.exe",
        "photoshop.exe",
        "obs64.exe",
        "mspaint.exe",
    };
}
