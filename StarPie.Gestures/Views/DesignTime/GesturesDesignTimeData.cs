using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace StarPie.Views.DesignTime;

/// <summary>
/// 手势与动作页设计期样例数据（ADR-0025/#101，仅被 GesturesSettingsPage 根节点
/// <c>d:DataContext</c> 消费，无条件编译、惰性；运行时代码不得引用）：
/// Profiles（5 项）与 Slots（8 行，方向标签按扇区表）。其余单值/命令绑定按叶子
/// “未样例清单”留空。
/// </summary>
public sealed class GesturesSettingsDesignTimeData
{
    public ObservableCollection<GestureProfileDesignTimeItem> Profiles { get; } = new()
    {
        new() { ProcessName = "Global" },
        new() { ProcessName = "chrome.exe" },
        new() { ProcessName = "code.exe" },
        new() { ProcessName = "photoshop.exe" },
        new() { ProcessName = "notepad.exe" },
    };

    public GestureProfileDesignTimeItem? SelectedProfile { get; set; }

    public ObservableCollection<GestureSlotDesignTimeItem> Slots { get; } = new();

    public GesturesSettingsDesignTimeData()
    {
        SelectedProfile = Profiles[1];

        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "右 (E / 0°)", Name = "复制 (Copy)", Type = "Hotkey", Parameter = "Ctrl+C", IconDisplayText = "Copy" });
        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "右下 (SE / 45°)", Name = "显示桌面 (Desktop)", Type = "System", Parameter = "ShowDesktop", SelectedSystemPreset = "ShowDesktop", IconDisplayText = "ShowDesktop" });
        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "下 (S / 90°)", Name = "启动 Chrome", Type = "Launch", Parameter = @"C:\Program Files\Google\Chrome\Application\chrome.exe", IconDisplayText = "Launch" });
        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "左下 (SW / 135°)", Name = "打开工程目录", Type = "Folder", Parameter = @"D:\Project", IconDisplayText = "Folder" });
        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "左 (W / 180°)", Name = "粘贴 (Paste)", Type = "Hotkey", Parameter = "Ctrl+V", IconDisplayText = "Paste" });
        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "左上 (NW / 225°)", Name = "音量加 (Vol+)", Type = "System", Parameter = "VolumeUp", SelectedSystemPreset = "VolumeUp", IconDisplayText = "VolumeUp" });
        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "上 (N / 270°)", Name = "关闭窗口 (Close)", Type = "System", Parameter = "CloseWindow", SelectedSystemPreset = "CloseWindow", IconDisplayText = "CloseWindow" });
        Slots.Add(new GestureSlotDesignTimeItem { DirectionLabel = "右上 (NE / 315°)", Name = "屏幕截图 (Screenshot)", Type = "System", Parameter = "Screenshot", SelectedSystemPreset = "Screenshot", IconDisplayText = "Screenshot" });
    }
}

/// <summary>配置方案设计期样例条目（展示 ProcessName，对齐运行时 ProfileItemViewModel）。</summary>
public sealed class GestureProfileDesignTimeItem
{
    public string ProcessName { get; set; } = "";
}

/// <summary>方向槽位设计期样例条目（绑定面对齐运行时 SlotViewModel：Type/方向/名称/图标/参数）。</summary>
public sealed class GestureSlotDesignTimeItem
{
    public string DirectionLabel { get; set; } = "";

    public string Name { get; set; } = "";

    public string Type { get; set; } = "Hotkey";

    public string Parameter { get; set; } = "";

    public string Arguments { get; set; } = "";

    public string IconDisplayText { get; set; } = "图标...";

    public bool HasVectorIcon { get; set; }

    public string? VectorIconPathData { get; set; }

    public string SelectedSystemPreset { get; set; } = "Lock";

    public string TestButtonText { get; set; } = "测试";

    public bool IsHotkeyType => Type == "Hotkey";

    public bool IsLaunchType => Type == "Launch";

    public bool IsFolderType => Type is "Folder" or "OpenFolder";

    public bool IsSystemType => Type == "System";

    public IReadOnlyList<GestureActionTypeDesignTimeOption> ActionTypes { get; } = new[]
    {
        new GestureActionTypeDesignTimeOption { Tag = "Hotkey", DisplayText = "快捷热键" },
        new GestureActionTypeDesignTimeOption { Tag = "Launch", DisplayText = "启动程序" },
        new GestureActionTypeDesignTimeOption { Tag = "Folder", DisplayText = "打开文件夹" },
        new GestureActionTypeDesignTimeOption { Tag = "System", DisplayText = "系统控制" },
    };
}

/// <summary>动作类型下拉设计期样例选项（Tag/DisplayText 对齐运行时 ActionTypeOption）。</summary>
public sealed class GestureActionTypeDesignTimeOption
{
    public string Tag { get; set; } = "";

    public string DisplayText { get; set; } = "";
}
