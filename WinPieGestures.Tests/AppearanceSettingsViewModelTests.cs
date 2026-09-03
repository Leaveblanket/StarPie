using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures;
using WinPieGestures.Views.Converters;
using Brush = System.Windows.Media.Brush;

namespace WinPieGestures.Tests;

/// <summary>
/// 外观分区子 ViewModel 的行为覆盖 (T10)：构造播种（含默认值回落与旧版 Shape 标签映射）、
/// 立即生效写穿 IConfigService、防抖/立即落盘事件语义、ShowText 与排版模式联动、
/// 配色预设增删改编排（mock 对话框服务）、实时预览失效事件。
/// </summary>
public sealed class AppearanceSettingsViewModelTests
{
    /// <summary>内存配置服务假实现：直接持有 POCO，记录 Save 调用。</summary>
    private sealed class FakeConfigService : IConfigService
    {
        public AppConfig Current { get; set; } = new();
        public int SaveCalls;
        public void Load() { }
        public void Save() => SaveCalls++;
        public WheelProfile GetProfileForProcess(string processName) => Current.Profiles[0];
        public WheelProfile GetGlobalProfile() => Current.Profiles[0];
    }

    /// <summary>对话框服务假实现：只落地 VM 用到的三类方法，其余不该被调用。</summary>
    private sealed class FakeDialogService : IDialogService
    {
        public InputDialogResult? InputResult;
        public int InputCalls;
        public string? LastInputDefaultText;
        public Func<string, (bool IsValid, string ErrorMessage)>? LastValidator;

        public ColorPickResult? ColorResult;
        public int ColorCalls;
        public string? LastColorPickerInitial;

        public EyedropResult? EyedropResult;
        public int EyedropCalls;

        public IconPickResult? IconResult;
        public int IconCalls;
        public string? LastIconPickerCurrentKey;

        public InputDialogResult? ShowInputDialog(string title, string prompt, string defaultText = "", Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        {
            InputCalls++;
            LastInputDefaultText = defaultText;
            LastValidator = validator;
            return InputResult;
        }

        public IconPickResult? ShowIconPicker(string? currentIconKey)
        {
            IconCalls++;
            LastIconPickerCurrentKey = currentIconKey;
            return IconResult;
        }

        public ColorPickResult? ShowColorPicker(string initialHex)
        {
            ColorCalls++;
            LastColorPickerInitial = initialHex;
            return ColorResult;
        }

        public EyedropResult? ShowEyedropper()
        {
            EyedropCalls++;
            return EyedropResult;
        }

        public bool ConfirmResult = true;
        public readonly List<(string Title, string Message)> Confirms = new();
        public readonly List<(string Title, string Message)> Infos = new();

        public ProgramPickResult? ShowProgramPicker() => throw new NotSupportedException();
        public FilePickResult? ShowOpenFileDialog(string filter, string? title = null) => throw new NotSupportedException();
        public FilePickResult? ShowSaveFileDialog(string filter, string? fileName = null, string? title = null) => throw new NotSupportedException();
        public FilePickResult? ShowFolderDialog(string? initialDirectory = null, string? title = null) => throw new NotSupportedException();
        public bool Confirm(string title, string message) { Confirms.Add((title, message)); return ConfirmResult; }
        public void ShowInfo(string title, string message) => Infos.Add((title, message));
    }

    /// <summary>
    /// 事件/消息计数器 (T19)：视图事件（预览/预设列表）照旧订阅；落盘请求转发到
    /// <see cref="SaveSpy"/> 消息计数（可赋值以支持用例中途清零）；删除确认与保存提示
    /// 经对话框替身的记录断言（T19 对话框编排内聚进 VM）。
    /// </summary>
    private sealed class EventLog
    {
        private readonly SaveSpy _spy;
        public int Preview; public int PresetList;
        public int AutoSave { get => _spy.Debounced; set => _spy.Debounced = value; }
        public int SaveNow { get => _spy.Immediate; set => _spy.Immediate = value; }

        private EventLog(SaveSpy spy) => _spy = spy;

        public static EventLog Attach(WeakReferenceMessenger messenger, SaveSpy spy)
        {
            var log = new EventLog(spy);
            messenger.Register<AppearancePreviewInvalidatedMessage>(log, (_, _) => log.Preview++);
            messenger.Register<AppearancePresetListChangedMessage>(log, (_, _) => log.PresetList++);
            return log;
        }
    }

    private static (AppearanceSettingsViewModel Vm, FakeConfigService Config, FakeDialogService Dialogs, EventLog Log)
        Create(AppConfig? config = null)
    {
        var configService = new FakeConfigService { Current = config ?? new AppConfig() };
        var dialogs = new FakeDialogService();
        var (messenger, spy) = SaveSpy.Create();
        var profileList = new ProfileListViewModel(configService.Current.Profiles, dialogs, messenger, new TestActionExecutor());
        var vm = new AppearanceSettingsViewModel(configService, dialogs, messenger, profileList);
        return (vm, configService, dialogs, EventLog.Attach(messenger, spy));
    }

    // --- 构造播种 -------------------------------------------------------------------

    [Fact]
    public void Constructor_SeedsValuesFromConfig_AndDoesNotWriteOrRaise()
    {
        var config = new AppConfig
        {
            UiStyle = "Glassmorphism", Theme = "Dark", Shape = "Circle",
            WheelRadius = 160, InnerRadius = 60, CoreRadius = 55, SectorGap = 3, SectorCornerRadius = 6,
            ShowText = false, IconLayoutMode = "TextOnly", SectorIconSize = 28, SectorFontSize = 12.5,
            HighlightGlowPreset = "Blue", HighlightGlowColor = "#3B82F6",
            HighlightGlowRadius = 32, HighlightGlowOpacity = 0.5,
            CustomSectorBg = "#111111", CustomText = "#FFFFFF"
        };

        var (vm, configService, _, log) = Create(config);

        Assert.Equal("Glassmorphism", vm.UiStyle);
        Assert.Equal("Dark", vm.SelectedTheme);
        Assert.Equal("Circle", vm.Shape);
        Assert.Equal(160, vm.WheelRadius);
        Assert.Equal("160", vm.WheelRadiusLabel);
        Assert.Equal(60, vm.InnerRadius);
        Assert.Equal(55, vm.CoreRadius);
        Assert.Equal("3 px", vm.SectorGapLabel);
        Assert.Equal("6 px", vm.SectorCornerRadiusLabel);
        Assert.False(vm.ShowText);
        Assert.Equal("TextOnly", vm.IconLayoutMode);
        Assert.Equal(28, vm.SectorIconSize);
        Assert.Equal(12.5, vm.SectorFontSize);
        Assert.Equal("12.5 px", vm.SectorFontSizeLabel);
        Assert.Equal("Blue", vm.HighlightGlowPreset);
        Assert.Equal("#3B82F6", vm.HighlightGlowColorText);
        Assert.Equal(32, vm.HighlightGlowRadius);
        Assert.Equal("32 px", vm.HighlightGlowRadiusLabel);
        Assert.Equal(50, vm.HighlightGlowOpacityPercent);
        Assert.Equal("50%", vm.HighlightGlowOpacityLabel);
        Assert.True(vm.IsCustomGlowVisible);
        Assert.Equal("#111111", vm.CustomSectorBgText);
        Assert.Equal("#FFFFFF", vm.CustomTextText);
        Assert.False(vm.IsCustomPresetSelected);
        Assert.False(vm.IsCustomColorExpanderExpanded);

        // 播种不写穿配置（配置值保持原样）、不发任何事件
        Assert.Equal(0, configService.SaveCalls);
        Assert.Equal(0, log.Preview);
        Assert.Equal(0, log.AutoSave);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void Constructor_AppliesBackwardCompatibleDefaults()
    {
        var config = new AppConfig
        {
            SectorIconSize = 0, SectorFontSize = 0,
            HighlightGlowRadius = 0, HighlightGlowOpacity = -1
        };

        var (vm, _, _, _) = Create(config);

        Assert.Equal(20.0, vm.SectorIconSize);
        Assert.Equal(10.5, vm.SectorFontSize);
        Assert.Equal(24.0, vm.HighlightGlowRadius);
        Assert.Equal(85, vm.HighlightGlowOpacityPercent);
    }

    [Fact]
    public void Constructor_ZeroOpacityStaysZero_LikeLegacyFormula()
    {
        // 迁移前公式：config >= 0 时原值采用（0 即 0%），仅负哨兵值回落 0.85
        var (vm, _, _, _) = Create(new AppConfig { HighlightGlowOpacity = 0 });

        Assert.Equal(0, vm.HighlightGlowOpacityPercent);
    }

    [Theory]
    [InlineData("RoundedRect", "RoundedCapsule")]
    [InlineData("FloatingCapsules", "RoundedCapsule")]
    [InlineData("Capsule", "RoundedCapsule")]
    [InlineData("OrganicPetals", "Original")]
    [InlineData("ArcTracker", "Original")]
    [InlineData("MinimalArc", "Original")]
    [InlineData("Circle", "Circle")]
    public void Constructor_MapsLegacyShapeTagsForDisplay(string configShape, string expected)
    {
        var (vm, _, _, _) = Create(new AppConfig { Shape = configShape });

        Assert.Equal(expected, vm.Shape);
    }

    [Fact]
    public void Constructor_CustomPresetTheme_ShowsPresetButtonsAndExpandsPanel()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "我的配色" };
        var (vm, _, _, _) = Create(new AppConfig { Theme = "CustomPreset_p1", CustomColorPresets = new List<CustomColorPreset> { preset } });

        Assert.True(vm.IsCustomPresetSelected);
        Assert.True(vm.IsCustomColorExpanderExpanded);
        Assert.Same(preset, vm.SelectedCustomPreset);
    }

    // --- 立即生效：写穿配置 + 事件语义 -------------------------------------------------

    [Fact]
    public void SliderChange_WritesThroughConfig_RaisesLabelAndDebouncedAutoSave()
    {
        var (vm, config, _, log) = Create();

        vm.WheelRadius = 150;

        Assert.Equal(150, config.Current.WheelRadius);
        Assert.Equal("150", vm.WheelRadiusLabel);
        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.AutoSave);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void GlowOpacitySlider_StoresFractionInConfig()
    {
        var (vm, config, _, _) = Create();

        vm.HighlightGlowOpacityPercent = 40;

        Assert.Equal(0.4, config.Current.HighlightGlowOpacity, 5);
        Assert.Equal("40%", vm.HighlightGlowOpacityLabel);
    }

    [Fact]
    public void UiStyleChange_WritesConfigAndRaisesPreview_ButNeverRequestsSave()
    {
        var (vm, config, _, log) = Create();

        vm.UiStyle = "CleanSectors";

        Assert.Equal("CleanSectors", config.Current.UiStyle);
        Assert.Equal(1, log.Preview);
        Assert.Equal(0, log.AutoSave);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void ShapeChange_WritesConfigAndRequestsImmediateSave()
    {
        var (vm, config, _, log) = Create();

        vm.Shape = "HexagonHive";

        Assert.Equal("HexagonHive", config.Current.Shape);
        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.SaveNow);
    }

    // --- 高亮光晕 -------------------------------------------------------------------

    [Theory]
    [InlineData("Lilac", "#A855F7")]
    [InlineData("Blue", "#3B82F6")]
    [InlineData("Emerald", "#10B981")]
    [InlineData("Rose", "#EC4899")]
    [InlineData("Amber", "#F59E0B")]
    [InlineData("Red", "#EF4444")]
    [InlineData("White", "#FFFFFF")]
    public void GlowPresetNamedColor_SetsColorAndShowsCustomRow(string preset, string expectedHex)
    {
        var (vm, config, _, log) = Create();

        vm.HighlightGlowPreset = preset;

        Assert.Equal(preset, config.Current.HighlightGlowPreset);
        Assert.Equal(expectedHex, config.Current.HighlightGlowColor);
        Assert.Equal(expectedHex, vm.HighlightGlowColorText);
        Assert.True(vm.IsCustomGlowVisible);
        Assert.Equal(1, log.SaveNow);
    }

    [Fact]
    public void GlowPresetAuto_ClearsColorAndHidesCustomRow()
    {
        var (vm, config, _, _) = Create();
        vm.HighlightGlowPreset = "Blue"; // 先离开 Auto
        vm.HighlightGlowColorText = "#FF0000";

        vm.HighlightGlowPreset = "Auto";

        Assert.Equal("", config.Current.HighlightGlowColor);
        Assert.False(vm.IsCustomGlowVisible);
    }

    [Fact]
    public void GlowPresetCustom_KeepsExistingColorAndShowsRow()
    {
        var (vm, config, _, _) = Create();
        vm.HighlightGlowColorText = "#FF0000";

        vm.HighlightGlowPreset = "Custom";

        Assert.Equal("#FF0000", config.Current.HighlightGlowColor);
        Assert.True(vm.IsCustomGlowVisible);
    }

    [Fact]
    public void GlowColorTextChange_TrimsWritesConfigAndRequestsAutoSave()
    {
        var (vm, config, _, log) = Create();

        vm.HighlightGlowColorText = "  #00FF00  ";

        Assert.Equal("#00FF00", config.Current.HighlightGlowColor);
        Assert.Equal(1, log.AutoSave);
        Assert.Equal(0, log.SaveNow);
    }

    // --- 排版模式与文字显示联动 ---------------------------------------------------------

    [Fact]
    public void LayoutModeIconOnly_SetsShowTextFalse_InConfigAndProperty()
    {
        var (vm, config, _, log) = Create();

        vm.IconLayoutMode = "IconOnly";

        Assert.Equal("IconOnly", config.Current.IconLayoutMode);
        Assert.False(config.Current.ShowText);
        Assert.False(vm.ShowText);
        // 联动抑制：一次用户操作只发一轮事件
        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.SaveNow);
    }

    [Fact]
    public void LayoutModeTextOnly_SetsShowTextTrue()
    {
        var (vm, config, _, _) = Create();

        vm.IconLayoutMode = "TextOnly";

        Assert.True(config.Current.ShowText);
        Assert.True(vm.ShowText);
    }

    [Fact]
    public void ShowTextUnchecked_SwitchesLayoutModeToIconOnly()
    {
        var (vm, config, _, log) = Create();

        vm.ShowText = false;

        Assert.False(config.Current.ShowText);
        Assert.Equal("IconOnly", config.Current.IconLayoutMode);
        Assert.Equal("IconOnly", vm.IconLayoutMode);
        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.SaveNow);
    }

    [Fact]
    public void ShowTextCheckedFromIconOnly_RestoresIconAndText()
    {
        var (vm, config, _, _) = Create();
        vm.IconLayoutMode = "IconOnly";

        vm.ShowText = true;

        Assert.True(config.Current.ShowText);
        Assert.Equal("IconAndText", config.Current.IconLayoutMode);
        Assert.Equal("IconAndText", vm.IconLayoutMode);
    }

    // --- 自定义配色 -------------------------------------------------------------------

    [Fact]
    public void CustomColorTextChange_WritesTrimmedValue_UpdatesBrush_NoSaveRequest()
    {
        var (vm, config, _, log) = Create();

        vm.CustomSectorBgText = "  #123456  ";

        Assert.Equal("#123456", config.Current.CustomSectorBg);
        // 预览画刷由 View 层 HexToBrushConverter 转换，VM 不再暴露 Brush
        Assert.Equal(1, log.Preview);
        Assert.Equal(0, log.AutoSave);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void HexToBrushConverter_EmptyOrInvalid_IsTransparent()
    {
        // ParseColorBrush 随 WIP 移入 View 层转换器（ADR-0008）：空串/非法值一律透明
        var converter = new HexToBrushConverter();
        Assert.Equal(System.Windows.Media.Brushes.Transparent, converter.Convert("", typeof(Brush), null!, null!));
        Assert.Equal(System.Windows.Media.Brushes.Transparent, converter.Convert("not-a-color", typeof(Brush), null!, null!));
    }

    [Fact]
    public void PickColorCommand_PickedHex_AppliesToTargetField()
    {
        var (vm, config, dialogs, _) = Create();
        dialogs.ColorResult = new ColorPickResult("#ABCDEF");

        vm.PickColorCommand.Execute("CustomText");

        Assert.Equal(1, dialogs.ColorCalls);
        Assert.Equal("#E0FFFFFF", dialogs.LastColorPickerInitial); // 初始色为当前色值（AppConfig 默认 CustomText）
        Assert.Equal("#ABCDEF", config.Current.CustomText);
        Assert.Equal("#ABCDEF", vm.CustomTextText);
    }

    [Fact]
    public void PickColorCommand_UsesCurrentFieldValueAsInitial()
    {
        var (vm, _, dialogs, _) = Create();
        vm.HighlightGlowColorText = "#111111";
        dialogs.ColorResult = null;

        vm.PickColorCommand.Execute("HighlightGlowColor");

        Assert.Equal("#111111", dialogs.LastColorPickerInitial);
    }

    [Fact]
    public void PickColorCommand_Cancelled_KeepsCurrent()
    {
        var (vm, _, dialogs, _) = Create();
        vm.CustomTextText = "#000000";
        dialogs.ColorResult = null;

        vm.PickColorCommand.Execute("CustomText");

        Assert.Equal("#000000", vm.CustomTextText);
    }

    [Fact]
    public void PickEyedropperCommand_PickedHex_AppliesToTargetField()
    {
        var (vm, config, dialogs, _) = Create();
        dialogs.EyedropResult = new EyedropResult("#654321");

        vm.PickEyedropperCommand.Execute("CustomSectorBg");

        Assert.Equal(1, dialogs.EyedropCalls);
        Assert.Equal("#654321", config.Current.CustomSectorBg);
    }

    // --- 配色预设增删改 -----------------------------------------------------------------

    [Fact]
    public void SavePreset_CreatesFromCurrentColors_SelectsItAndSaves()
    {
        var (vm, config, dialogs, log) = Create();
        vm.CustomSectorBgText = "#111111";
        vm.CustomSectorBorderText = "#222222";
        vm.CustomHighlightBgText = "#333333";
        vm.CustomHighlightBorderText = "#444444";
        vm.CustomTextText = "#555555";
        dialogs.InputResult = new InputDialogResult("我的预设");

        vm.SavePresetCommand.Execute(null);

        Assert.Equal(1, dialogs.InputCalls);
        Assert.Equal("我的预设", config.Current.CustomColorPresets!.Single().Name);
        var preset = config.Current.CustomColorPresets!.Single();
        Assert.Equal("#111111", preset.SectorBg);
        Assert.Equal("#222222", preset.SectorBorder);
        Assert.Equal("#333333", preset.HighlightBg);
        Assert.Equal("#444444", preset.HighlightBorder);
        Assert.Equal("#555555", preset.TextColor);
        Assert.Equal("CustomPreset_" + preset.Id, config.Current.Theme);
        Assert.Equal("CustomPreset_" + preset.Id, vm.SelectedTheme);
        Assert.True(vm.IsCustomPresetSelected);
        Assert.True(vm.IsCustomColorExpanderExpanded);
        Assert.Equal(1, log.PresetList);
        Assert.Equal(1, log.SaveNow);
        // T19：保存成功提示经对话框服务(编排内聚进 VM)
        var info = Assert.Single(dialogs.Infos);
        Assert.Equal("提示", info.Title);
        Assert.Equal("配色预设【我的预设】已成功保存！", info.Message);
    }

    [Fact]
    public void SavePreset_Cancelled_DoesNothing()
    {
        var (vm, config, dialogs, log) = Create();
        dialogs.InputResult = null;

        vm.SavePresetCommand.Execute(null);

        Assert.Empty(config.Current.CustomColorPresets);
        Assert.Equal(0, log.PresetList);
        Assert.Equal(0, log.SaveNow);
        Assert.Empty(dialogs.Infos);
    }

    [Fact]
    public void SavePreset_SeededList_CreatesListAndAdds()
    {
        var (vm, config, dialogs, _) = Create();
        config.Current.CustomColorPresets = null!;
        dialogs.InputResult = new InputDialogResult("首次预设");

        vm.SavePresetCommand.Execute(null);

        Assert.Single(config.Current.CustomColorPresets!);
        Assert.Equal("首次预设", config.Current.CustomColorPresets![0].Name);
    }

    [Fact]
    public void RenamePreset_RenamesAndRebuildsList()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "旧名" };
        var (vm, config, dialogs, log) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });
        dialogs.InputResult = new InputDialogResult("新名");

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal("旧名", dialogs.LastInputDefaultText);
        Assert.NotNull(dialogs.LastValidator);
        Assert.Equal((false, "配色方案名称不能为空！"), dialogs.LastValidator!.Invoke(" "));
        Assert.Equal((true, ""), dialogs.LastValidator!.Invoke("新名"));
        Assert.Equal("新名", preset.Name);
        Assert.Equal("CustomPreset_p1", vm.SelectedTheme); // 选中保持不变
        Assert.Equal(1, log.PresetList);
        Assert.Equal(1, log.SaveNow);
    }

    [Fact]
    public void RenamePreset_NoCustomPresetSelected_DoesNotOpenDialog()
    {
        var (vm, _, dialogs, log) = Create();

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal(0, dialogs.InputCalls);
        Assert.Equal(0, log.PresetList);
    }

    [Fact]
    public void RenamePreset_Cancelled_KeepsName()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "旧名" };
        var (vm, _, dialogs, log) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });
        dialogs.InputResult = null;

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal("旧名", preset.Name);
        Assert.Equal(0, log.PresetList);
    }

    [Fact]
    public void DeletePreset_ConfirmsWithSelectedPresetName_AndDeletesOnConfirm()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "待删" };
        var (vm, _, dialogs, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });

        vm.DeletePresetCommand.Execute(null);

        // T19：删除确认对话框编排内聚进 VM——确认即删除并提示成功
        var confirm = Assert.Single(dialogs.Confirms);
        Assert.Equal("确认删除配色方案", confirm.Title);
        Assert.Contains("待删", confirm.Message);
        var info = Assert.Single(dialogs.Infos);
        Assert.Contains("待删", info.Message);
    }

    [Fact]
    public void DeletePreset_Cancelled_KeepsPreset()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "待删" };
        var (vm, config, dialogs, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });
        dialogs.ConfirmResult = false;

        vm.DeletePresetCommand.Execute(null);

        Assert.Single(config.Current.CustomColorPresets!);
        Assert.Empty(dialogs.Infos);
    }

    [Fact]
    public void DeletePreset_NoCustomPresetSelected_DoesNotRequestConfirm()
    {
        var (vm, _, dialogs, _) = Create();

        vm.DeletePresetCommand.Execute(null);

        Assert.Empty(dialogs.Confirms);
    }

    [Fact]
    public void ConfirmDelete_RemovesPreset_FallsBackToSystemAndRaises()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "待删" };
        var other = new CustomColorPreset { Id = "p2", Name = "保留" };
        var (vm, config, dialogs, log) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset, other }
        });

        vm.ConfirmDeleteCustomColorPreset(preset);

        Assert.Equal("System", config.Current.Theme);
        Assert.Equal("System", vm.SelectedTheme);
        Assert.False(vm.IsCustomPresetSelected);
        Assert.DoesNotContain(preset, config.Current.CustomColorPresets!);
        Assert.Contains(other, config.Current.CustomColorPresets!);
        // T19：删除成功提示经对话框服务
        var info = Assert.Single(dialogs.Infos);
        Assert.Equal("提示", info.Title);
        Assert.Contains("待删", info.Message);
        Assert.Equal(1, log.SaveNow);
        Assert.True(log.Preview > 0);
    }

    [Fact]
    public void ConfirmDelete_PresetNoLongerInList_IsNoOp()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "待删" };
        var (vm, config, dialogs, log) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });

        vm.ConfirmDeleteCustomColorPreset(new CustomColorPreset { Id = "other" });

        Assert.Single(config.Current.CustomColorPresets!);
        Assert.Equal(0, log.SaveNow);
        Assert.Empty(dialogs.Infos);
    }

    // --- 主题选择边界 -------------------------------------------------------------------

    [Fact]
    public void SelectedTheme_TransientNullOrEmpty_IsIgnored()
    {
        var (vm, config, _, log) = Create(new AppConfig { Theme = "Dark" });

        vm.SelectedTheme = null!;
        vm.SelectedTheme = "";

        Assert.Equal("Dark", config.Current.Theme);
        Assert.Equal("Dark", vm.SelectedTheme);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void SelectedTheme_StaticTheme_WritesConfig_KeepsExpanderState()
    {
        var (vm, config, _, log) = Create(new AppConfig { Theme = "CustomPreset_p1" });

        vm.SelectedTheme = "MatchaForest";

        Assert.Equal("MatchaForest", config.Current.Theme);
        Assert.False(vm.IsCustomPresetSelected);
        Assert.True(vm.IsCustomColorExpanderExpanded); // 迁移前不收起
        Assert.Equal(1, log.SaveNow);
    }

    [Fact]
    public void SelectedTheme_CustomPreset_LoadsPresetColorsIntoFields()
    {
        var preset = new CustomColorPreset
        {
            Id = "p1", SectorBg = "#AAAAAA", SectorBorder = "#BBBBBB",
            HighlightBg = "#CCCCCC", HighlightBorder = "#DDDDDD", TextColor = "#EEEEEE"
        };
        var (vm, config, _, _) = Create(new AppConfig { Theme = "Dark", CustomColorPresets = new List<CustomColorPreset> { preset } });

        vm.SelectedTheme = "CustomPreset_p1";

        Assert.Equal("#AAAAAA", vm.CustomSectorBgText);
        Assert.Equal("#BBBBBB", vm.CustomSectorBorderText);
        Assert.Equal("#CCCCCC", vm.CustomHighlightBgText);
        Assert.Equal("#DDDDDD", vm.CustomHighlightBorderText);
        Assert.Equal("#EEEEEE", vm.CustomTextText);
        Assert.Equal("#AAAAAA", config.Current.CustomSectorBg);
        Assert.True(vm.IsCustomColorExpanderExpanded);
    }

    // --- 一键重置 -------------------------------------------------------------------

    [Fact]
    public void ResetDimensions_AppliesRecommendedValues_SingleEventRound()
    {
        var (vm, config, _, log) = Create();
        vm.WheelRadius = 200;
        log.Preview = 0; log.AutoSave = 0; log.SaveNow = 0;

        vm.ResetDimensionsCommand.Execute(null);

        Assert.Equal(138, config.Current.WheelRadius);
        Assert.Equal(52, config.Current.InnerRadius);
        Assert.Equal(50, config.Current.CoreRadius);
        Assert.Equal(2, config.Current.SectorGap);
        Assert.Equal(4, config.Current.SectorCornerRadius);
        Assert.Equal(20.0, config.Current.SectorIconSize);
        Assert.Equal(10.5, config.Current.SectorFontSize);
        Assert.Equal("138", vm.WheelRadiusLabel);
        Assert.Equal("10.5 px", vm.SectorFontSizeLabel);
        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.SaveNow);
        Assert.Equal(0, log.AutoSave);
    }

    // --- 界面主题与中心核图标（T16 自窗口收编的透传属性） ---------------------------

    [Fact]
    public void PassThroughProperties_ReadThroughLiveConfig_WithLegacyFallbacks()
    {
        var config = new AppConfig
        {
            AppTheme = "Dark", ShowCoreIcon = true,
            CoreIconType = "Crosshair", CoreCustomIconKey = "Copy", CoreCustomImagePath = "C:\\i.png"
        };
        var (vm, _, _, _) = Create(config);

        Assert.Equal("Dark", vm.AppTheme);
        Assert.True(vm.ShowCoreIcon);
        Assert.Equal("Crosshair", vm.CoreIconType);
        Assert.Equal("Copy", vm.CoreCustomIconKey);
        Assert.Equal("C:\\i.png", vm.CoreCustomImagePath);

        // 空值回落与迁移前窗口读取点一致：主题 System、核图标 Exit、键与路径空串
        config.AppTheme = null!;
        config.CoreIconType = null!;
        config.CoreCustomIconKey = null!;
        config.CoreCustomImagePath = null!;
        Assert.Equal("System", vm.AppTheme);
        Assert.Equal("Exit", vm.CoreIconType);
        Assert.Equal("", vm.CoreCustomIconKey);
        Assert.Equal("", vm.CoreCustomImagePath);
    }

    [Fact]
    public void PassThroughProperties_WriteThroughToLiveConfig_AndRaiseLiveApplyPipelineEvents()
    {
        var (vm, config, _, log) = Create();

        int propertyNotifications = 0;
        vm.PropertyChanged += (_, _) => propertyNotifications++;

        vm.AppTheme = "MidnightNavy";
        vm.ShowCoreIcon = false;
        vm.CoreIconType = "Image";
        vm.CoreCustomIconKey = "custom:star";
        vm.CoreCustomImagePath = "  C:\\imgs\\core.png  ";

        Assert.Equal("MidnightNavy", config.Current.AppTheme);
        Assert.False(config.Current.ShowCoreIcon);
        Assert.Equal("Image", config.Current.CoreIconType);
        Assert.Equal("custom:star", config.Current.CoreCustomIconKey);
        Assert.Equal("C:\\imgs\\core.png", config.Current.CoreCustomImagePath);

        // T17：透传属性归队 Live-apply 管线——AppTheme/ShowCoreIcon 上报防抖落盘；
        // CoreIconType/CoreCustomImagePath 同时上报预览重绘；CoreCustomIconKey 保持纯通知
        //（其落盘由 PickCoreIcon 经 SaveNowRequested 驱动）。
        Assert.Equal(5, propertyNotifications);
        Assert.Equal(2, log.Preview);
        Assert.Equal(4, log.AutoSave);
        Assert.Equal(0, log.SaveNow);

        // 同值写入不再通知、不再触发管线
        vm.AppTheme = "MidnightNavy";
        Assert.Equal(5, propertyNotifications);
        Assert.Equal(4, log.AutoSave);
    }

    // --- 透传属性管线逐项语义 (T17) -------------------------------------------------

    [Fact]
    public void AppTheme_Setter_RaisesAutoSaveOnly()
    {
        var (vm, _, _, log) = Create();

        vm.AppTheme = "Dark";

        Assert.Equal(1, log.AutoSave);
        Assert.Equal(0, log.Preview);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void ShowCoreIcon_Setter_RaisesAutoSaveOnly()
    {
        var (vm, _, _, log) = Create();

        vm.ShowCoreIcon = false;

        Assert.Equal(1, log.AutoSave);
        Assert.Equal(0, log.Preview);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void CoreIconType_Setter_RaisesPreviewAndAutoSave()
    {
        var (vm, _, _, log) = Create();

        vm.CoreIconType = "Crosshair";

        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.AutoSave);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void CoreCustomImagePath_Setter_TrimsAndRaisesPreviewAndAutoSave()
    {
        var (vm, config, _, log) = Create();

        vm.CoreCustomImagePath = "  C:\\imgs\\core.png  ";

        Assert.Equal("C:\\imgs\\core.png", config.Current.CoreCustomImagePath);
        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.AutoSave);
        Assert.Equal(0, log.SaveNow);
    }

    [Fact]
    public void PickCoreIcon_Confirmed_RaisesSaveNow()
    {
        var (vm, config, dialogs, log) = Create();
        dialogs.IconResult = new IconPickResult("custom:star");

        vm.PickCoreIconCommand.Execute(null);

        Assert.Equal("custom:star", config.Current.CoreCustomIconKey);
        Assert.Equal(1, log.SaveNow);
        Assert.Equal(0, log.AutoSave);
    }

    [Fact]
    public void PickCoreIcon_Cancelled_ReturnsFalseAndKeepsConfig()
    {
        var (vm, config, dialogs, _) = Create();
        config.Current.CoreCustomIconKey = "Copy";
        dialogs.IconResult = null;

        vm.PickCoreIconCommand.Execute(null);

        Assert.Equal(1, dialogs.IconCalls);
        Assert.Equal("Copy", dialogs.LastIconPickerCurrentKey);
        Assert.Equal("Copy", config.Current.CoreCustomIconKey);
    }

    [Fact]
    public void PickCoreIcon_Confirmed_WritesIconKeyAndReturnsTrue()
    {
        var (vm, config, dialogs, _) = Create();
        dialogs.IconResult = new IconPickResult("custom:star");

        vm.PickCoreIconCommand.Execute(null);

        Assert.Equal("custom:star", config.Current.CoreCustomIconKey);
    }

    [Fact]
    public void PickCoreIcon_ClearSelection_WritesEmptyKey()
    {
        var (vm, config, dialogs, _) = Create();
        config.Current.CoreCustomIconKey = "Copy";
        dialogs.IconResult = new IconPickResult(null);

        vm.PickCoreIconCommand.Execute(null);

        Assert.Equal("", config.Current.CoreCustomIconKey);
    }
}
