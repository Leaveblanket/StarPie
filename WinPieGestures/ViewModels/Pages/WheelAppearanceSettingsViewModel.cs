using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using WinPieGestures.Services;

namespace WinPieGestures.ViewModels.Pages
{
    /// <summary>
    /// 轮盘模块外观设置子 ViewModel（#56，ADR-0014 决策 5/6/8）：承接原外观聚合 VM 的轮盘外观
    /// 配置面——外观页除界面主题外的全部轮盘外观设置：皮肤选择（UiStyle）、轮盘配色方案与自定义
    /// 配色预设、高亮边缘光晕、几何尺寸、排版与文字显示、中心核图标。全部设置改动即时写穿
    /// <see cref="IConfigService.Current"/>（立即生效语义）；落盘请求经 <see cref="IMessenger"/>
    /// 上报组合根编排订阅者（防抖/立即两类消息，T19 起取代迁移前的事件上报）。
    /// 实时预览的绘制留在视图层：预览属性变更（含 <see cref="ShowCoreIcon"/>，#56 起一并）经
    /// <see cref="AppearancePreviewInvalidatedMessage"/> 通知页面 View 重绘画布；配色预设增删改的
    /// 编排（命名输入与删除确认/结果提示对话框）在此（T19 对话框编排内聚进 VM；#53 名称语义原样保留）。
    /// T19 页面化：构造注入 <see cref="ProfileListViewModel"/> 单例——预览画哪个方案是编译期可见的
    /// 静态已知依赖，不走消息（ADR-0005）；导入成功经 <see cref="ConfigImportedMessage"/> 广播后
    /// 自行从配置重挂状态（播种快照属性导入后会过期），并重建配色下拉选项（T21 ItemsSource 化）。
    /// T16 收编窗口 code-behind 残留的中心核图标状态（透传属性，读直取、写直穿运行态配置）与
    /// 中心核图标选取编排（<see cref="PickCoreIcon"/>）；界面主题（AppTheme）由 #54 拆入
    /// <see cref="InterfaceThemeSettingsViewModel"/>（本 VM 不持有）。
    /// #55/#56（ADR-0014 决策 8）：本子 VM 实现轮盘模块只读状态接口 <see cref="IWheelAppearanceState"/>
    /// ——预览渲染器与页面预览 code-behind 只依赖该接口，不再以具体聚合 VM 类型为参数。
    /// 生命周期（ADR-0010 第 3 条）：DI 单例注入外观聚合 VM <see cref="AppearanceSettingsViewModel"/>
    /// （暴露为 <see cref="AppearanceSettingsViewModel.WheelAppearance"/>）；语言订阅成对退订并随
    /// 容器释放（组合根随 Composition.Dispose 调用）。
    /// </summary>
    public partial class WheelAppearanceSettingsViewModel : ObservableObject, IWheelAppearanceState, IDisposable
    {
        private readonly IConfigService _config;
        private readonly IDialogService _dialogs;
        private readonly IMessenger _messenger;
        private readonly ILocalizationService _localization;
        private bool _disposed;

        // Re-entrancy guards（与迁移前窗口 _isUpdatingUi 语义一致）：
        // _loading     构造播种期：只落状态字段，不回写配置、不发事件（绑定随后一次性读取）
        // _bulkUpdating 批量赋值（一键重置）：配置照常写穿，事件由发起方收尾统一发
        // _layoutSyncing ShowText ↔ 排版模式联动时的抑制标志
        private bool _loading = true;
        private bool _bulkUpdating;
        private bool _layoutSyncing;

        /// <summary>方案列表分区 ViewModel 单例（T19 构造注入）：预览读取选中方案——静态已知依赖
        /// 走构造注入不走消息（ADR-0005，Spec 决策 13）。</summary>
        public ProfileListViewModel ProfileList { get; }

        /// <summary>运行态配置访问（T19：预览渲染初始化等 View 层读取；导入后自动取到新实例，
        /// 与迁移前根 VM / #56 前聚合 VM 的 CurrentConfig 语义一致）。</summary>
        public AppConfig CurrentConfig => _config.Current;

        /// <summary>预览渲染所用 Profile 上下文（#55 接口成员，#56 起由本子 VM 实现）：优先选中方案，
        /// 无选中时回落列表首项——与迁移前 WheelPreviewRenderer 的取值链一致；空列表兜底仍留在渲染器。</summary>
        public WheelProfile? PreviewProfile
            => ProfileList.SelectedProfile?.Model ?? ProfileList.Profiles.FirstOrDefault()?.Model;

        public WheelAppearanceSettingsViewModel(
            IConfigService config,
            IDialogService dialogs,
            IMessenger messenger,
            ProfileListViewModel profileList,
            ILocalizationService localization)
        {
            _config = config;
            _dialogs = dialogs;
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            ProfileList = profileList ?? throw new ArgumentNullException(nameof(profileList));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));

            // #56（ADR-0014 决策 6）：轮盘外观设置子 VM 承接聚合 VM 的轮盘外观配置面——导入成功
            // 广播后自行从新配置重挂播种快照（透传属性读穿配置本就即时）并重建配色下拉；页面级
            // View 效果（预览重绘等）由外观聚合 VM 收尾广播 PageConfigReloadedMessage。
            messenger.Register<ConfigImportedMessage>(this, (_, _) => ReloadFromConfig());

            LoadFromConfig();
            _loading = false;

            // ADR-0010 驻留文案机制：轮盘配色选项标签随语言切换重建（单例 VM 成对退订）。
            _localization.LanguageChanged += OnLanguageChanged;
        }

        private AppConfig Config => _config.Current;

        /// <summary>自定义配色预设列表（视图重建下拉动态项的数据源；配置可能未初始化，恒不为 null）。</summary>
        public IReadOnlyList<CustomColorPreset> CustomPresets
            => (IReadOnlyList<CustomColorPreset>?)Config.CustomColorPresets ?? Array.Empty<CustomColorPreset>();

        /// <summary>轮盘配色下拉的固定配色项（Tag 与迁移前 XAML 静态 ComboBoxItem 一致；标签即时取词）。</summary>
        private ThemeOptionItem[] BuildStaticThemeOptions()
        {
            return new ThemeOptionItem[]
            {
                new("System", _localization.GetString("WheelThemeSystem")),
                new("Dark", _localization.GetString("WheelThemeDark")),
                new("Light", _localization.GetString("WheelThemeLight")),
                new("MatchaForest", _localization.GetString("WheelThemeMatchaForest")),
                new("GlacialIce", _localization.GetString("WheelThemeGlacialIce")),
                new("MorandiMuted", _localization.GetString("WheelThemeMorandiMuted"))
            };
        }

        private IReadOnlyList<ThemeOptionItem> _themeOptions = Array.Empty<ThemeOptionItem>();

        /// <summary>轮盘配色下拉选项（固定配色 + 自定义预设；导入/增删改名后重建，T21 ItemsSource 化）。</summary>
        public IReadOnlyList<ThemeOptionItem> ThemeOptions
        {
            get => _themeOptions;
            private set => SetProperty(ref _themeOptions, value);
        }

        /// <summary>重建 <see cref="ThemeOptions"/>：固定配色在前、自定义预设按配置顺序追加（Tag=CustomPreset_{id}）。</summary>
        private void RebuildThemeOptions()
        {
            var presets = Config.CustomColorPresets;
            var staticOptions = BuildStaticThemeOptions();
            var options = new List<ThemeOptionItem>(staticOptions.Length + (presets?.Count ?? 0));
            options.AddRange(staticOptions);
            if (presets != null)
            {
                foreach (var preset in presets)
                {
                    options.Add(new ThemeOptionItem(
                        $"CustomPreset_{preset.Id}",
                        string.Format(_localization.GetString("WheelThemeCustomPreset"), preset.Name)));
                }
            }
            ThemeOptions = options;
        }

        /// <summary>语言切换后重建轮盘配色下拉选项（固定标签 + 自定义预设后缀均为文案，预设名保持用户数据）。</summary>
        private void OnLanguageChanged()
        {
            RebuildThemeOptions();
            // 同 ReloadFromConfig 语义：补发选中通知，让 ComboBox 从新 ThemeOptions 恢复选中。
            OnPropertyChanged(nameof(SelectedTheme));
        }

        /// <summary>退订本地化事件（ADR-0010 第 3 条：单例 VM 配 IDisposable，组合根随 Composition.Dispose 调用）。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _localization.LanguageChanged -= OnLanguageChanged;
        }

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

                _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
                _messenger.Send(ImmediateSaveRequestedMessage.Instance);
            }
        }

        /// <summary>当前选中项是否为自定义配色预设（控制重命名/删除按钮可见性）。</summary>
        [ObservableProperty]
        private bool _isCustomPresetSelected;

        /// <summary>自定义高级配色折叠面板展开态（迁移前：主题为 Custom 或自定义预设时展开，选中预设时强制展开，从不自动收起）。</summary>
        [ObservableProperty]
        private bool _isCustomColorExpanderExpanded;

        // ---- 自定义配色微调（文本框原始内容，预览由 View 经 HexToBrushConverter 转成画刷） -----------------------------

        [ObservableProperty]
        private string _customSectorBgText = "";

        [ObservableProperty]
        private string _customSectorBorderText = "";

        [ObservableProperty]
        private string _customHighlightBgText = "";

        [ObservableProperty]
        private string _customHighlightBorderText = "";

        [ObservableProperty]
        private string _customTextText = "";

        // ---- 高亮边缘光晕 ---------------------------------------------------------

        [ObservableProperty]
        private string _highlightGlowPreset = "Auto";

        [ObservableProperty]
        private string _highlightGlowColorText = "";

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

        // ---- 中心核图标（T16 自窗口 code-behind 收编） ------------------------------
        //
        // 透传属性：状态直接住运行态配置（读直取、写直穿），不持副本——配置导入替换实例后
        // 无需重挂即取到新值，与迁移前窗口处理器在保存点对配置对象的直读直写逐字等价。
        // 界面主题（AppTheme）已随 #54 拆入 InterfaceThemeSettingsViewModel（见类头）。
        // T17/#56：变更归队 Live-apply 管线——ShowCoreIcon 上报防抖落盘并（#56 起）同报预览重绘
        // （ADR-0014 决策 7：预览重绘全部经 AppearancePreviewInvalidatedMessage 消息管线，页面
        // ShowCoreIconCheckBox 的 Checked/Unchecked 事件处理器已删除）；CoreIconType/
        // CoreCustomImagePath 同时上报预览重绘（对应迁移前各自 SelectionChanged/TextChanged
        // 处理器里的重绘与落盘调用；绑定初始化回推同值时被 setter 的等值守卫短路，不触发事件）。

        /// <summary>是否显示核圆中心图标/图案。</summary>
        public bool ShowCoreIcon
        {
            get => Config.ShowCoreIcon;
            set
            {
                if (Config.ShowCoreIcon == value) return;
                Config.ShowCoreIcon = value;
                OnPropertyChanged();
                _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
                _messenger.Send(DebouncedSaveRequestedMessage.Instance);
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
                _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
                _messenger.Send(DebouncedSaveRequestedMessage.Instance);
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
                _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
                _messenger.Send(DebouncedSaveRequestedMessage.Instance);
            }
        }

        /// <summary>中心核自定义图标选取编排（迁移前 PickCoreIconButton_Click 的对话框部分）：
        /// 取消返回 false 不动状态；确认后写回图标键（null = 清除，写空串）并经
        /// 消息请求立即落盘，预览刷新由视图层驱动。</summary>
        [RelayCommand]
        private void PickCoreIcon()
        {
            var picked = _dialogs.ShowIconPicker(CoreCustomIconKey);
            if (picked == null) return;
            CoreCustomIconKey = picked.IconKey ?? "";
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        /// <summary>中心核自定义图片选取编排（T19 自页面 View 收编，页面保持无参构造不经容器）：
        /// 取消返回 false 不动状态；选中写回 <see cref="CoreCustomImagePath"/>（绑定回填文本框、
        /// 缩略图随 PropertyChanged 刷新，落盘/预览由该属性管线发出）。</summary>
        [RelayCommand]
        private void BrowseCoreImage()
        {
            var picked = _dialogs.ShowOpenFileDialog(
                _localization.GetString("ImageFileFilter"),
                _localization.GetString("CoreImageBrowseTitle"));
            if (picked == null) return;

            CoreCustomImagePath = picked.Path;
        }

        [RelayCommand]
        private void ClearCoreImage() => CoreCustomImagePath = "";

        // ---- 变更管线（写穿配置 + 通知标签 + 事件） ---------------------------------

        partial void OnUiStyleChanged(string value)
        {
            if (_loading) return;
            Config.UiStyle = value;
            // 与迁移前一致：皮肤切换只重绘预览，不请求落盘
            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
        }

        partial void OnCustomSectorBgTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomSectorBg = c);
        partial void OnCustomSectorBorderTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomSectorBorder = c);
        partial void OnCustomHighlightBgTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomHighlightBg = c);
        partial void OnCustomHighlightBorderTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomHighlightBorder = c);
        partial void OnCustomTextTextChanged(string value) => OnCustomColorTextChanged(value, c => Config.CustomText = c);

        private void OnCustomColorTextChanged(string value, Action<string> writeConfig)
        {
            if (_loading) return;
            // 与迁移前一致：自定义配色键入写穿配置并重绘，但落盘留给关窗等既有时机
            writeConfig((value ?? "").Trim());
            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
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

            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        partial void OnHighlightGlowColorTextChanged(string value)
        {
            if (_loading) return;
            Config.HighlightGlowColor = (value ?? "").Trim();
            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
            _messenger.Send(DebouncedSaveRequestedMessage.Instance);
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
                _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
                _messenger.Send(ImmediateSaveRequestedMessage.Instance);
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

            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
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

            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        private void RaisePreviewAndAutoSave()
        {
            if (_bulkUpdating) return;
            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
            _messenger.Send(DebouncedSaveRequestedMessage.Instance);
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

            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
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

        /// <summary>把当前自定义配色保存为预设（命名输入 → 新建 → 选中 → 落盘；取消则不动）。
        /// 对话框文案即时取词（标题/提示/默认名模板/成功提示键化）；名称 trim 后入库，
        /// trim 后为空拒绝（键化报错）——默认名 = 本地化模板 + 时间戳，落库后即用户数据。</summary>
        [RelayCommand]
        private void SavePreset()
        {
            string defaultName = string.Format(
                _localization.GetString("CustomPresetDefaultName"),
                DateTime.Now.ToString("MMdd-HHmm"));
            var result = _dialogs.ShowInputDialog(
                _localization.GetString("SaveCustomPresetTitle"),
                _localization.GetString("SaveCustomPresetPrompt"),
                defaultName);
            if (result == null) return;

            string presetName = result.Text.Trim();
            if (RejectBlankPresetName(presetName)) return;

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

            // 先重建下拉项（新 Tag 才有落点），再切选中触发主题管线
            RebuildThemeOptions();
            SelectedTheme = "CustomPreset_" + newPreset.Id;
            _dialogs.ShowInfo(
                _localization.GetString("Notice"),
                string.Format(_localization.GetString("SaveCustomPresetSuccess"), presetName));
        }

        /// <summary>重命名当前选中的自定义配色预设（仅选中自定义预设时有效；取消则不动）。
        /// 对话框文案即时取词（标题/含旧名提示键化）；新名 trim 后入库，trim 后为空拒绝（键化报错）。</summary>
        [RelayCommand]
        private void RenamePreset()
        {
            var preset = SelectedCustomPreset;
            if (preset == null) return;

            string oldName = preset.Name;
            var result = _dialogs.ShowInputDialog(
                title: _localization.GetString("RenameCustomPresetTitle"),
                prompt: string.Format(_localization.GetString("RenameCustomPresetPrompt"), oldName),
                defaultText: oldName);

            if (result == null) return;

            string newName = result.Text.Trim();
            if (RejectBlankPresetName(newName)) return;

            preset.Name = newName;
            RebuildThemeOptions();
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
            _messenger.Send(AppearancePreviewInvalidatedMessage.Instance);
        }

        /// <summary>删除当前选中的自定义配色预设（T19：删除确认与结果提示对话框编排内聚进本 VM）：
        /// 确认框（标题/含预设名的确认文案键化）→ 移除预设、回落 System 配色并落盘
        /// （事件由主题管线统一发出）→ 成功提示（键化）。</summary>
        [RelayCommand]
        private void DeletePreset()
        {
            var preset = SelectedCustomPreset;
            if (preset == null) return;

            if (_dialogs.Confirm(
                _localization.GetString("DeleteCustomPresetTitle"),
                string.Format(_localization.GetString("MsgConfirmDeletePreset"), preset.Name)))
            {
                ConfirmDeleteCustomColorPreset(preset);
            }
        }

        /// <summary>确认后执行删除：移除预设、回落 System 配色并落盘，随后提示删除成功（键化）。</summary>
        public void ConfirmDeleteCustomColorPreset(CustomColorPreset preset)
        {
            if (preset == null) return;
            if (Config.CustomColorPresets == null || !Config.CustomColorPresets.Contains(preset)) return;

            Config.CustomColorPresets.Remove(preset);
            Config.Theme = "System";

            RebuildThemeOptions();
            SelectedTheme = "System";
            _dialogs.ShowInfo(
                _localization.GetString("Notice"),
                string.Format(_localization.GetString("DeleteCustomPresetSuccess"), preset.Name));
        }

        /// <summary>保存/重命名共用空名守卫：trim 后为空即弹键化提示并返回 true（不动配置、不入库）。</summary>
        private bool RejectBlankPresetName(string name)
        {
            if (name.Length > 0) return false;
            _dialogs.ShowInfo(_localization.GetString("Notice"), _localization.GetString("CustomPresetNameEmpty"));
            return true;
        }

        // ---- 播种与纯函数 ----------------------------------------------------------

        /// <summary>
        /// 导入配置后从当前配置重挂播种快照状态 (T19)：<see cref="_loading"/> 抑制与构造播种一致
        /// （只落状态不回写配置、不发落盘/预览事件），随后补发绑定通知让透传属性绑定同步拉取新值，
        /// 并重建 <see cref="ThemeOptions"/> 供配色下拉（T21 ItemsSource 化）。
        /// </summary>
        public void ReloadFromConfig()
        {
            _loading = true;
            try
            {
                LoadFromConfig();
            }
            finally
            {
                _loading = false;
            }

            OnPropertyChanged(nameof(ShowCoreIcon));
            OnPropertyChanged(nameof(CoreIconType));
            OnPropertyChanged(nameof(CoreCustomIconKey));
            OnPropertyChanged(nameof(CoreCustomIconSvg));
            OnPropertyChanged(nameof(CoreCustomImagePath));
            // T21：重建下拉项后补发选中通知，让 ComboBox 从新 ThemeOptions 中恢复选中（
            // LoadFromConfig 播种期 SelectedTheme 走 _loading 短路不通知）。
            OnPropertyChanged(nameof(SelectedTheme));
        }

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

            CustomSectorBgText = c.CustomSectorBg ?? "";
            CustomSectorBorderText = c.CustomSectorBorder ?? "";
            CustomHighlightBgText = c.CustomHighlightBg ?? "";
            CustomHighlightBorderText = c.CustomHighlightBorder ?? "";
            CustomTextText = c.CustomText ?? "";

            IsCustomPresetSelected = SelectedTheme.StartsWith("CustomPreset_");
            IsCustomColorExpanderExpanded = c.Theme == "Custom" || IsCustomPresetSelected;

            RebuildThemeOptions();
        }

        /// <summary>旧版 Shape 标签 → 当前 Combo Tag（迁移前 SetComboBoxSelectedValue 的映射表原样保留）。</summary>
        public static string MapLegacyShapeTag(string? shape)
        {
            string value = shape ?? "";
            if (value is "RoundedRect" or "FloatingCapsules" or "Capsule") return "RoundedCapsule";
            if (value is "OrganicPetals" or "ArcTracker" or "LiquidDroplets" or "MinimalArc") return "Original";
            return value;
        }

    }

    /// <summary>轮盘配色下拉选项条目（T21 ItemsSource 化）：Tag 供 SelectedValue 匹配，Label 为展示文案。</summary>
    public sealed class ThemeOptionItem
    {
        public string Tag { get; }

        public string Label { get; }

        public ThemeOptionItem(string tag, string label)
        {
            Tag = tag;
            Label = label;
        }

        // 兜底展示/UIA 可访问文本（DisplayMemberPath 之外的安全网）。
        public override string ToString() => Label;
    }
}
