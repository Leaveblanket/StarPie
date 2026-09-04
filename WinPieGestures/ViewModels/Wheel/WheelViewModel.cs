using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WinPieGestures.Services.Localization;

namespace WinPieGestures.ViewModels.Wheel
{
    /// <summary>
    /// 轮盘 ViewModel (T05, ADR-0001): owns the wheel's view state — selected sector,
    /// outer-escape state, the sector collection and the center point — plus the
    /// state the style renderers consume. The gesture engine drives it through
    /// <see cref="IWheelViewModel"/>; the window observes the change notifications
    /// and performs all drawing. One instance lives per gesture.
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

        /// <summary>Live config reference: the radial window reads pure-styling
        /// fields (shape, fonts, core imagery) off it; renderer-facing state is
        /// exposed as properties.</summary>
        public AppConfig Config { get; }

        /// <summary>One slot per 轮盘 sector, indexed by azimuth; slots without a bound action report <c>HasAction == false</c>.</summary>
        public IReadOnlyList<WheelSectorViewModel> Sectors { get; }

        public int SectorCount => _profile.SectorCount;

        public string CoreTitle { get; }

        public string CoreSubtitle { get; }

        public string Theme { get; }

        public string UiStyle { get; }

        public double InnerRadius { get; }

        public double OuterRadius { get; }

        public double CoreRadius { get; }

        public bool ShowCoreIcon { get; }

        public WheelViewModel(GesturePoint center, WheelProfile profile, AppConfig config)
        {
            Center = center;
            _profile = profile;
            Config = config;

            CoreTitle = profile.ProcessName == "Global" ? I18n.T("WheelCoreTitle") : profile.ProcessName;
            CoreSubtitle = string.Format(I18n.T("WheelCoreSubtitle"), profile.SectorCount);

            Sectors = Enumerable.Range(0, Math.Max(0, profile.SectorCount))
                .Select(i => new WheelSectorViewModel(i, i < profile.Actions.Count ? profile.Actions[i] : null))
                .ToList();

            Theme = config.Theme ?? "System";
            UiStyle = config.UiStyle ?? "ClassicRing";
            OuterRadius = config.WheelRadius;
            CoreRadius = config.CoreRadius;
            // Safety boundary carried over from the pre-migration window: the ring
            // must not degenerate, so inner radius stays below outer.
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
                // Re-assert like the pre-migration window did: the engine calls this
                // on every drag move, and the view re-applies the selection (center
                // cancel feedback included) instead of silently skipping no-op moves.
                OnPropertyChanged(nameof(SelectedSectorIndex));
                return;
            }

            SelectedSectorIndex = sectorIndex;
        }

        public void SetOuterEscapeState(bool isEscaped) => IsOuterEscaped = isEscaped;

        public void Close() => IsClosed = true;
    }

    /// <summary>Read-only state of a single 扇区 slot, built from its bound action (or the lack of one).</summary>
    public sealed class WheelSectorViewModel
    {
        public int Index { get; }

        /// <summary>False when the slot has no bound action; the view shows the 未设置 placeholder.</summary>
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
