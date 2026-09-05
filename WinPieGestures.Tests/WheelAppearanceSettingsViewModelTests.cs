using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures;
using WinPieGestures.Views.Converters;
using Brush = System.Windows.Media.Brush;

namespace WinPieGestures.Tests;

/// <summary>
/// 轮盘模块外观设置子 ViewModel 的行为覆盖（#56/ADR-0014 决策 6/8，自外观聚合 VM 测试迁移）：
/// 构造播种（含默认值回落与旧版 Shape 标签映射）、立即生效写穿 IConfigService、防抖/立即落盘
/// 事件语义、ShowText 与排版模式联动、配色预设增删改编排（mock 对话框服务；#53 名称语义）、
/// 实时预览失效事件（含 #56 起 ShowCoreIcon 预览消息）、切语重建与导入重挂、Dispose 退订、
/// 以及只读状态接口 <see cref="IWheelAppearanceState"/> 的预览取值面。
/// </summary>
public sealed class WheelAppearanceSettingsViewModelTests
{
    private static readonly LocalizationService Localization = new();

    /// <summary>
    /// 事件/消息计数器 (T19)：视图事件（预览/预设列表）照旧订阅；落盘请求转发到
    /// <see cref="SaveSpy"/> 消息计数（可赋值以支持用例中途清零）；删除确认与保存提示
    /// 经对话框替身的记录断言（T19 对话框编排内聚进 VM）。
    /// </summary>
    private sealed class EventLog
    {
        private readonly SaveSpy _spy;
        public int Preview;
        public int AutoSave { get => _spy.Debounced; set => _spy.Debounced = value; }
        public int SaveNow { get => _spy.Immediate; set => _spy.Immediate = value; }

        private EventLog(SaveSpy spy) => _spy = spy;

        public static EventLog Attach(WeakReferenceMessenger messenger, SaveSpy spy)
        {
            var log = new EventLog(spy);
            messenger.Register<AppearancePreviewInvalidatedMessage>(log, (_, _) => log.Preview++);
            return log;
        }
    }

    /// <summary>只实现 M1 只读预览 Profile 来源接口的测试替身（#69）：用于编译期钉住外观设置子
    /// VM 的构造签名依赖接口而非具体 <c>ProfileListViewModel</c>（若签名回退为具体类型，本类将
    /// 无法传入），兼作转发行为取值源。</summary>
    private sealed class FakeProfilePreviewSource : IProfilePreviewSource
    {
        public WheelProfile? PreviewProfile { get; set; }
    }

    private static (WheelAppearanceSettingsViewModel Vm, TestConfigService Config, TestDialogService Dialogs, EventLog Log)
        Create(AppConfig? config = null, ILocalizationService? localization = null)
    {
        var configService = new TestConfigService { Current = config ?? new AppConfig() };
        var dialogs = new TestDialogService();
        var (messenger, spy) = SaveSpy.Create();
        var loc = localization ?? Localization;
        var profileList = new ProfileListViewModel(configService.Current.Profiles, dialogs, messenger, new TestActionExecutor(), loc);
        var vm = new WheelAppearanceSettingsViewModel(configService, dialogs, messenger, profileList, loc);
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
        dialogs.ColorToPick = new ColorPickResult("#ABCDEF");

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
        dialogs.ColorToPick = null;

        vm.PickColorCommand.Execute("HighlightGlowColor");

        Assert.Equal("#111111", dialogs.LastColorPickerInitial);
    }

    [Fact]
    public void PickColorCommand_Cancelled_KeepsCurrent()
    {
        var (vm, _, dialogs, _) = Create();
        vm.CustomTextText = "#000000";
        dialogs.ColorToPick = null;

        vm.PickColorCommand.Execute("CustomText");

        Assert.Equal("#000000", vm.CustomTextText);
    }

    [Fact]
    public void PickEyedropperCommand_PickedHex_AppliesToTargetField()
    {
        var (vm, config, dialogs, _) = Create();
        dialogs.EyedropToPick = new EyedropResult("#654321");

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
        dialogs.InputToPick = new InputDialogResult("我的预设");

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
        // T21：预设下拉项 ItemsSource 化——保存后 VM 重建 ThemeOptions（6 固定 + 1 自定义）。
        Assert.Equal(7, vm.ThemeOptions.Count);
        Assert.Contains(vm.ThemeOptions, o => o.Tag == "CustomPreset_" + preset.Id && o.Label.Contains("我的预设"));
        Assert.Equal(1, log.SaveNow);
        // T19/#53：保存成功提示经对话框服务(编排内聚进 VM)，文案即时取词键化
        var info = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(Localization.GetString("Notice"), info.Title);
        Assert.Equal(string.Format(Localization.GetString("SaveCustomPresetSuccess"), "我的预设"), info.Message);
    }

    [Fact]
    public void SavePreset_Cancelled_DoesNothing()
    {
        var (vm, config, dialogs, log) = Create();
        dialogs.InputToPick = null;

        vm.SavePresetCommand.Execute(null);

        Assert.Empty(config.Current.CustomColorPresets);
        Assert.Equal(6, vm.ThemeOptions.Count); // 取消不新增自定义项
        Assert.Equal(0, log.SaveNow);
        Assert.Empty(dialogs.InfoCalls);
    }

    [Fact]
    public void SavePreset_SeededList_CreatesListAndAdds()
    {
        var (vm, config, dialogs, _) = Create();
        config.Current.CustomColorPresets = null!;
        dialogs.InputToPick = new InputDialogResult("首次预设");

        vm.SavePresetCommand.Execute(null);

        Assert.Single(config.Current.CustomColorPresets!);
        Assert.Equal("首次预设", config.Current.CustomColorPresets![0].Name);
    }

    [Fact]
    public void SavePreset_DialogCopyAndDefaultName_AreLocalizedTemplatePlusTimestamp()
    {
        var (vm, _, dialogs, _) = Create();
        dialogs.InputToPick = null; // 取消一次即可捕获对话框实参

        vm.SavePresetCommand.Execute(null);

        Assert.Equal(Localization.GetString("SaveCustomPresetTitle"), dialogs.LastInputTitle);
        Assert.Equal(Localization.GetString("SaveCustomPresetPrompt"), dialogs.LastInputPrompt);
        // 默认名建议 = 本地化模板 + 时间戳（模板 {0} 被 MMdd-HHmm 替换）
        Assert.NotNull(dialogs.LastInputDefaultText);
        Assert.StartsWith(Localization.GetString("CustomPresetDefaultName").Replace("{0}", ""), dialogs.LastInputDefaultText!);
        Assert.Matches(@"\d{4}-\d{4}$", dialogs.LastInputDefaultText!);
    }

    [Fact]
    public void SavePreset_TrimsNameBeforeStoring()
    {
        var (vm, config, dialogs, _) = Create();
        dialogs.InputToPick = new InputDialogResult("  我的预设  ");

        vm.SavePresetCommand.Execute(null);

        Assert.Equal("我的预设", config.Current.CustomColorPresets!.Single().Name);
        Assert.Contains(vm.ThemeOptions, o => o.Label.Contains("我的预设"));
    }

    [Fact]
    public void SavePreset_TrimmedEmptyResult_IsRejectedWithoutCreatingPreset()
    {
        var (vm, config, dialogs, log) = Create();
        dialogs.InputToPick = new InputDialogResult("   ");

        vm.SavePresetCommand.Execute(null);

        Assert.Empty(config.Current.CustomColorPresets);
        Assert.Equal(6, vm.ThemeOptions.Count); // 空名拒绝：不新增自定义项
        Assert.Equal(0, log.SaveNow);
        var info = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(Localization.GetString("Notice"), info.Title);
        Assert.Equal(Localization.GetString("CustomPresetNameEmpty"), info.Message);
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
        dialogs.InputToPick = new InputDialogResult("新名");

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal("旧名", dialogs.LastInputDefaultText);
        Assert.Equal("新名", preset.Name);
        Assert.Equal("CustomPreset_p1", vm.SelectedTheme); // 选中保持不变
        // T21：预设下拉项 ItemsSource 化——改名后 VM 重建 ThemeOptions 刷新标签。
        Assert.Contains(vm.ThemeOptions, o => o.Tag == "CustomPreset_p1" && o.Label.Contains("新名"));
        Assert.Equal(1, log.SaveNow);
    }

    [Fact]
    public void RenamePreset_DialogCopy_IsKeyedWithCurrentName()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "旧名" };
        var (vm, _, dialogs, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });
        dialogs.InputToPick = null; // 取消一次即可捕获对话框实参

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal(Localization.GetString("RenameCustomPresetTitle"), dialogs.LastInputTitle);
        Assert.Equal(string.Format(Localization.GetString("RenameCustomPresetPrompt"), "旧名"), dialogs.LastInputPrompt);
        Assert.Equal("旧名", dialogs.LastInputDefaultText);
    }

    [Fact]
    public void RenamePreset_TrimsNameBeforeStoring()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "旧名" };
        var (vm, _, dialogs, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });
        dialogs.InputToPick = new InputDialogResult("  新名  ");

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal("新名", preset.Name);
        Assert.Contains(vm.ThemeOptions, o => o.Tag == "CustomPreset_p1" && o.Label.Contains("新名"));
    }

    [Fact]
    public void RenamePreset_TrimmedEmptyResult_IsRejectedAndKeepsName()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "旧名" };
        var (vm, _, dialogs, log) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });
        dialogs.InputToPick = new InputDialogResult("   ");

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal("旧名", preset.Name);
        Assert.Equal(0, log.SaveNow);
        // 空名拒绝：不改名不重建下拉，错误信息键化
        Assert.Contains(vm.ThemeOptions, o => o.Tag == "CustomPreset_p1" && o.Label.Contains("旧名"));
        var info = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(Localization.GetString("Notice"), info.Title);
        Assert.Equal(Localization.GetString("CustomPresetNameEmpty"), info.Message);
    }

    [Fact]
    public void RenamePreset_NoCustomPresetSelected_DoesNotOpenDialog()
    {
        var (vm, _, dialogs, log) = Create();

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal(0, dialogs.InputCalls);
        Assert.Equal(6, vm.ThemeOptions.Count); // 未选中预设不重建不新增
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
        dialogs.InputToPick = null;

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal("旧名", preset.Name);
        // T21：取消改名不动下拉项。
        Assert.Contains(vm.ThemeOptions, o => o.Tag == "CustomPreset_p1" && o.Label.Contains("旧名"));
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
        dialogs.ConfirmResult = true;

        vm.DeletePresetCommand.Execute(null);

        // T19/#53：删除确认对话框编排内聚进 VM——确认即删除并提示成功，文案键化
        var confirm = Assert.Single(dialogs.ConfirmCalls);
        Assert.Equal(Localization.GetString("DeleteCustomPresetTitle"), confirm.Title);
        Assert.Equal(string.Format(Localization.GetString("MsgConfirmDeletePreset"), "待删"), confirm.Message);
        var info = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(Localization.GetString("Notice"), info.Title);
        Assert.Equal(string.Format(Localization.GetString("DeleteCustomPresetSuccess"), "待删"), info.Message);
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
        Assert.Empty(dialogs.InfoCalls);
    }

    [Fact]
    public void DeletePreset_NoCustomPresetSelected_DoesNotRequestConfirm()
    {
        var (vm, _, dialogs, _) = Create();

        vm.DeletePresetCommand.Execute(null);

        Assert.Empty(dialogs.ConfirmCalls);
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
        // T19/#53：删除成功提示经对话框服务，文案键化
        var info = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(Localization.GetString("Notice"), info.Title);
        Assert.Equal(string.Format(Localization.GetString("DeleteCustomPresetSuccess"), "待删"), info.Message);
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
        Assert.Empty(dialogs.InfoCalls);
    }

    // --- 预设对话框文案语言切换（#53） ----------------------------------------------
    // 对话框/成功提示属即时取词：每次命令执行读当前语言；预设名落库后是用户数据，
    // 切语只重建下拉后缀（WheelThemeCustomPreset），名称永不翻译。

    [Fact]
    public void SavePreset_UsesDialogCopyAndDefaultNameOfCurrentLanguage()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("en");
        var (vm, _, dialogs, _) = Create(localization: loc);
        dialogs.InputToPick = new InputDialogResult("My Preset");

        vm.SavePresetCommand.Execute(null);

        Assert.Equal(loc.GetString("SaveCustomPresetTitle"), dialogs.LastInputTitle);
        Assert.Equal(loc.GetString("SaveCustomPresetPrompt"), dialogs.LastInputPrompt);
        Assert.NotNull(dialogs.LastInputDefaultText);
        Assert.StartsWith(loc.GetString("CustomPresetDefaultName").Replace("{0}", ""), dialogs.LastInputDefaultText!);
        Assert.Matches(@"\d{4}-\d{4}$", dialogs.LastInputDefaultText!);
        var info = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(loc.GetString("Notice"), info.Title);
        Assert.Equal(string.Format(loc.GetString("SaveCustomPresetSuccess"), "My Preset"), info.Message);
    }

    [Fact]
    public void SavedPresetName_IsUserData_NotTranslatedOnLanguageSwitch()
    {
        var loc = new LocalizationService();
        var (vm, config, dialogs, _) = Create(localization: loc);

        // 先取消一次取到当前语言（zh-CN）默认名，再以该默认名保存，模拟用户直接确认默认建议
        vm.SavePresetCommand.Execute(null);
        string? defaultName = dialogs.LastInputDefaultText;
        dialogs.InputToPick = new InputDialogResult(defaultName!);
        vm.SavePresetCommand.Execute(null);

        string savedName = config.Current.CustomColorPresets!.Single().Name;
        Assert.Equal(defaultName, savedName);

        loc.SetLanguage("en");

        // 名称保持用户数据原样；下拉标签仅后缀随语言切换（模板 WheelThemeCustomPreset）
        Assert.Equal(savedName, config.Current.CustomColorPresets!.Single().Name);
        string label = Assert.Single(vm.ThemeOptions, o => o.Tag == "CustomPreset_" + config.Current.CustomColorPresets[0].Id).Label;
        Assert.Contains(savedName, label);
        Assert.Equal(string.Format(loc.GetString("WheelThemeCustomPreset"), savedName), label);
    }

    [Fact]
    public void RenamePreset_UsesDialogCopyOfCurrentLanguage()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("en");
        var preset = new CustomColorPreset { Id = "p1", Name = "My Preset" };
        var (vm, _, dialogs, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        }, loc);
        dialogs.InputToPick = null; // 取消一次即可捕获对话框实参

        vm.RenamePresetCommand.Execute(null);

        Assert.Equal(loc.GetString("RenameCustomPresetTitle"), dialogs.LastInputTitle);
        Assert.Equal(string.Format(loc.GetString("RenameCustomPresetPrompt"), "My Preset"), dialogs.LastInputPrompt);
        Assert.Equal("My Preset", dialogs.LastInputDefaultText);
    }

    [Fact]
    public void DeletePreset_UsesConfirmAndSuccessCopyOfCurrentLanguage()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("en");
        var preset = new CustomColorPreset { Id = "p1", Name = "My Preset" };
        var (vm, _, dialogs, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        }, loc);
        dialogs.ConfirmResult = true;

        vm.DeletePresetCommand.Execute(null);

        var confirm = Assert.Single(dialogs.ConfirmCalls);
        Assert.Equal(loc.GetString("DeleteCustomPresetTitle"), confirm.Title);
        Assert.Equal(string.Format(loc.GetString("MsgConfirmDeletePreset"), "My Preset"), confirm.Message);
        var info = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(loc.GetString("Notice"), info.Title);
        Assert.Equal(string.Format(loc.GetString("DeleteCustomPresetSuccess"), "My Preset"), info.Message);
    }

    [Fact]
    public void ThemeOptions_Constructor_SeedsStaticAndCustomPresetsInOrder()
    {
        var preset = new CustomColorPreset { Id = "p1", Name = "方案一" };
        var (vm, _, _, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { preset }
        });

        // T21：配色下拉 ItemsSource 化——固定 6 项在前、自定义预设按配置顺序追加。
        Assert.Equal(7, vm.ThemeOptions.Count);
        Assert.Equal("System", vm.ThemeOptions[0].Tag);
        Assert.Equal("MorandiMuted", vm.ThemeOptions[5].Tag);
        Assert.Equal("CustomPreset_p1", vm.ThemeOptions[6].Tag);
        Assert.Contains("方案一", vm.ThemeOptions[6].Label);
    }

    [Fact]
    public void ReloadFromConfig_RebuildsThemeOptionsFromImportedConfig()
    {
        var oldPreset = new CustomColorPreset { Id = "p1", Name = "旧预设" };
        var (vm, config, _, _) = Create(new AppConfig
        {
            Theme = "CustomPreset_p1",
            CustomColorPresets = new List<CustomColorPreset> { oldPreset }
        });
        Assert.Equal(7, vm.ThemeOptions.Count);

        var imported = new AppConfig
        {
            Theme = "System",
            CustomColorPresets = new List<CustomColorPreset> { new CustomColorPreset { Id = "p2", Name = "导入预设" } }
        };
        config.Current = imported;
        string? selectedNotified = null;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SelectedTheme)) selectedNotified = e.PropertyName; };
        vm.ReloadFromConfig();

        Assert.Equal("System", vm.SelectedTheme);
        // T21：重建下拉后补发选中通知，保证 ComboBox 能从新 ThemeOptions 恢复选中。
        Assert.Equal(nameof(vm.SelectedTheme), selectedNotified);
        Assert.Equal(7, vm.ThemeOptions.Count);
        Assert.DoesNotContain(vm.ThemeOptions, o => o.Tag == "CustomPreset_p1");
        Assert.Contains(vm.ThemeOptions, o => o.Tag == "CustomPreset_p2" && o.Label.Contains("导入预设"));
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

    // --- 中心核图标透传属性（T16 自窗口收编；界面主题 AppTheme 已随 #54 迁入
    //     InterfaceThemeSettingsViewModelTests） -------------------------------------

    [Fact]
    public void PassThroughProperties_ReadThroughLiveConfig_WithLegacyFallbacks()
    {
        var config = new AppConfig
        {
            ShowCoreIcon = true,
            CoreIconType = "Crosshair", CoreCustomIconKey = "Copy", CoreCustomImagePath = "C:\\i.png"
        };
        var (vm, _, _, _) = Create(config);

        Assert.True(vm.ShowCoreIcon);
        Assert.Equal("Crosshair", vm.CoreIconType);
        Assert.Equal("Copy", vm.CoreCustomIconKey);
        Assert.Equal("C:\\i.png", vm.CoreCustomImagePath);

        // 空值回落与迁移前窗口读取点一致：核图标 Exit、键与路径空串
        config.CoreIconType = null!;
        config.CoreCustomIconKey = null!;
        config.CoreCustomImagePath = null!;
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

        vm.ShowCoreIcon = false;
        vm.CoreIconType = "Image";
        vm.CoreCustomIconKey = "custom:star";
        vm.CoreCustomImagePath = "  C:\\imgs\\core.png  ";

        Assert.False(config.Current.ShowCoreIcon);
        Assert.Equal("Image", config.Current.CoreIconType);
        Assert.Equal("custom:star", config.Current.CoreCustomIconKey);
        Assert.Equal("C:\\imgs\\core.png", config.Current.CoreCustomImagePath);

        // T17/#56：透传属性归队 Live-apply 管线——ShowCoreIcon 上报防抖落盘并（#56 起）上报预览
        // 重绘（页面 checkbox 事件已删除，重绘全部经消息管线）；CoreIconType/CoreCustomImagePath
        // 同时上报预览重绘；CoreCustomIconKey 保持纯通知（其落盘由 PickCoreIcon 经
        // SaveNowRequested 驱动）。
        Assert.Equal(4, propertyNotifications);
        Assert.Equal(3, log.Preview);
        Assert.Equal(3, log.AutoSave);
        Assert.Equal(0, log.SaveNow);

        // 同值写入不再通知、不再触发管线
        vm.CoreCustomImagePath = "C:\\imgs\\core.png";
        Assert.Equal(4, propertyNotifications);
        Assert.Equal(3, log.AutoSave);
    }

    // --- 透传属性管线逐项语义 (T17) -------------------------------------------------

    [Fact]
    public void ShowCoreIcon_Setter_RaisesPreviewAndAutoSave()
    {
        var (vm, _, _, log) = Create();

        vm.ShowCoreIcon = false;

        // #56：ShowCoreIcon 变更经 AppearancePreviewInvalidatedMessage 触发页面预览重绘（页面
        // Checked/Unchecked 事件处理器删除），落盘仍走防抖请求。
        Assert.Equal(1, log.Preview);
        Assert.Equal(1, log.AutoSave);
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
        dialogs.IconToPick = new IconPickResult("custom:star");

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
        dialogs.IconToPick = null;

        vm.PickCoreIconCommand.Execute(null);

        Assert.Equal(1, dialogs.IconCalls);
        Assert.Equal("Copy", dialogs.LastIconPickerCurrentKey);
        Assert.Equal("Copy", config.Current.CoreCustomIconKey);
    }

    [Fact]
    public void PickCoreIcon_Confirmed_WritesIconKeyAndReturnsTrue()
    {
        var (vm, config, dialogs, _) = Create();
        dialogs.IconToPick = new IconPickResult("custom:star");

        vm.PickCoreIconCommand.Execute(null);

        Assert.Equal("custom:star", config.Current.CoreCustomIconKey);
    }

    [Fact]
    public void PickCoreIcon_ClearSelection_WritesEmptyKey()
    {
        var (vm, config, dialogs, _) = Create();
        config.Current.CoreCustomIconKey = "Copy";
        dialogs.IconToPick = new IconPickResult(null);

        vm.PickCoreIconCommand.Execute(null);

        Assert.Equal("", config.Current.CoreCustomIconKey);
    }

    // --- 预览输入接口（#55/#56 IWheelAppearanceState：轮盘外观设置子 VM 实现方；#69 起预览
    // Profile 上下文经 M1 只读 IProfilePreviewSource 注入转发） ------------------------------

    [Fact]
    public void WheelAppearanceSettingsViewModel_ImplementsIWheelAppearanceState_ReadsThroughSamePreviewSurface()
    {
        var profile = new WheelProfile { ProcessName = "chrome.exe", SectorCount = 8 };
        var config = new AppConfig
        {
            UiStyle = "Glassmorphism", Theme = "MatchaForest", Shape = "Circle",
            WheelRadius = 160, InnerRadius = 60, CoreRadius = 55, SectorGap = 3, SectorCornerRadius = 6,
            IconLayoutMode = "TextOnly", ShowText = true, SectorIconSize = 28, SectorFontSize = 12.5,
            ShowCoreIcon = false, CoreIconType = "Crosshair",
            CoreCustomIconKey = "custom:star", CoreCustomIconSvg = "M0,0L1,1", CoreCustomImagePath = "C:\\core.png",
            Profiles = new List<WheelProfile> { profile }
        };
        var (vm, configService, _, _) = Create(config);

        IWheelAppearanceState state = vm;

        Assert.Equal("Glassmorphism", state.UiStyle);
        Assert.Equal("MatchaForest", state.SelectedTheme);
        Assert.Equal("Circle", state.Shape);
        Assert.Equal(160, state.WheelRadius);
        Assert.Equal(60, state.InnerRadius);
        Assert.Equal(55, state.CoreRadius);
        Assert.Equal(3, state.SectorGap);
        Assert.Equal(6, state.SectorCornerRadius);
        Assert.Equal("TextOnly", state.IconLayoutMode);
        Assert.True(state.ShowText);
        Assert.Equal(28, state.SectorIconSize);
        Assert.Equal(12.5, state.SectorFontSize);
        Assert.False(state.ShowCoreIcon);
        Assert.Equal("Crosshair", state.CoreIconType);
        Assert.Equal("custom:star", state.CoreCustomIconKey);
        Assert.Equal("M0,0L1,1", state.CoreCustomIconSvg);
        Assert.Equal("C:\\core.png", state.CoreCustomImagePath);
        Assert.Same(configService.Current, state.CurrentConfig);
        // 预览 Profile 上下文：构造后默认选中首项；无选中回落与迁移前渲染器取值链一致。
        Assert.Same(profile, state.PreviewProfile);
    }

    [Fact]
    public void Constructor_AcceptsIProfilePreviewSource_NotConcreteProfileListVm_AndForwardsToPreviewState()
    {
        // #69（B2）：外观设置子 VM 的预览 Profile 上下文来源改经 M1 只读接口构造注入——若签名回退
        // 为具体 ProfileListViewModel（或同族具体类型），仅实现接口的替身将无法编译（同
        // WheelPreviewRendererTests 方法组钉签名的编译期验证思路）。
        var profile = new WheelProfile { ProcessName = "chrome.exe", SectorCount = 8 };
        var configService = new TestConfigService { Current = new AppConfig() };
        var dialogs = new TestDialogService();
        var (messenger, _) = SaveSpy.Create();
        var source = new FakeProfilePreviewSource { PreviewProfile = profile };

        var vm = new WheelAppearanceSettingsViewModel(configService, dialogs, messenger, source, Localization);
        IWheelAppearanceState state = vm;

        Assert.Same(profile, state.PreviewProfile);

        // 来源（M1 实现方选中态）变化即时反映到预览状态面
        var other = new WheelProfile { ProcessName = "b.exe", SectorCount = 4 };
        source.PreviewProfile = other;
        Assert.Same(other, state.PreviewProfile);
    }

    [Fact]
    public void PreviewProfile_FollowsProfileListSelection()
    {
        var first = new WheelProfile { ProcessName = "a.exe", SectorCount = 4 };
        var second = new WheelProfile { ProcessName = "b.exe", SectorCount = 12 };
        var configService = new TestConfigService
        {
            Current = new AppConfig { Profiles = new List<WheelProfile> { first, second } }
        };
        var dialogs = new TestDialogService();
        var (messenger, _) = SaveSpy.Create();
        // 以真实方案列表 VM（M1 实现方）作接口来源注入——镜像 Composition 装配（IProfilePreviewSource 别名）。
        var profileList = new ProfileListViewModel(
            configService.Current.Profiles, dialogs, messenger, new TestActionExecutor(), Localization);
        var vm = new WheelAppearanceSettingsViewModel(configService, dialogs, messenger, profileList, Localization);
        IWheelAppearanceState state = vm;

        Assert.Same(first, state.PreviewProfile);

        profileList.SelectProfile(profileList.Profiles[1]);

        Assert.Same(second, state.PreviewProfile);
    }

    [Fact]
    public void PreviewProfile_NoProfiles_IsNull()
    {
        var (vm, _, _, _) = Create(new AppConfig());
        IWheelAppearanceState state = vm;

        Assert.Null(state.PreviewProfile);
    }
}
