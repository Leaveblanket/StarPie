using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StarPie.Kernel.Localization;

namespace StarPie.ViewModels.Wheel
{
    /// <summary>
    /// 轮盘 ViewModel：持有轮盘视图状态——选中扇区、外围逃逸状态、扇区集合与中心点——
    /// 以及样式渲染器消费的状态。手势引擎经 <see cref="IWheelViewModel"/> 驱动，
    /// 窗口观察变更通知并完成全部绘制；每次手势一个实例。
    /// </summary>
    public partial class WheelViewModel : ObservableObject, IWheelViewModel
    {
        private readonly WheelProfile _profile;

        [ObservableProperty]
        private int _selectedSectorIndex = -1;

        [ObservableProperty]
        private bool _isOuterEscaped;

        [ObservableProperty]
        private bool _isShown;

        [ObservableProperty]
        private bool _isClosed;

        public GesturePoint Center { get; }

        /// <summary>运行态配置引用：轮盘窗口读取纯样式字段（形状/字体/核图），
        /// 面向渲染器的状态以属性暴露。</summary>
        public AppConfig Config { get; }

        /// <summary>每个轮盘扇区一个槽位（按方位角索引）；未绑定动作的槽位 HasAction 为 false。</summary>
        public IReadOnlyList<WheelSectorViewModel> Sectors { get; }

        public int SectorCount => _profile.SectorCount;

        public string CoreTitle { get; }

        public string CoreSubtitle { get; }

        public string WheelPalette { get; }

        public string WheelStyle { get; }

        public double InnerRadius { get; }

        public double OuterRadius { get; }

        public double CoreRadius { get; }

        public bool ShowCoreIcon { get; }

        public WheelViewModel(GesturePoint center, WheelProfile profile, AppConfig config, ILocalizationService localization)
        {
            Center = center;
            _profile = profile;
            Config = config;

            CoreTitle = profile.ProcessName == "Global" ? localization.GetString("WheelCoreTitle") : profile.ProcessName;
            CoreSubtitle = string.Format(localization.GetString("WheelCoreSubtitle"), profile.SectorCount);

            Sectors = Enumerable.Range(0, Math.Max(0, profile.SectorCount))
                .Select(i => new WheelSectorViewModel(i, i < profile.Actions.Count ? profile.Actions[i] : null))
                .ToList();

            WheelPalette = config.WheelPalette ?? "System";
            WheelStyle = config.WheelStyle ?? "ClassicRing";
            OuterRadius = config.WheelRadius;
            CoreRadius = config.CoreRadius;
            // 安全边界：环不得退化，内半径保持小于外半径。
            InnerRadius = config.InnerRadius >= OuterRadius
                ? Math.Max(0, OuterRadius - 20)
                : config.InnerRadius;
            ShowCoreIcon = config.ShowCoreIcon;
        }

        public void Show() => IsShown = true;

        public void HighlightSector(int sectorIndex)
        {
            if (SelectedSectorIndex == sectorIndex)
            {
                // 引擎在每次拖拽移动时都调用本方法；即使索引重复也重新应用选中
                // （含中心取消反馈），而不是静默跳过 no-op 移动。
                OnPropertyChanged(nameof(SelectedSectorIndex));
                return;
            }

            SelectedSectorIndex = sectorIndex;
        }

        public void SetOuterEscapeState(bool isEscaped) => IsOuterEscaped = isEscaped;

        public void Close() => IsClosed = true;
    }

    /// <summary>单个扇区槽位的只读状态，由绑定的动作（或缺失动作）构建。</summary>
    public sealed class WheelSectorViewModel
    {
        public int Index { get; }

        /// <summary>槽位无绑定动作为 false；视图据此显示"未设置"占位。</summary>
        public bool HasAction { get; }

        public string Name { get; }

        public string Type { get; }

        public string Parameter { get; }

        public string IconKey { get; }

        public string CustomIconSvg { get; }

        public WheelSectorViewModel(int index, ActionItem? action)
        {
            Index = index;
            HasAction = action != null;
            Name = action?.Name ?? "";
            Type = action?.Type ?? "Hotkey";
            Parameter = action?.Parameter ?? "";
            IconKey = action?.IconKey ?? "";
            CustomIconSvg = action?.CustomIconSvg ?? "";
        }
    }
}
