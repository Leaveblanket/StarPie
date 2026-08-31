using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinPieGestures.ViewModels
{
    /// <summary>
    /// 设置窗口·配置方案分区列表侧 ViewModel (T11/T12, ADR-0001)：承接迁移前 SettingsWindow
    /// code-behind 的方案列表与选中态（<c>_selectedProfile</c> 字段）、扇区数切换与
    /// 方向槽位集合（<c>_slotViewModels</c> + <c>RefreshSlots</c>）。
    /// T12 起槽位持有对话框服务（<see cref="IDialogService"/>，动作编辑闭环的对话框编排全部
    /// 进槽位 ViewModel），本 VM 将槽位编辑提交事件转发为 <see cref="SlotEditCommitted"/> 供
    /// 窗口落盘。与 <see cref="WheelViewModel.Config"/> 先例同理，直接持有运行态配置的
    /// Profiles 列表引用（live-apply：改动即时写入运行态模型并生效）。
    /// </summary>
    public partial class ProfileListViewModel : ObservableObject
    {
        // 方位角标签与缺省动作（自 SettingsWindow 迁入，文案与补齐规则一字未动）
        private static readonly string[] Directions4 = { "右 (E / 0°)", "下 (S / 90°)", "左 (W / 180°)", "上 (N / 270°)" };
        private static readonly string[] Directions8 = { "右 (E / 0°)", "右下 (SE / 45°)", "下 (S / 90°)", "左下 (SW / 135°)", "左 (W / 180°)", "左上 (NW / 225°)", "上 (N / 270°)", "右上 (NE / 315°)" };
        private static readonly string[] Directions12 = {
            "右 3点钟 (E / 0°)", "右下 4点钟 (30°)", "右下 5点钟 (60°)", "下 6点钟 (S / 90°)",
            "左下 7点钟 (120°)", "左下 8点钟 (150°)", "左 9点钟 (W / 180°)", "左上 10点钟 (210°)",
            "左上 11点钟 (240°)", "上 12点钟 (N / 270°)", "右上 1点钟 (300°)", "右上 2点钟 (330°)"
        };

        private static readonly ActionItem[] DefaultPresets4 = new[]
        {
            new ActionItem { Type = "Hotkey", Name = "复制 (Copy)", Parameter = "Ctrl+C", IconKey = "Copy" },
            new ActionItem { Type = "System", Name = "显示桌面 (Desktop)", Parameter = "ShowDesktop", IconKey = "ShowDesktop" },
            new ActionItem { Type = "Hotkey", Name = "粘贴 (Paste)", Parameter = "Ctrl+V", IconKey = "Paste" },
            new ActionItem { Type = "System", Name = "关闭窗口 (Close)", Parameter = "CloseWindow", IconKey = "CloseWindow" }
        };

        private static readonly ActionItem[] DefaultPresets12 = new[]
        {
            new ActionItem { Type = "Hotkey", Name = "复制 (Copy)", Parameter = "Ctrl+C", IconKey = "Copy" },
            new ActionItem { Type = "Hotkey", Name = "剪切 (Cut)", Parameter = "Ctrl+X", IconKey = "Cut" },
            new ActionItem { Type = "System", Name = "锁定电脑 (Lock)", Parameter = "Lock", IconKey = "Lock" },
            new ActionItem { Type = "System", Name = "显示桌面 (Desktop)", Parameter = "ShowDesktop", IconKey = "ShowDesktop" },
            new ActionItem { Type = "System", Name = "任务视图 (TaskView)", Parameter = "TaskView", IconKey = "TaskView" },
            new ActionItem { Type = "System", Name = "屏幕截图 (Screenshot)", Parameter = "Screenshot", IconKey = "Screenshot" },
            new ActionItem { Type = "Hotkey", Name = "粘贴 (Paste)", Parameter = "Ctrl+V", IconKey = "Paste" },
            new ActionItem { Type = "Hotkey", Name = "撤销 (Undo)", Parameter = "Ctrl+Z", IconKey = "Undo" },
            new ActionItem { Type = "System", Name = "音量减小 (Vol-)", Parameter = "VolumeDown", IconKey = "VolumeDown" },
            new ActionItem { Type = "System", Name = "关闭窗口 (Close)", Parameter = "CloseWindow", IconKey = "CloseWindow" },
            new ActionItem { Type = "System", Name = "音量增加 (Vol+)", Parameter = "VolumeUp", IconKey = "VolumeUp" },
            new ActionItem { Type = "System", Name = "任务管理器 (TaskMgr)", Parameter = "TaskManager", IconKey = "TaskManager" }
        };

        private List<WheelProfile> _sourceProfiles;
        private readonly IDialogService _dialogs;

        /// <summary>方案展示列表（按前台进程名展示每个方案，Global 为全局兜底方案）。</summary>
        public ObservableCollection<ProfileItemViewModel> Profiles { get; } = new();

        /// <summary>选中方案的方向槽位集合：选中方案或扇区数变更时整体重建。</summary>
        public ObservableCollection<SlotViewModel> Slots { get; } = new();

        /// <summary>任一槽位动作编辑提交（文件夹选择写回）后触发，窗口据此落盘。</summary>
        public event Action? SlotEditCommitted;

        [ObservableProperty]
        private ProfileItemViewModel? _selectedProfile;

        /// <summary>选中方案的扇区数（原始值不做规范化——与迁移前单选钮同步逻辑一致）。</summary>
        public int? SelectedSectorCount => SelectedProfile?.Model.SectorCount;

        public ProfileListViewModel(List<WheelProfile> sourceProfiles, IDialogService dialogs)
        {
            _sourceProfiles = sourceProfiles ?? throw new ArgumentNullException(nameof(sourceProfiles));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            Reload(sourceProfiles);
        }

        /// <summary>以新的运行态方案列表重建展示集合（导入配置后调用）；清空选中态与槽位。</summary>
        public void Reload(List<WheelProfile> sourceProfiles)
        {
            _sourceProfiles = sourceProfiles ?? throw new ArgumentNullException(nameof(sourceProfiles));
            SelectedProfile = null;
            Profiles.Clear();
            foreach (var profile in sourceProfiles)
            {
                Profiles.Add(new ProfileItemViewModel(profile));
            }
            Slots.Clear();
        }

        /// <summary>
        /// 选中指定方案并重建槽位（对应迁移前 ProfilesListBox_SelectionChanged 的列表侧效果）。
        /// item 为 null（如清空选择）时返回 false 不作处理——与迁移前"选中空即返回"一致。
        /// </summary>
        public bool SelectProfile(ProfileItemViewModel? item)
        {
            if (item == null) return false;
            SelectedProfile = item;
            RebuildSlots();
            return true;
        }

        /// <summary>
        /// 将扇区数应用到选中方案并重建槽位（对应迁移前 SectorCountRadio_Checked 的列表侧效果）。
        /// 未选中时兜底取第一个方案但不改列表可视选中——与迁移前的字段兜底一致。无任何方案时返回 false。
        /// </summary>
        public bool ApplySectorCount(int sectorCount)
        {
            var item = SelectedProfile ?? Profiles.FirstOrDefault();
            if (item == null) return false;
            SelectedProfile = item;
            item.Model.SectorCount = sectorCount;
            RebuildSlots();
            return true;
        }

        /// <summary>
        /// 重建方向槽位集合（迁移前 RefreshSlots）：扇区数规范化为 4/8/12（非法值按 8 展示，
        /// 不回写模型）、按缺省预设补齐缺失动作、按方位角生成槽位 ViewModel。
        /// </summary>
        public void RebuildSlots()
        {
            try
            {
                Slots.Clear();

                var item = SelectedProfile ?? Profiles.FirstOrDefault();
                if (item == null) return;
                SelectedProfile = item;
                var profile = item.Model;

                int count = profile.SectorCount;
                if (count != 4 && count != 8 && count != 12) count = 8;

                string[] directions = count switch
                {
                    4 => Directions4,
                    12 => Directions12,
                    _ => Directions8
                };

                if (profile.Actions == null)
                {
                    profile.Actions = new List<ActionItem>();
                }

                while (profile.Actions.Count < count)
                {
                    int idx = profile.Actions.Count;
                    if (count == 12 && idx < DefaultPresets12.Length)
                    {
                        var p = DefaultPresets12[idx];
                        profile.Actions.Add(new ActionItem { Type = p.Type, Name = p.Name, Parameter = p.Parameter, IconKey = p.IconKey });
                    }
                    else if (count == 4 && idx < DefaultPresets4.Length)
                    {
                        var p = DefaultPresets4[idx];
                        profile.Actions.Add(new ActionItem { Type = p.Type, Name = p.Name, Parameter = p.Parameter, IconKey = p.IconKey });
                    }
                    else
                    {
                        profile.Actions.Add(new ActionItem { Type = "Hotkey", Name = $"快捷动作 {idx + 1}", Parameter = "" });
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    var slot = new SlotViewModel(directions[i], profile.Actions[i], _dialogs);
                    slot.EditApplied += () => SlotEditCommitted?.Invoke();
                    Slots.Add(slot);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RebuildSlots Error]: {ex}");
            }
        }

        /// <summary>新方案写入运行态配置并进入展示列表（迁移前 AddProfileButton_Click 的列表侧效果）；返回其包装项。</summary>
        public ProfileItemViewModel AddProfile(WheelProfile profile)
        {
            var item = new ProfileItemViewModel(profile);
            _sourceProfiles.Add(profile);
            Profiles.Add(item);
            return item;
        }

        /// <summary>从运行态配置与展示列表中移除方案（迁移前 DeleteProfileButton_Click 的列表侧效果）。
        /// 不主动清选中态——与迁移前一致，由列表选择事件回落。</summary>
        public void RemoveProfile(ProfileItemViewModel item)
        {
            _sourceProfiles.Remove(item.Model);
            Profiles.Remove(item);
        }

        /// <summary>方案名（进程名）占用查重，大小写不敏感（迁移前新增/重命名对话框校验语义；
        /// T16 自窗口 code-behind 对运行态配置的直接查询收编）。</summary>
        public bool IsProcessNameTaken(string processName)
            => Profiles.Any(p => p.Model.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

        /// <summary>新自定义方案的缺省名（迁移前 AddCustomProfileButton_Click 的默认文案）。</summary>
        public string CreateDefaultCustomProfileName() => $"自定义配置_{Profiles.Count}";
    }

    /// <summary>
    /// 配置方案列表条目 ViewModel：包装 <see cref="WheelProfile"/> 模型提供列表展示。
    /// 模型是纯 POCO 无变更通知，模型属性被直接修改后调用 <see cref="RefreshDisplay"/>
    /// 触发展示刷新（对应迁移前 ProfilesListBox.Items.Refresh()）。
    /// </summary>
    public sealed class ProfileItemViewModel : ObservableObject
    {
        public WheelProfile Model { get; }

        /// <summary>展示名：方案绑定的前台进程名（或 Global / 自定义名称）。</summary>
        public string ProcessName => Model.ProcessName;

        public ProfileItemViewModel(WheelProfile model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RefreshDisplay() => OnPropertyChanged(nameof(ProcessName));

        // 列表项可见文案沿用迁移前 WheelProfile.ToString() 的进程名；e2e 依赖该文案。
        public override string ToString() => Model.ProcessName;
    }
}
