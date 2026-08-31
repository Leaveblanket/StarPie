using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WinPieGestures
{
    /// <summary>
    /// 外观分区子 ViewModel (T10, ADR-0001)：皮肤选择（UiStyle）、轮盘配色方案与自定义配色预设、
    /// 高亮边缘光晕、几何尺寸、排版与文字显示。全部设置改动即时写穿 <see cref="IConfigService.Current"/>
    /// （立即生效语义）；落盘时机与迁移前逐项一致——防抖项经 <see cref="AutoSaveRequested"/>、
    /// 立即项经 <see cref="SaveNowRequested"/>，由视图层驱动（视图持有防抖计时器与 SyncUiToConfigAndSave）。
    /// 实时预览的绘制留在视图层：视图经 <see cref="PreviewInvalidated"/> 用当前属性值重绘画布。
    /// 配色预设增删改的编排（含命名输入对话框）在此，确认/提示 MessageBox 属视图交互，经事件回调视图。
    /// T16 收编窗口 code-behind 残留的界面主题（AppTheme）与中心核图标状态（透传属性，
    /// 读直取、写直穿运行态配置）与中心核图标选取编排（<see cref="PickCoreIcon"/>）。
    /// </summary>
    public partial class AppearanceSettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;
        private readonly IDialogService _dialogs;

        // Re-entrancy guards（与迁移前窗口 _isUpdatingUi 语义一致）：
        // _loading     构造播种期：只落状态字段，不回写配置、不发事件（绑定随后一次性读取）
        // _bulkUpdating 批量赋值（一键重置）：配置照常写穿，事件由发起方收尾统一发
        // _layoutSyncing ShowText ↔ 排版模式联动时的抑制标志
        private bool _loading = true;
        private bool _bulkUpdating;
        private bool _layoutSyncing;

        /// <summary>任意外观设置变化后请求视图重绘实时预览（视图自行判断分区是否可见）。</summary>
        public event Action? PreviewInvalidated;

        /// <summary>防抖落盘（迁移前 ScheduleAutoSave 语义：400ms 防抖后整窗同步保存）。</summary>
        public event Action? AutoSaveRequested;

        /// <summary>立即落盘（迁移前 SyncUiToConfigAndSave(true) 语义）。</summary>
        public event Action? SaveNowRequested;

        /// <summary>自定义配色预设列表变化（增/删/改名），视图据此重建配色方案下拉的动态项。</summary>
        public event Action? PresetListChanged;

        /// <summary>请求视图弹确认框删除预设（确认后视图回调 <see cref="ConfirmDeleteCustomColorPreset"/>）。</summary>
        public event Action<CustomColorPreset>? DeleteConfirmRequested;

        /// <summary>预设已删除，message 为视图提示文案所需的预设名。</summary>
        public event Action<string>? PresetDeleted;

        /// <summary>预设已保存，message 为视图提示文案所需的预设名。</summary>
        public event Action<string>? PresetSaved;

        public AppearanceSettingsViewModel(IConfigService config, IDialogService dialogs)
        {
            _config = config;
            _dialogs = dialogs;
            LoadFromConfig();
            _loading = false;
        }

        private AppConfig Config => _config.Current;

        /// <summary>自定义配色预设列表（视图重建下拉动态项的数据源；配置可能未初始化，恒不为 null）。</summary>
        public IReadOnlyList<CustomColorPreset> CustomPresets
            => (IReadOnlyList<CustomColorPreset>?)Config.CustomColorPresets ?? Array.Empty<CustomColorPreset>();

        /// <summary>当前选中的自定义配色预设；未选中自定义预设时为 null。</summary>
        public CustomColorPreset? SelectedCustomPreset
        {
            get
            {
                string theme = SelectedTheme ?? "";
                if (!theme.StartsWith("CustomPreset_")) return null;
                string presetId = theme.Substring("CustomPreset_".Length);
                return Config.CustomColorPresets?.Find(p => p.Id == presetId);
            }
        }

        // ---- 皮肤选择 & 配色方案 -------------------------------------------------

        /// <summary>轮盘皮肤（ClassicRing / CleanSectors / Glassmorphism）。与迁移前一致：切换只重绘预览，不主动请求落盘。</summary>
        [ObservableProperty]
        private string _uiStyle = "ClassicRing";

        /// <summary>轮盘配色方案（含 CustomPreset_{id} 动态项）。瞬态空值（下拉动态项重建时的 null 回推）不写入状态。</summary>
        private string _selectedTheme = "System";

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                // 迁移前对 null 选择项同样短路；动态项重建期间绑定回推 null，不得清掉当前主题
                if (string.IsNullOrEmpty(value)) return;
                if (_loading) { _selectedTheme = value; return; }

                if (!SetProperty(ref _selectedTheme, value)) return;

                Config.Theme = value;
                bool isCustomPreset = value.StartsWith("CustomPreset_");
                IsCustomPresetSelected = isCustomPreset;

                if (isCustomPreset)
                {
                    var preset = SelectedCustomPreset;
                    if (preset != null)
                    {
                        // 预设色值回落到微调输入框（属性管线写穿 config，与迁移前文本框回填一致）
                        CustomSectorBgText = preset.SectorBg;
                        CustomSectorBorderText = preset.SectorBorder;
                        CustomHighlightBgText = preset.HighlightBg;
                        CustomHighlightBorderText = preset.HighlightBorder;
                        CustomTextText = preset.TextColor;
                    }
                    IsCustomColorExpanderExpanded = true;
                }

                PreviewInvalidated?.Invoke();
                SaveNowRequested?.Invoke();
            }
        }

        /// <summary>当前选中项是否为自定义配色预设（控制重命名/删除按钮可见性）。</summary>
        [ObservableProperty]
        private bool _isCustomPresetSelected;

        /// <summary>自定义高级配色折叠面板展开态（迁移前：主题为 Custom 或自定义预设时展开，选中预设时强制展开，从不自动收起）。</summary>
        [ObservableProperty]
        private bool _isCustomColorExpanderExpanded;

        // ---- 自定义配色微调（文本框原始内容 + 预览画刷） -----------------------------

        [ObservableProperty]
        private string _customSectorBgText = "";

        [ObservableProperty]
        private Brush _customSectorBgBrush = Brushes.Transparent;

        [ObservableProperty]
        private string _customSectorBorderText = "";

        [ObservableProperty]
        private Brush _customSectorBorderBrush = Brushes.Transparent;

        [ObservableProperty]
        private string _customHighlightBgText = "";

        [ObservableProperty]
        private Brush _customHighlightBgBrush = Brushes.Transparent;

        [ObservableProperty]
        private string _customHighlightBorderText = "";

        [ObservableProperty]
        private Brush _customHighlightBorderBrush = Brushes.Transparent;

        [ObservableProperty]
        private string _customTextText = "";

        [ObservableProperty]
        private Brush _customTextBrush = Brushes.Transparent;

        // ---- 高亮边缘光晕 ---------------------------------------------------------

        [ObservableProperty]
        private string _highlightGlowPreset = "Auto";

        [ObservableProperty]
        private string _highlightGlowColorText = "";

        [ObservableProperty]
        private Brush _highlightGlowColorBrush = Brushes.Transparent;

        /// <summary>光晕弥散半径滑杆值（px，8~48；配置存 0 时按迁移前回落 24）。</summary>
        [ObservableProperty]
        private double _highlightGlowRadius = 24.0;

        /// <summary>光晕不透明度滑杆值（0~100 百分数；配置存 0.0~1.0 小数）。</summary>
        [ObservableProperty]
        private double _highlightGlowOpacityPercent = 85.0;

        /// <summary>自定义光晕颜色行可见性（迁移前仅在预设切换与初始加载时按公式刷新）。</summary>
        [ObservableProperty]
        private bool _isCustomGlowVisible;

        public string HighlightGlowRadiusLabel => $"{HighlightGlowRadius:0} px";
        public string HighlightGlowOpacityLabel => $"{HighlightGlowOpacityPercent:0}%";

        // ---- 几何形态与尺寸 -------------------------------------------------------

        /// <summary>扇区切削形态（Combo Tag：Original / Circle / RoundedCapsule / HexagonHive）。</summary>
        [ObservableProperty]
        private string _shape = "Original";

        [ObservableProperty]
        private double _wheelRadius = 138.0;

        [ObservableProperty]
        private double _innerRadius = 52.0;

        [ObservableProperty]
        private double _coreRadius = 50.0;

        [ObservableProperty]
        private double _sectorGap = 2.0;

        [ObservableProperty]
        private double _sectorCornerRadius = 4.0;

        public string WheelRadiusLabel => $"{WheelRadius:0}";
        public string InnerRadiusLabel => $"{InnerRadius:0}";
        public string CoreRadiusLabel => $"{CoreRadius:0}";
        public string SectorGapLabel => $"{SectorGap:0} px";
        public string SectorCornerRadiusLabel => $"{SectorCornerRadius:0} px";

        // ---- 排版与文字显示 -------------------------------------------------------

        /// <summary>排版模式（IconAndText / IconOnly / TextOnly），与文字显示开关双向联动。</summary>
        [ObservableProperty]
        private string _iconLayoutMode = "IconAndText";

        [ObservableProperty]
        private double _sectorIconSize = 20.0;

        [ObservableProperty]
        private double _sectorFontSize = 10.5;

        [ObservableProperty]
        private bool _showText = true;

        public string SectorIconSizeLabel => $"{SectorIconSize:0} px";
        public string SectorFontSizeLabel => $"{SectorFontSize:0.0} px";

        // ---- 界面主题与中心核图标（T16 自窗口 code-behind 收编） --------------------
        //
        // 透传属性：状态直接住运行态配置（读直取、写直穿），不持副本——配置导入替换实例后
        // 无需重挂即取到新值，与迁移前窗口处理器在保存点对配置对象的直读直写逐字等价。预览重绘与落盘时机由视图层处理器驱动（这些属性的变更不走
        // PreviewInvalidated/AutoSaveRequested 管线，与迁移前一致）。

        /// <summary>软件控制台界面主题（System/Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray）。
        /// 主题应用到窗口属视图效果，经 IThemeService 由窗口驱动。</summary>
        public string AppTheme
        {
            get => Config.AppTheme ?? "System";
            set
            {
                value ??= "System";
                if (string.Equals(Config.AppTheme, value, StringComparison.Ordinal)) return;
                Config.AppTheme = value;
                OnPropertyChanged();
            }
        }

        /// <summary>是否显示核圆中心图标/图案。</summary>
        public bool ShowCoreIcon
        {
            get => Config.ShowCoreIcon;
            set
            {
                if (Config.ShowCoreIcon == value) return;
                Config.ShowCoreIcon = value;
                OnPropertyChanged();
            }
        }

        /// <summary>中心核图案类型（Exit/Crosshair/.../Custom/Image）。</summary>
        public string CoreIconType
        {
            get => Config.CoreIconType ?? "Exit";
            set
            {
                value ??= "Exit";
                if (string.Equals(Config.CoreIconType, value, StringComparison.Ordinal)) return;
                Config.CoreIconType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>中心核自定义矢量图标键（custom: 前缀或图标库键；空为默认五角星）。</summary>
        public string CoreCustomIconKey
        {
            get => Config.CoreCustomIconKey ?? "";
            set
            {
                value ??= "";
                if (string.Equals(Config.CoreCustomIconKey, value, StringComparison.Ordinal)) return;
                Config.CoreCustomIconKey = value;
                OnPropertyChanged();
            }
        }

        /// <summary>中心核自定义 SVG 路径数据（只读透传：窗口层无写入点，预览绘制消费）。</summary>
        public string CoreCustomIconSvg => Config.CoreCustomIconSvg ?? "";

        /// <summary>中心核自定义图片本地路径（写穿透传；文本框逐键写入的迁移前语义）。</summary>
        public string CoreCustomImagePath
        {
            get => Config.CoreCustomImagePath ?? "";
            set
            {
                value = (value ?? "").Trim();
                if (string.Equals(Config.CoreCustomImagePath, value, StringComparison.Ordinal)) return;
                Config.CoreCustomImagePath = value;
                OnPropertyChanged();
            }
        }

        /// <summary>中心核自定义图标选取编排（迁移前 PickCoreIconButton_Click 的对话框部分）：
        /// 取消返回 false 不动状态；确认后写回图标键（null = 清除，写空串）并返回 true，
        /// 预览刷新与落盘由视图层驱动。</summary>
        public bool PickCoreIcon()
        {
            var picked = _dialogs.ShowIconPicker(CoreCustomIconKey);
            if (picked == null) return false;
            CoreCustomIconKey = picked.IconKey ?? "";
            return true;
        }

        // ---- 变更管线（写穿配置 + 通知标签 + 事件） ---------------------------------

        partial void OnUiStyleChanged(string value)
        {
            if (_loading) return;
            Config.UiStyle = value;
            // 与迁移前一致：皮肤切换只重绘预览，不请求落盘
            PreviewInvalidated?.Invoke();
        }

        partial void OnCustomSectorBgTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomSectorBg = c,
            v => CustomSectorBgBrush = v);
        partial void OnCustomSectorBorderTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomSectorBorder = c,
            v => CustomSectorBorderBrush = v);
        partial void OnCustomHighlightBgTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomHighlightBg = c,
            v => CustomHighlightBgBrush = v);
        partial void OnCustomHighlightBorderTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomHighlightBorder = c,
            v => CustomHighlightBorderBrush = v);
        partial void OnCustomTextTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomText = c,
            v => CustomTextBrush = v);

        private void OnCustomColorTextChanged(string value, Action<string> writeConfig, Action<Brush> setBrush)
        {
            if (_loading) return;
            // 与迁移前一致：自定义配色键入写穿配置并重绘，但落盘留给关窗等既有时机
            writeConfig((value ?? "").Trim());
            setBrush(ParseColorBrush(value));
            PreviewInvalidated?.Invoke();
        }

        partial void OnHighlightGlowPresetChanged(string value)
        {
            if (_loading) return;
            Config.HighlightGlowPreset = value;

            switch (value)
            {
                case "Lilac": HighlightGlowColorText = "#A855F7"; break;
                case "Blue": HighlightGlowColorText = "#3B82F6"; break;
                case "Emerald": HighlightGlowColorText = "#10B981"; break;
                case "Rose": HighlightGlowColorText = "#EC4899"; break;
                case "Amber": HighlightGlowColorText = "#F59E0B"; break;
                case "Red": HighlightGlowColorText = "#EF4444"; break;
                case "White": HighlightGlowColorText = "#FFFFFF"; break;
                case "Auto": HighlightGlowColorText = ""; break;
            }

            // 与迁移前一致：先回落色值，再按"Custom 或已有色值"计算可见性
            IsCustomGlowVisible = value == "Custom" || !string.IsNullOrEmpty(HighlightGlowColorText);

            PreviewInvalidated?.Invoke();
            SaveNowRequested?.Invoke();
        }

        partial void OnHighlightGlowColorTextChanged(string value)
        {
            if (_loading) return;
            Config.HighlightGlowColor = (value ?? "").Trim();
            HighlightGlowColorBrush = ParseColorBrush(value);
            PreviewInvalidated?.Invoke();
            AutoSaveRequested?.Invoke();
        }

        partial void OnHighlightGlowRadiusChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(HighlightGlowRadiusLabel));
            Config.HighlightGlowRadius = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnHighlightGlowOpacityPercentChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(HighlightGlowOpacityLabel));
            Config.HighlightGlowOpacity = value / 100.0;
            RaisePreviewAndAutoSave();
        }

        partial void OnShapeChanged(string value)
        {
            if (_loading) return;
            Config.Shape = value;
            if (!_bulkUpdating)
            {
                PreviewInvalidated?.Invoke();
                SaveNowRequested?.Invoke();
            }
        }

        partial void OnWheelRadiusChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(WheelRadiusLabel));
            Config.WheelRadius = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnInnerRadiusChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(InnerRadiusLabel));
            Config.InnerRadius = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnCoreRadiusChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(CoreRadiusLabel));
            Config.CoreRadius = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnSectorGapChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(SectorGapLabel));
            Config.SectorGap = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnSectorCornerRadiusChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(SectorCornerRadiusLabel));
            Config.SectorCornerRadius = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnIconLayoutModeChanged(string value)
        {
            if (_loading) return;
            if (_layoutSyncing)
            {
                // ShowText 联动发起：只写穿配置，事件由发起方统一发（与迁移前 _isUpdatingUi 抑制一致）
                Config.IconLayoutMode = value;
                return;
            }

            Config.IconLayoutMode = value;
            Config.ShowText = value != "IconOnly";
            _layoutSyncing = true;
            try
            {
                ShowText = value != "IconOnly";
            }
            finally
            {
                _layoutSyncing = false;
            }

            PreviewInvalidated?.Invoke();
            SaveNowRequested?.Invoke();
        }

        partial void OnSectorIconSizeChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(SectorIconSizeLabel));
            Config.SectorIconSize = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnSectorFontSizeChanged(double value)
        {
            if (_loading) return;
            OnPropertyChanged(nameof(SectorFontSizeLabel));
            Config.SectorFontSize = value;
            RaisePreviewAndAutoSave();
        }

        partial void OnShowTextChanged(bool value)
        {
            if (_loading) return;
            if (_layoutSyncing) return;

            Config.ShowText = value;

            // 与迁移前联动规则一致：勾选文字时排版模式 IconOnly→IconAndText；取消勾选时改为 IconOnly。
            // 联动发起时置抑制标志：排版模式管线只写穿配置，事件由本处统一发一轮。
            _layoutSyncing = true;
            try
            {
                if (value && IconLayoutMode == "IconOnly")
                {
                    IconLayoutMode = "IconAndText";
                }
                else if (!value && IconLayoutMode != "IconOnly")
                {
                    IconLayoutMode = "IconOnly";
                }
            }
            finally
            {
                _layoutSyncing = false;
            }

            PreviewInvalidated?.Invoke();
            SaveNowRequested?.Invoke();
        }

        private void RaisePreviewAndAutoSave()
        {
            if (_bulkUpdating) return;
            PreviewInvalidated?.Invoke();
            AutoSaveRequested?.Invoke();
        }

        // ---- 命令 -----------------------------------------------------------------

        /// <summary>一键重置为推荐几何尺寸（与迁移前相同的七项默认值，事件收尾统一发一次）。</summary>
        [RelayCommand]
        private void ResetDimensions()
        {
            _bulkUpdating = true;
            try
            {
                WheelRadius = 138;
                InnerRadius = 52;
                CoreRadius = 50;
                SectorGap = 2;
                SectorCornerRadius = 4;
                SectorIconSize = 20.0;
                SectorFontSize = 10.5;
            }
            finally
            {
                _bulkUpdating = false;
            }

            PreviewInvalidated?.Invoke();
            SaveNowRequested?.Invoke();
        }

        /// <summary>打开调色板选取颜色（tag 定位目标色值：五个自定义配色 + 高亮光晕色）。</summary>
        [RelayCommand]
        private void PickColor(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            var picked = _dialogs.ShowColorPicker(GetColorTextByTag(tag));
            if (picked != null)
            {
                SetColorTextByTag(tag, picked.HexColor);
            }
        }

        /// <summary>屏上取色（tag 定位目标色值，同上）。</summary>
        [RelayCommand]
        private void PickEyedropper(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            var picked = _dialogs.ShowEyedropper();
            if (picked != null)
            {
                SetColorTextByTag(tag, picked.HexColor);
            }
        }

        private string GetColorTextByTag(string tag) => tag switch
        {
            "CustomSectorBg" => CustomSectorBgText,
            "CustomSectorBorder" => CustomSectorBorderText,
            "CustomHighlightBg" => CustomHighlightBgText,
            "CustomHighlightBorder" => CustomHighlightBorderText,
            "CustomText" => CustomTextText,
            "HighlightGlowColor" => HighlightGlowColorText,
            _ => ""
        };

        private void SetColorTextByTag(string tag, string hex)
        {
            switch (tag)
            {
                case "CustomSectorBg": CustomSectorBgText = hex; break;
                case "CustomSectorBorder": CustomSectorBorderText = hex; break;
                case "CustomHighlightBg": CustomHighlightBgText = hex; break;
                case "CustomHighlightBorder": CustomHighlightBorderText = hex; break;
                case "CustomText": CustomTextText = hex; break;
                case "HighlightGlowColor": HighlightGlowColorText = hex; break;
            }
        }

        /// <summary>把当前自定义配色保存为预设（命名输入 → 新建 → 选中 → 落盘；取消则不动）。</summary>
        [RelayCommand]
        private void SavePreset()
        {
            var result = _dialogs.ShowInputDialog("保存配色预设", "请输入自定义配色方案名称:", $"自定义配色 {DateTime.Now:MMdd-HHmm}");
            if (result == null) return;

            string presetName = result.Text;
            if (Config.CustomColorPresets == null)
            {
                Config.CustomColorPresets = new List<CustomColorPreset>();
            }

            var newPreset = new CustomColorPreset
            {
                Name = presetName,
                SectorBg = (CustomSectorBgText ?? "").Trim(),
                SectorBorder = (CustomSectorBorderText ?? "").Trim(),
                HighlightBg = (CustomHighlightBgText ?? "").Trim(),
                HighlightBorder = (CustomHighlightBorderText ?? "").Trim(),
                TextColor = (CustomTextText ?? "").Trim()
            };

            Config.CustomColorPresets.Add(newPreset);
            Config.Theme = "CustomPreset_" + newPreset.Id;

            // 先发列表变化让视图重建下拉项（新 Tag 才有落点），再切选中触发主题管线
            PresetListChanged?.Invoke();
            SelectedTheme = "CustomPreset_" + newPreset.Id;
            PresetSaved?.Invoke(presetName);
        }

        /// <summary>重命名当前选中的自定义配色预设（仅选中自定义预设时有效；取消则不动）。</summary>
        [RelayCommand]
        private void RenamePreset()
        {
            var preset = SelectedCustomPreset;
            if (preset == null) return;

            string oldName = preset.Name;
            var result = _dialogs.ShowInputDialog(
                title: I18n.T("RenameCustomPresetTitle"),
                prompt: $"{I18n.T("RenameCustomPresetPrompt")}「{oldName}」",
                defaultText: oldName,
                validator: input =>
                {
                    if (string.IsNullOrWhiteSpace(input)) return (false, "配色方案名称不能为空！");
                    return (true, "");
                });

            if (result == null) return;

            preset.Name = result.Text;
            PresetListChanged?.Invoke();
            SaveNowRequested?.Invoke();
            PreviewInvalidated?.Invoke();
        }

        /// <summary>删除当前选中的自定义配色预设：先经视图确认，再执行 <see cref="DeleteConfirmRequested"/> 回调链。</summary>
        [RelayCommand]
        private void DeletePreset()
        {
            var preset = SelectedCustomPreset;
            if (preset == null) return;
            DeleteConfirmRequested?.Invoke(preset);
        }

        /// <summary>视图确认后执行删除：移除预设、回落 System 配色并落盘（事件由主题管线统一发出）。</summary>
        public void ConfirmDeleteCustomColorPreset(CustomColorPreset preset)
        {
            if (preset == null) return;
            if (Config.CustomColorPresets == null || !Config.CustomColorPresets.Contains(preset)) return;

            Config.CustomColorPresets.Remove(preset);
            Config.Theme = "System";

            PresetListChanged?.Invoke();
            SelectedTheme = "System";
            PresetDeleted?.Invoke(preset.Name);
        }

        // ---- 播种与纯函数 ----------------------------------------------------------

        /// <summary>
        /// 从配置播种状态（经属性赋值；<see cref="_loading"/> 使各 On*Changed 管线短路——
        /// 不回写配置、不发事件；绑定在 DataContext 挂上后一次性读取当前值）。
        /// </summary>
        private void LoadFromConfig()
        {
            var c = Config;

            UiStyle = string.IsNullOrEmpty(c.UiStyle) ? "ClassicRing" : c.UiStyle;
            SelectedTheme = string.IsNullOrEmpty(c.Theme) ? "System" : c.Theme;
            Shape = MapLegacyShapeTag(c.Shape);
            IconLayoutMode = string.IsNullOrEmpty(c.IconLayoutMode) ? "IconAndText" : c.IconLayoutMode;
            ShowText = c.ShowText;

            WheelRadius = c.WheelRadius;
            InnerRadius = c.InnerRadius;
            CoreRadius = c.CoreRadius;
            SectorGap = c.SectorGap;
            SectorCornerRadius = c.SectorCornerRadius;
            SectorIconSize = c.SectorIconSize > 0 ? c.SectorIconSize : 20.0;
            SectorFontSize = c.SectorFontSize > 0 ? c.SectorFontSize : 10.5;

            HighlightGlowPreset = c.HighlightGlowPreset ?? "Auto";
            HighlightGlowColorText = c.HighlightGlowColor ?? "";
            HighlightGlowRadius = c.HighlightGlowRadius > 0 ? c.HighlightGlowRadius : 24.0;
            HighlightGlowOpacityPercent = (c.HighlightGlowOpacity >= 0 ? c.HighlightGlowOpacity : 0.85) * 100.0;
            IsCustomGlowVisible = HighlightGlowPreset == "Custom" || !string.IsNullOrEmpty(HighlightGlowColorText);
            HighlightGlowColorBrush = ParseColorBrush(HighlightGlowColorText);

            CustomSectorBgText = c.CustomSectorBg ?? "";
            CustomSectorBorderText = c.CustomSectorBorder ?? "";
            CustomHighlightBgText = c.CustomHighlightBg ?? "";
            CustomHighlightBorderText = c.CustomHighlightBorder ?? "";
            CustomTextText = c.CustomText ?? "";
            CustomSectorBgBrush = ParseColorBrush(CustomSectorBgText);
            CustomSectorBorderBrush = ParseColorBrush(CustomSectorBorderText);
            CustomHighlightBgBrush = ParseColorBrush(CustomHighlightBgText);
            CustomHighlightBorderBrush = ParseColorBrush(CustomHighlightBorderText);
            CustomTextBrush = ParseColorBrush(CustomTextText);

            IsCustomPresetSelected = SelectedTheme.StartsWith("CustomPreset_");
            IsCustomColorExpanderExpanded = c.Theme == "Custom" || IsCustomPresetSelected;
        }

        /// <summary>旧版 Shape 标签 → 当前 Combo Tag（迁移前 SetComboBoxSelectedValue 的映射表原样保留）。</summary>
        public static string MapLegacyShapeTag(string? shape)
        {
            string value = shape ?? "";
            if (value is "RoundedRect" or "FloatingCapsules" or "Capsule") return "RoundedCapsule";
            if (value is "OrganicPetals" or "ArcTracker" or "LiquidDroplets" or "MinimalArc") return "Original";
            return value;
        }

        /// <summary>十六进制串 → 预览画刷；空串与非法值一律透明（迁移前 UpdateColorPreviewBorder 行为）。</summary>
        public static Brush ParseColorBrush(string? hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return Brushes.Transparent;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}
