using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using TextBox = System.Windows.Controls.TextBox;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;

namespace WinPieGestures
{
    public partial class SettingsWindow : Window
    {
        private readonly IThemeService _themeService;
        private readonly Action _exitApplication;
        private readonly Action<string, string> _showTrayBalloonTip;
        private readonly IDialogService _dialogs;
        // 配置方案分区列表侧 ViewModel (T11)：方案列表/选中态、扇区数、方向槽位集合。
        private readonly ProfileListViewModel _profileList;

        // Re-entrancy guards (Initial state is true to prevent XAML initialization from overwriting saved config)
        private bool _isUpdatingUi = true;
        private bool _isRenderingPreview = false;

        // Preview rendering tracking
        private readonly List<System.Windows.Shapes.Path> _previewSectorPaths = new List<System.Windows.Shapes.Path>();
        private readonly List<TranslateTransform> _previewTransforms = new List<TranslateTransform>();
        private readonly List<double> _previewAngles = new List<double>();
        private IRadialStyleRenderer? _previewStyleRenderer;
        private Brush? _previewDefaultBrush;
        private Brush? _previewHighlightBrush;
        private Brush? _previewBorderBrush;
        private Brush? _previewHighlightBorderBrush;
        private Brush? _previewTextBrush;
        private Brush? _previewCoreBgBrush;
        private Brush? _previewCoreBorderBrush;
        private Ellipse? _previewCoreCircle;
        private System.Windows.Shapes.Path? _previewExitIcon;
        private int _lastHoveredSector = -2;

        public SettingsWindow(IThemeService themeService, IDialogService dialogs, Action exitApplication, Action<string, string> showTrayBalloonTip)
        {
            InitializeComponent();
            _themeService = themeService;
            _exitApplication = exitApplication;
            _showTrayBalloonTip = showTrayBalloonTip;
            _dialogs = dialogs;

            _isUpdatingUi = true;
            try
            {
                // Load profiles to listbox
                _profileList = new ProfileListViewModel(ConfigManager.CurrentConfig.Profiles, dialogs);
                // 槽位动作编辑提交（T12 文件夹选择写回）后同步 UI 状态并落盘——对应迁移前 BrowseFolder_Click 的调用点。
                _profileList.SlotEditCommitted += () => SyncUiToConfigAndSave(true);
                ProfilesListBox.ItemsSource = _profileList.Profiles;
                ThresholdSlider.Value = ConfigManager.CurrentConfig.DragThreshold;
                ThresholdValueLabel.Text = ConfigManager.CurrentConfig.DragThreshold.ToString("0");

                // Load App Interface Theme
                SetComboBoxSelectedValue(AppThemeComboBox, ConfigManager.CurrentConfig.AppTheme ?? "System");
                _themeService.ApplyTheme(this, ConfigManager.CurrentConfig.AppTheme ?? "System");

                // Load theme & style settings
                ReloadThemePresets();
                SetComboBoxSelectedValue(ThemeComboBox, ConfigManager.CurrentConfig.Theme);
                SetComboBoxSelectedValue(UiStyleComboBox, ConfigManager.CurrentConfig.UiStyle);

                CustomSectorBgTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBg;
                CustomSectorBorderTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBorder;
                CustomHighlightBgTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBg;
                CustomHighlightBorderTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBorder;
                CustomTextTextBox.Text = ConfigManager.CurrentConfig.CustomText;

                bool isCustomPreset = (ConfigManager.CurrentConfig.Theme ?? "").StartsWith("CustomPreset_");
                if (CustomColorsPanel != null) CustomColorsPanel.Visibility = Visibility.Visible;
                if ((ConfigManager.CurrentConfig.Theme == "Custom" || isCustomPreset) && CustomColorExpander != null)
                {
                    CustomColorExpander.IsExpanded = true;
                }
                if (RenameCustomColorPresetButton != null) RenameCustomColorPresetButton.Visibility = isCustomPreset ? Visibility.Visible : Visibility.Collapsed;
                if (DeletePresetInPanelButton != null) DeletePresetInPanelButton.Visibility = isCustomPreset ? Visibility.Visible : Visibility.Collapsed;

                // Load Highlight Glow settings
                SetComboBoxSelectedValue(HighlightGlowPresetComboBox, ConfigManager.CurrentConfig.HighlightGlowPreset ?? "Auto");
                HighlightGlowColorTextBox.Text = ConfigManager.CurrentConfig.HighlightGlowColor ?? "";
                HighlightGlowRadiusSlider.Value = ConfigManager.CurrentConfig.HighlightGlowRadius > 0 ? ConfigManager.CurrentConfig.HighlightGlowRadius : 24.0;
                HighlightGlowRadiusLabel.Text = $"{HighlightGlowRadiusSlider.Value:0} px";
                HighlightGlowOpacitySlider.Value = (ConfigManager.CurrentConfig.HighlightGlowOpacity >= 0 ? ConfigManager.CurrentConfig.HighlightGlowOpacity : 0.85) * 100.0;
                HighlightGlowOpacityLabel.Text = $"{HighlightGlowOpacitySlider.Value:0}%";
                CustomHighlightGlowPanel.Visibility = (ConfigManager.CurrentConfig.HighlightGlowPreset == "Custom" || !string.IsNullOrEmpty(ConfigManager.CurrentConfig.HighlightGlowColor)) ? Visibility.Visible : Visibility.Collapsed;

                // Load sliders & shape settings
                WheelRadiusSlider.Value = ConfigManager.CurrentConfig.WheelRadius;
                WheelRadiusLabel.Text = ConfigManager.CurrentConfig.WheelRadius.ToString("0");
                InnerRadiusSlider.Value = ConfigManager.CurrentConfig.InnerRadius;
                InnerRadiusLabel.Text = ConfigManager.CurrentConfig.InnerRadius.ToString("0");
                CoreRadiusSlider.Value = ConfigManager.CurrentConfig.CoreRadius;
                CoreRadiusLabel.Text = ConfigManager.CurrentConfig.CoreRadius.ToString("0");

                SectorGapSlider.Value = ConfigManager.CurrentConfig.SectorGap;
                SectorGapLabel.Text = $"{ConfigManager.CurrentConfig.SectorGap:0} px";
                SectorCornerRadiusSlider.Value = ConfigManager.CurrentConfig.SectorCornerRadius;
                SectorCornerRadiusLabel.Text = $"{ConfigManager.CurrentConfig.SectorCornerRadius:0} px";
                SectorIconSizeSlider.Value = ConfigManager.CurrentConfig.SectorIconSize > 0 ? ConfigManager.CurrentConfig.SectorIconSize : 20.0;
                SectorIconSizeLabel.Text = $"{SectorIconSizeSlider.Value:0} px";
                SectorFontSizeSlider.Value = ConfigManager.CurrentConfig.SectorFontSize > 0 ? ConfigManager.CurrentConfig.SectorFontSize : 10.5;
                SectorFontSizeLabel.Text = $"{SectorFontSizeSlider.Value:0.0} px";

                SetComboBoxSelectedValue(ShapeComboBox, ConfigManager.CurrentConfig.Shape);
                SetComboBoxSelectedValue(IconLayoutModeComboBox, ConfigManager.CurrentConfig.IconLayoutMode);
                ShowTextCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowText;

                // Center Core Pattern, Image & Visibility
                ShowCoreIconCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowCoreIcon;
                SetComboBoxSelectedValue(CoreIconTypeComboBox, ConfigManager.CurrentConfig.CoreIconType ?? "Exit");
                CoreImagePathTextBox.Text = ConfigManager.CurrentConfig.CoreCustomImagePath ?? "";
                UpdateCoreIconPreviewUI();

                // Load Scene Isolation settings
                DisableOnFullScreenCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnFullScreen;
                CtrlModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnCtrl;
                ShiftModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnShift;
                AltModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnAlt;

                if (ConfigManager.CurrentConfig.BlacklistedProcesses != null)
                {
                    foreach (var proc in ConfigManager.CurrentConfig.BlacklistedProcesses)
                    {
                        BlacklistListBox.Items.Add(proc);
                    }
                }

                // Auto-start setting
                AutoStartCheckBox.IsChecked = ConfigManager.IsAutoStartEnabled();

                // Initialize Language setting
                SetComboBoxSelectedValue(LanguageComboBox, ConfigManager.CurrentConfig.Language ?? "Auto");
                ApplyLocalization();

                // Initialize color preview borders
                UpdateColorPreviews();

                SlotsItemsControl.ItemsSource = _profileList.Slots;

                // Check UAC privileges and show warning if not elevated
                bool isAdmin = IsRunningAsAdmin();
                UacWarningCard.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

                // Select default profile
                var firstItem = _profileList.Profiles.FirstOrDefault();
                if (firstItem != null)
                {
                    // 与迁移前一致：仅设置可视选中（处理器此时被 _isUpdatingUi 拦截），状态经 VM 落地。
                    ProfilesListBox.SelectedItem = firstItem;
                    _profileList.SelectProfile(firstItem);
                    UpdateSectorCountRadios();
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }

            this.Loaded += (s, e) =>
            {
                if (AppearanceSettingsGrid.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                MemoryOptimizer.TrimMemory();
            };
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                ConfigManager.CurrentConfig.Language = langCode;
                I18n.SetLanguage(langCode);
                ApplyLocalization();
                ConfigManager.SaveConfig();
            }
        }

        public void ApplyLocalization()
        {
            this.Title = I18n.T("WindowTitle") + DevInstance.Suffix;
            if (SidebarSubtitleText != null) SidebarSubtitleText.Text = I18n.T("AppSubtitle");

            // Sidebar Tabs
            if (NavTab0Text != null) NavTab0Text.Text = I18n.T("TabTrigger");
            if (NavTab1Text != null) NavTab1Text.Text = I18n.T("TabAppearance");
            if (NavTab2Text != null) NavTab2Text.Text = I18n.T("TabGestures");
            if (NavTab3Text != null) NavTab3Text.Text = I18n.T("TabAdvanced");
            if (NavTab4Text != null) NavTab4Text.Text = I18n.T("TabAbout");

            // Bottom Bar
            if (BottomNoteText != null) BottomNoteText.Text = I18n.T("BottomStatusNote");
            if (SaveButton != null) SaveButton.Content = I18n.T("BtnSave");
            if (CloseButton != null) CloseButton.Content = I18n.T("BtnClose");

            // Tab 0: Trigger & Scenes
            if (TriggerPageHeader != null) TriggerPageHeader.Text = I18n.T("TriggerHeader");
            if (TriggerPageSubheader != null) TriggerPageSubheader.Text = I18n.T("TriggerSubheader");
            if (SensitivityTitleText != null) SensitivityTitleText.Text = I18n.T("SensitivityTitle");
            if (SensitivityDescText != null) SensitivityDescText.Text = I18n.T("SensitivityDesc");
            if (SceneIsolationTitleText != null) SceneIsolationTitleText.Text = I18n.T("SceneIsolationTitle");
            if (SceneIsolationDescText != null) SceneIsolationDescText.Text = I18n.T("SceneIsolationDesc");
            if (FullScreenOptionTitleText != null) FullScreenOptionTitleText.Text = I18n.T("FullScreenOption");
            if (FullScreenOptionDescText != null) FullScreenOptionDescText.Text = I18n.T("FullScreenOptionDesc");
            if (ModifierPassTitleText != null) ModifierPassTitleText.Text = I18n.T("ModifierPassTitle");
            if (CtrlModifierCheckBox != null) CtrlModifierCheckBox.Content = I18n.T("ModifierCtrl");
            if (ShiftModifierCheckBox != null) ShiftModifierCheckBox.Content = I18n.T("ModifierShift");
            if (AltModifierCheckBox != null) AltModifierCheckBox.Content = I18n.T("ModifierAlt");
            if (BlacklistTitleText != null) BlacklistTitleText.Text = I18n.T("BlacklistTitle");
            if (BlacklistDescText != null) BlacklistDescText.Text = I18n.T("BlacklistDesc");
            if (BrowseBlacklistButton != null) BrowseBlacklistButton.Content = I18n.T("BtnPickProcess");
            if (AddBlacklistButton != null) AddBlacklistButton.Content = I18n.T("BtnAddProcess");
            if (DeleteBlacklistButton != null) DeleteBlacklistButton.Content = I18n.T("BtnDeleteProcess");
            if (NewBlacklistProcessTextBox != null) NewBlacklistProcessTextBox.ToolTip = I18n.T("BlacklistPlaceholder");

            if (OuterEscapeTitleText != null) OuterEscapeTitleText.Text = I18n.T("OuterEscapeTitle");
            if (OuterEscapeDescText != null) OuterEscapeDescText.Text = I18n.T("OuterEscapeDesc");
            if (OuterEscapeCheckboxTitleText != null) OuterEscapeCheckboxTitleText.Text = I18n.T("OuterEscapeCheckbox");
            if (OuterEscapeDistanceTitleText != null) OuterEscapeDistanceTitleText.Text = I18n.T("OuterEscapeDistanceTitle");
            if (OuterEscapeDistanceDescText != null) OuterEscapeDistanceDescText.Text = I18n.T("OuterEscapeDistanceDesc");
            if (RenameCustomColorPresetButton != null) RenameCustomColorPresetButton.Content = I18n.T("RenameCustomPresetButton");
            
            // Tab 1: Appearance & Shapes
            if (CustomColorsExpanderTitleText != null) CustomColorsExpanderTitleText.Text = I18n.T("CustomColorsExpanderTitle");
            if (CustomColorsExpanderDescText != null) CustomColorsExpanderDescText.Text = I18n.T("CustomColorsExpanderDesc");
            var olderExpander = this.FindName("OlderMilestonesExpander") as System.Windows.Controls.Expander;
            if (olderExpander != null) olderExpander.Header = I18n.T("MilestonesOlderExpander");
            if (AppearancePageHeader != null) AppearancePageHeader.Text = I18n.T("AppearanceHeader");
            if (AppearancePageSubheader != null) AppearancePageSubheader.Text = I18n.T("AppearanceSubheader");
                        if (ResetDimensionsButton != null) ResetDimensionsButton.Content = I18n.T("BtnResetGeometry");

            // Tab 2: Gestures & Actions
            if (GesturesPageHeader != null) GesturesPageHeader.Text = I18n.T("GesturesHeader");
            if (ProfileCardTitleText != null) ProfileCardTitleText.Text = I18n.T("ProfileCardTitle");
            if (ProfileCardDescText != null) ProfileCardDescText.Text = I18n.T("ProfileCardDesc");
            if (AddProfileButton != null) AddProfileButton.Content = I18n.T("BtnAddAppProfile");
            if (AddCustomProfileButton != null) AddCustomProfileButton.Content = I18n.T("BtnAddCustomProfile");
            if (RenameProfileButton != null) RenameProfileButton.Content = I18n.T("BtnRenameProfile");
            if (DeleteProfileButton != null) DeleteProfileButton.Content = I18n.T("BtnDeleteProfile");
            if (SectorCountTitleText != null) SectorCountTitleText.Text = I18n.T("SectorCountOptionTitle");
            if (SectorCountDescText != null) SectorCountDescText.Text = I18n.T("SectorCountOptionDesc");
            if (SectorCount4Radio != null) SectorCount4Radio.Content = I18n.T("SectorCount4");
            if (SectorCount8Radio != null) SectorCount8Radio.Content = I18n.T("SectorCount8");
            if (SectorCount12Radio != null) SectorCount12Radio.Content = I18n.T("SectorCount12");
            if (SectorActionListTitleText != null) SectorActionListTitleText.Text = I18n.T("SectorActionListTitle");
            if (SectorActionListDescText != null) SectorActionListDescText.Text = I18n.T("SectorActionListDesc");

            // Tab 3: Advanced & System
            if (AdvancedPageHeader != null) AdvancedPageHeader.Text = I18n.T("AdvancedHeader");
            if (LanguageTitleText != null) LanguageTitleText.Text = I18n.T("LanguageTitle");
            if (LanguageDescText != null) LanguageDescText.Text = I18n.T("LanguageDesc");
            if (StartupTitleText != null) StartupTitleText.Text = I18n.T("StartupTitle");
            if (StartupDescText != null) StartupDescText.Text = I18n.T("StartupDesc");
            if (ElevateTitleText != null) ElevateTitleText.Text = I18n.T("ElevateTitle");
            if (ElevateDescText != null) ElevateDescText.Text = I18n.T("ElevateDesc");
            if (ElevateButton != null) ElevateButton.Content = I18n.T("BtnElevate");
            if (MemoryOptTitleText != null) MemoryOptTitleText.Text = I18n.T("MemoryTitle");
            if (MemoryOptDescText != null) MemoryOptDescText.Text = I18n.T("MemoryDesc");
            if (TrimMemoryButton != null) TrimMemoryButton.Content = I18n.T("BtnTrimMemory");
            if (BackupTitleText != null) BackupTitleText.Text = I18n.T("BackupTitle");
            if (ExportConfigButton != null) ExportConfigButton.Content = I18n.T("BtnExportConfig");
            if (ImportConfigButton != null) ImportConfigButton.Content = I18n.T("BtnImportConfig");

            // Tray menu labels refresh automatically (menu is rebuilt on every open)
        }

        public void ShowSettings(int tabIndex = 0)
        {
            SwitchToTab(tabIndex);

            this.Opacity = 0.0;
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();

            // Smooth fade-in animation
            var anim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(160)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, anim);
        }

        private void NavTab_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (sender is FrameworkElement elem && int.TryParse(elem.Tag?.ToString(), out int tabIndex))
            {
                SwitchToTab(tabIndex);
            }
        }

        public void SwitchToTab(int index)
        {
            if (TriggerSettingsGrid == null || AppearanceSettingsGrid == null || MappingsSettingsGrid == null || SystemSettingsGrid == null || AboutSettingsGrid == null) return;

            TriggerSettingsGrid.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            AppearanceSettingsGrid.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            MappingsSettingsGrid.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            SystemSettingsGrid.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            AboutSettingsGrid.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

            _isUpdatingUi = true;
            try
            {
                if (NavTab0 != null) NavTab0.IsChecked = index == 0;
                if (NavTab1 != null) NavTab1.IsChecked = index == 1;
                if (NavTab2 != null) NavTab2.IsChecked = index == 2;
                if (NavTab3 != null) NavTab3.IsChecked = index == 3;
                if (NavTab4 != null) NavTab4.IsChecked = index == 4;
            }
            finally
            {
                _isUpdatingUi = false;
            }

            if (index == 2)
            {
                if (_profileList.SelectedProfile == null && _profileList.Profiles.Count > 0)
                {
                    if (ProfilesListBox != null) ProfilesListBox.SelectedItem = _profileList.Profiles[0];
                }
                if (_profileList.SelectedProfile != null)
                {
                    _isUpdatingUi = true;
                    try
                    {
                        UpdateSectorCountRadios();
                        _profileList.RebuildSlots();
                    }
                    finally
                    {
                        _isUpdatingUi = false;
                    }
                }
            }
            else if (index == 1)
            {
                RenderLiveWheelPreview();
            }
        }

        private DispatcherTimer? _autoSaveDebounceTimer;

        private void ScheduleAutoSave()
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;

            if (_autoSaveDebounceTimer == null)
            {
                _autoSaveDebounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(400)
                };
                _autoSaveDebounceTimer.Tick += (s, e) =>
                {
                    _autoSaveDebounceTimer.Stop();
                    SyncUiToConfigAndSave(true);
                };
            }

            _autoSaveDebounceTimer.Stop();
            _autoSaveDebounceTimer.Start();
        }

        private void SyncUiToConfigAndSave(bool saveToDisk = true)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;

            try
            {
                if (AppThemeComboBox?.SelectedItem is ComboBoxItem appThemeItem)
                {
                    ConfigManager.CurrentConfig.AppTheme = appThemeItem.Tag?.ToString() ?? "System";
                }
                if (UiStyleComboBox?.SelectedItem is ComboBoxItem uiStyleItem)
                {
                    ConfigManager.CurrentConfig.UiStyle = uiStyleItem.Tag?.ToString() ?? "ClassicRing";
                }
                if (ThemeComboBox?.SelectedItem is ComboBoxItem themeItem)
                {
                    ConfigManager.CurrentConfig.Theme = themeItem.Tag?.ToString() ?? "System";
                }
                if (ShapeComboBox?.SelectedItem is ComboBoxItem shapeItem)
                {
                    ConfigManager.CurrentConfig.Shape = shapeItem.Tag?.ToString() ?? "Original";
                }
                if (IconLayoutModeComboBox?.SelectedItem is ComboBoxItem layoutItem)
                {
                    ConfigManager.CurrentConfig.IconLayoutMode = layoutItem.Tag?.ToString() ?? "IconAndText";
                }

                if (ShowTextCheckBox != null)
                {
                    ConfigManager.CurrentConfig.ShowText = ShowTextCheckBox.IsChecked == true;
                }

                if (ShowCoreIconCheckBox != null)
                {
                    ConfigManager.CurrentConfig.ShowCoreIcon = ShowCoreIconCheckBox.IsChecked == true;
                }
                if (CoreIconTypeComboBox?.SelectedItem is ComboBoxItem coreIconItem)
                {
                    ConfigManager.CurrentConfig.CoreIconType = coreIconItem.Tag?.ToString() ?? "Exit";
                }
                if (CoreImagePathTextBox != null)
                {
                    ConfigManager.CurrentConfig.CoreCustomImagePath = CoreImagePathTextBox.Text.Trim();
                }

                if (HighlightGlowPresetComboBox?.SelectedItem is ComboBoxItem glowPresetItem)
                {
                    ConfigManager.CurrentConfig.HighlightGlowPreset = glowPresetItem.Tag?.ToString() ?? "Auto";
                }
                if (HighlightGlowColorTextBox != null)
                {
                    ConfigManager.CurrentConfig.HighlightGlowColor = HighlightGlowColorTextBox.Text.Trim();
                }
                if (HighlightGlowRadiusSlider != null)
                {
                    ConfigManager.CurrentConfig.HighlightGlowRadius = HighlightGlowRadiusSlider.Value;
                }
                if (HighlightGlowOpacitySlider != null)
                {
                    ConfigManager.CurrentConfig.HighlightGlowOpacity = HighlightGlowOpacitySlider.Value / 100.0;
                }

                if (WheelRadiusSlider != null) ConfigManager.CurrentConfig.WheelRadius = WheelRadiusSlider.Value;
                if (InnerRadiusSlider != null) ConfigManager.CurrentConfig.InnerRadius = InnerRadiusSlider.Value;
                if (CoreRadiusSlider != null) ConfigManager.CurrentConfig.CoreRadius = CoreRadiusSlider.Value;
                if (SectorGapSlider != null) ConfigManager.CurrentConfig.SectorGap = SectorGapSlider.Value;
                if (SectorCornerRadiusSlider != null) ConfigManager.CurrentConfig.SectorCornerRadius = SectorCornerRadiusSlider.Value;
                if (SectorIconSizeSlider != null) ConfigManager.CurrentConfig.SectorIconSize = SectorIconSizeSlider.Value;
                if (SectorFontSizeSlider != null) ConfigManager.CurrentConfig.SectorFontSize = SectorFontSizeSlider.Value;
                if (ThresholdSlider != null) ConfigManager.CurrentConfig.DragThreshold = ThresholdSlider.Value;

                if (CustomSectorBgTextBox != null) ConfigManager.CurrentConfig.CustomSectorBg = CustomSectorBgTextBox.Text.Trim();
                if (CustomSectorBorderTextBox != null) ConfigManager.CurrentConfig.CustomSectorBorder = CustomSectorBorderTextBox.Text.Trim();
                if (CustomHighlightBgTextBox != null) ConfigManager.CurrentConfig.CustomHighlightBg = CustomHighlightBgTextBox.Text.Trim();
                if (CustomHighlightBorderTextBox != null) ConfigManager.CurrentConfig.CustomHighlightBorder = CustomHighlightBorderTextBox.Text.Trim();
                if (CustomTextTextBox != null) ConfigManager.CurrentConfig.CustomText = CustomTextTextBox.Text.Trim();

                if (DisableOnFullScreenCheckBox != null) ConfigManager.CurrentConfig.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
                if (CtrlModifierCheckBox != null) ConfigManager.CurrentConfig.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
                if (ShiftModifierCheckBox != null) ConfigManager.CurrentConfig.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
                if (AltModifierCheckBox != null) ConfigManager.CurrentConfig.DisableOnAlt = AltModifierCheckBox.IsChecked == true;

                if (saveToDisk)
                {
                    ConfigManager.SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncUiToConfigAndSave Error]: {ex.Message}");
            }
        }

        /// <summary>Persists in-progress UI edits to config; called by the composition root before an app-level exit.</summary>
        public void SavePendingChanges()
        {
            SyncUiToConfigAndSave(true);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // App-level exit (ADR-0003): pending edits were already flushed by the
            // composition root — allow the close. Any other close hides to the tray.
            if (Composition.IsExiting) return;

            SyncUiToConfigAndSave(true);
            MemoryOptimizer.TrimMemory();

            e.Cancel = true;

            // Fade out before hiding
            var anim = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(120)));
            anim.Completed += (s, ev) =>
            {
                this.Hide();
                this.Opacity = 1.0;
                MemoryOptimizer.TrimMemory();
            };
            this.BeginAnimation(Window.OpacityProperty, anim);

            _showTrayBalloonTip(
                "WinPieGestures",
                "应用已最小化至系统托盘，将在后台继续运行鼠标笔势监视。");
        }

        private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;

            if (!_profileList.SelectProfile(ProfilesListBox.SelectedItem as ProfileItemViewModel)) return;

            _isUpdatingUi = true;
            try
            {
                // Update radio buttons
                UpdateSectorCountRadios();
            }
            finally
            {
                _isUpdatingUi = false;
            }

            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
        }

        /// <summary>把选中方案的扇区数（原始值）同步到 4/8/12 单选钮（与迁移前的三处手工同步一致）。</summary>
        private void UpdateSectorCountRadios()
        {
            int count = _profileList.SelectedProfile?.Model.SectorCount ?? 0;
            if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = count == 4;
            if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = count == 8;
            if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = count == 12;
        }

        private void SectorCountRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;

            int newCount = 8;
            if (SectorCount4Radio?.IsChecked == true) newCount = 4;
            else if (SectorCount8Radio?.IsChecked == true) newCount = 8;
            else if (SectorCount12Radio?.IsChecked == true) newCount = 12;

            if (!_profileList.ApplySectorCount(newCount)) return;

            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            SyncUiToConfigAndSave(true);
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // T06：程序选择走对话框服务（VM 化链路），取消与无效统一 null。
            var picked = _dialogs.ShowProgramPicker();
            if (picked != null)
            {
                string procName = Path.GetFileName(picked.Path).ToLower();
                if (ConfigManager.CurrentConfig.Profiles.Any(p => p.ProcessName.Equals(procName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("已存在该程序的配置方案！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int currentSectorCount = _profileList.SelectedProfile?.Model.SectorCount ?? 8;
                var newProfile = new WheelProfile
                {
                    ProcessName = procName,
                    SectorCount = currentSectorCount,
                    Actions = new List<ActionItem>()
                };

                for (int i = 0; i < currentSectorCount; i++)
                {
                    newProfile.Actions.Add(new ActionItem { Type = "Hotkey", Name = $"动作 {i + 1}", Parameter = "" });
                }

                ProfilesListBox.SelectedItem = _profileList.AddProfile(newProfile);
                SyncUiToConfigAndSave(true);
            }
        }

        private void AddCustomProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var result = _dialogs.ShowInputDialog(
                title: "新建自定义配置",
                prompt: "请输入新配置方案名称（如：游戏模式、绘图工作流、PS修图 或 myapp.exe）：",
                defaultText: $"自定义配置_{ConfigManager.CurrentConfig.Profiles.Count}",
                validator: input =>
                {
                    if (ConfigManager.CurrentConfig.Profiles.Any(p => p.ProcessName.Equals(input, StringComparison.OrdinalIgnoreCase)))
                    {
                        return (false, "已存在同名的配置方案，请换一个名称！");
                    }
                    return (true, "");
                });

            if (result != null)
            {
                int currentSectorCount = _profileList.SelectedProfile?.Model.SectorCount ?? 8;
                var newProfile = new WheelProfile
                {
                    ProcessName = result.Text,
                    SectorCount = currentSectorCount,
                    Actions = new List<ActionItem>()
                };

                for (int i = 0; i < currentSectorCount; i++)
                {
                    newProfile.Actions.Add(new ActionItem { Type = "Hotkey", Name = $"动作 {i + 1}", Parameter = "" });
                }

                ProfilesListBox.SelectedItem = _profileList.AddProfile(newProfile);
                SyncUiToConfigAndSave(true);
            }
        }

        private void RenameProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _profileList.SelectedProfile;
            if (selected == null)
            {
                MessageBox.Show("请先在列表中选择要重命名的配置方案！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selected.Model.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("「Global」为系统全局默认基础配置，不可重命名。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string oldName = selected.Model.ProcessName;
            var result = _dialogs.ShowInputDialog(
                title: "重命名配置方案",
                prompt: $"请输入配置方案「{oldName}」的新名称：",
                defaultText: oldName,
                validator: input =>
                {
                    if (input.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        return (true, "");
                    }
                    if (ConfigManager.CurrentConfig.Profiles.Any(p => p.ProcessName.Equals(input, StringComparison.OrdinalIgnoreCase)))
                    {
                        return (false, "已存在同名的配置方案，请换一个名称！");
                    }
                    return (true, "");
                });

            if (result != null)
            {
                selected.Model.ProcessName = result.Text;
                selected.RefreshDisplay();
                SyncUiToConfigAndSave(true);
            }
        }

        private void ProfilesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is ProfileItemViewModel item && !item.Model.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                RenameProfileButton_Click(sender, e);
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _profileList.SelectedProfile;
            if (selected == null) return;

            if (selected.Model.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("全局默认配置 (Global) 不能删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"确定要删除配置方案 [{selected.Model.ProcessName}] 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _profileList.RemoveProfile(selected);
                ProfilesListBox.SelectedIndex = 0;
                SyncUiToConfigAndSave(true);
            }
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThresholdValueLabel != null && ConfigManager.CurrentConfig != null)
            {
                ThresholdValueLabel.Text = e.NewValue.ToString("0");
                ConfigManager.CurrentConfig.DragThreshold = e.NewValue;
                ScheduleAutoSave();
            }
        }

        private void UiStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UiStyleComboBox == null || ConfigManager.CurrentConfig == null) return;
            var selectedItem = UiStyleComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                ConfigManager.CurrentConfig.UiStyle = selectedItem.Tag?.ToString() ?? "ClassicRing";
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
            }
        }

        private void ReloadThemePresets()
        {
            if (ThemeComboBox == null) return;
            string currentTag = ConfigManager.CurrentConfig?.Theme ?? (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";

            var toRemove = new List<ComboBoxItem>();
            foreach (var item in ThemeComboBox.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag != null && cbi.Tag.ToString()!.StartsWith("CustomPreset_"))
                {
                    toRemove.Add(cbi);
                }
            }
            foreach (var item in toRemove)
            {
                ThemeComboBox.Items.Remove(item);
            }

            int customIndex = -1;
            for (int i = 0; i < ThemeComboBox.Items.Count; i++)
            {
                if (ThemeComboBox.Items[i] is ComboBoxItem cbi && cbi.Tag?.ToString() == "Custom")
                {
                    customIndex = i;
                    break;
                }
            }

            if (ConfigManager.CurrentConfig.CustomColorPresets != null)
            {
                foreach (var preset in ConfigManager.CurrentConfig.CustomColorPresets)
                {
                    var newItem = new ComboBoxItem
                    {
                        Content = $"🎨 {preset.Name} (自定义预设)",
                        Tag = "CustomPreset_" + preset.Id
                    };
                    if (customIndex >= 0)
                    {
                        ThemeComboBox.Items.Insert(customIndex, newItem);
                        customIndex++;
                    }
                    else
                    {
                        ThemeComboBox.Items.Add(newItem);
                    }
                }
            }

            SetComboBoxSelectedValue(ThemeComboBox, currentTag);
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox == null || ConfigManager.CurrentConfig == null) return;
            var selectedItem = ThemeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string theme = selectedItem.Tag?.ToString() ?? "System";
                ConfigManager.CurrentConfig.Theme = theme;

                bool isCustomPreset = theme.StartsWith("CustomPreset_");

                if (RenameCustomColorPresetButton != null)
                {
                    RenameCustomColorPresetButton.Visibility = isCustomPreset ? Visibility.Visible : Visibility.Collapsed;
                }
                if (DeletePresetInPanelButton != null)
                {
                    DeletePresetInPanelButton.Visibility = isCustomPreset ? Visibility.Visible : Visibility.Collapsed;
                }

                if (isCustomPreset)
                {
                    string presetId = theme.Substring("CustomPreset_".Length);
                    var preset = ConfigManager.CurrentConfig.CustomColorPresets?.Find(p => p.Id == presetId);
                    if (preset != null)
                    {
                        CustomSectorBgTextBox.Text = preset.SectorBg;
                        CustomSectorBorderTextBox.Text = preset.SectorBorder;
                        CustomHighlightBgTextBox.Text = preset.HighlightBg;
                        CustomHighlightBorderTextBox.Text = preset.HighlightBorder;
                        CustomTextTextBox.Text = preset.TextColor;
                    }
                    if (CustomColorExpander != null)
                    {
                        CustomColorExpander.IsExpanded = true;
                    }
                }

                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);
            }
        }

        private void OuterEscapeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (ConfigManager.CurrentConfig == null) return;
            ConfigManager.CurrentConfig.EnableOuterEscapeCancel = true;
            if (OuterEscapeDistancePanel != null) OuterEscapeDistancePanel.Visibility = Visibility.Visible;
            SyncUiToConfigAndSave(true);
        }

        private void OuterEscapeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ConfigManager.CurrentConfig == null) return;
            ConfigManager.CurrentConfig.EnableOuterEscapeCancel = false;
            if (OuterEscapeDistancePanel != null) OuterEscapeDistancePanel.Visibility = Visibility.Collapsed;
            SyncUiToConfigAndSave(true);
        }

        private void OuterEscapeDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;
            double val = Math.Round(e.NewValue);
            ConfigManager.CurrentConfig.OuterEscapeDistance = val;
            if (OuterEscapeDistanceLabel != null)
            {
                OuterEscapeDistanceLabel.Text = $"{val:0} px";
            }
            SyncUiToConfigAndSave(true);
        }

        private void RenameCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigManager.CurrentConfig == null) return;
            string theme = (ThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.Theme ?? "";
            if (!theme.StartsWith("CustomPreset_")) return;

            string presetId = theme.Substring("CustomPreset_".Length);
            var preset = ConfigManager.CurrentConfig.CustomColorPresets?.Find(p => p.Id == presetId);
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

            if (result != null)
            {
                preset.Name = result.Text;
                ConfigManager.SaveConfig();

                ReloadThemePresets();
                SetComboBoxSelectedValue(ThemeComboBox, $"CustomPreset_{preset.Id}");
                SyncUiToConfigAndSave(true);
            }
        }

        private void DeleteCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigManager.CurrentConfig == null) return;
            string theme = (ThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.Theme ?? "";
            if (!theme.StartsWith("CustomPreset_")) return;

            string presetId = theme.Substring("CustomPreset_".Length);
            var preset = ConfigManager.CurrentConfig.CustomColorPresets?.Find(p => p.Id == presetId);
            if (preset == null) return;

            var result = MessageBox.Show(this, $"确定要删除自定义配色方案预设【{preset.Name}】吗？", "确认删除配色方案", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ConfigManager.CurrentConfig.CustomColorPresets?.Remove(preset);
                ConfigManager.CurrentConfig.Theme = "System";
                ConfigManager.SaveConfig();

                ReloadThemePresets();
                SetComboBoxSelectedValue(ThemeComboBox, "System");

                if (RenameCustomColorPresetButton != null) RenameCustomColorPresetButton.Visibility = Visibility.Collapsed;
                if (DeletePresetInPanelButton != null) DeletePresetInPanelButton.Visibility = Visibility.Collapsed;
                

                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);

                MessageBox.Show(this, $"自定义配色方案【{preset.Name}】已成功删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = _dialogs.ShowInputDialog("保存配色预设", "请输入自定义配色方案名称:", $"自定义配色 {DateTime.Now:MMdd-HHmm}");

            if (result != null)
            {
                string presetName = result.Text;
                if (ConfigManager.CurrentConfig.CustomColorPresets == null)
                {
                    ConfigManager.CurrentConfig.CustomColorPresets = new List<CustomColorPreset>();
                }

                var newPreset = new CustomColorPreset
                {
                    Name = presetName,
                    SectorBg = CustomSectorBgTextBox.Text.Trim(),
                    SectorBorder = CustomSectorBorderTextBox.Text.Trim(),
                    HighlightBg = CustomHighlightBgTextBox.Text.Trim(),
                    HighlightBorder = CustomHighlightBorderTextBox.Text.Trim(),
                    TextColor = CustomTextTextBox.Text.Trim()
                };

                ConfigManager.CurrentConfig.CustomColorPresets.Add(newPreset);
                ConfigManager.CurrentConfig.Theme = "CustomPreset_" + newPreset.Id;
                ConfigManager.SaveConfig();

                ReloadThemePresets();
                SetComboBoxSelectedValue(ThemeComboBox, "CustomPreset_" + newPreset.Id);
                RenderLiveWheelPreview();

                System.Windows.MessageBox.Show($"配色预设【{presetName}】已成功保存！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void WheelRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WheelRadiusLabel != null && ConfigManager.CurrentConfig != null)
            {
                WheelRadiusLabel.Text = e.NewValue.ToString("0");
                ConfigManager.CurrentConfig.WheelRadius = e.NewValue;
                if (!_isUpdatingUi && AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                ScheduleAutoSave();
            }
        }

        private void InnerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (InnerRadiusLabel != null && ConfigManager.CurrentConfig != null)
            {
                InnerRadiusLabel.Text = e.NewValue.ToString("0");
                ConfigManager.CurrentConfig.InnerRadius = e.NewValue;
                if (!_isUpdatingUi && AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                ScheduleAutoSave();
            }
        }

        private void CoreRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CoreRadiusLabel != null && ConfigManager.CurrentConfig != null)
            {
                CoreRadiusLabel.Text = e.NewValue.ToString("0");
                ConfigManager.CurrentConfig.CoreRadius = e.NewValue;
                if (!_isUpdatingUi && AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                ScheduleAutoSave();
            }
        }

        private void SectorGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SectorGapLabel != null && ConfigManager.CurrentConfig != null)
            {
                SectorGapLabel.Text = $"{e.NewValue:0} px";
                ConfigManager.CurrentConfig.SectorGap = e.NewValue;
                if (!_isUpdatingUi && AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                ScheduleAutoSave();
            }
        }

        private void SectorCornerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SectorCornerRadiusLabel != null && ConfigManager.CurrentConfig != null)
            {
                SectorCornerRadiusLabel.Text = $"{e.NewValue:0} px";
                ConfigManager.CurrentConfig.SectorCornerRadius = e.NewValue;
                if (!_isUpdatingUi && AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                ScheduleAutoSave();
            }
        }

        private void ShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShapeComboBox == null || ConfigManager.CurrentConfig == null) return;
            var selectedItem = ShapeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                ConfigManager.CurrentConfig.Shape = selectedItem.Tag?.ToString() ?? "Original";
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);
            }
        }

        private void IconLayoutModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IconLayoutModeComboBox == null || ConfigManager.CurrentConfig == null || _isUpdatingUi) return;
            var selectedItem = IconLayoutModeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string mode = selectedItem.Tag?.ToString() ?? "IconAndText";
                ConfigManager.CurrentConfig.IconLayoutMode = mode;

                _isUpdatingUi = true;
                try
                {
                    if (mode == "IconOnly")
                    {
                        ConfigManager.CurrentConfig.ShowText = false;
                        if (ShowTextCheckBox != null) ShowTextCheckBox.IsChecked = false;
                    }
                    else
                    {
                        ConfigManager.CurrentConfig.ShowText = true;
                        if (ShowTextCheckBox != null) ShowTextCheckBox.IsChecked = true;
                    }
                }
                finally
                {
                    _isUpdatingUi = false;
                }

                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);
            }
        }

        private void ShowTextCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowTextCheckBox == null || ConfigManager.CurrentConfig == null || _isUpdatingUi) return;
            bool isShow = ShowTextCheckBox.IsChecked == true;
            ConfigManager.CurrentConfig.ShowText = isShow;

            _isUpdatingUi = true;
            try
            {
                if (isShow)
                {
                    if (ConfigManager.CurrentConfig.IconLayoutMode == "IconOnly")
                    {
                        ConfigManager.CurrentConfig.IconLayoutMode = "IconAndText";
                        SetComboBoxSelectedValue(IconLayoutModeComboBox, "IconAndText");
                    }
                }
                else
                {
                    if (ConfigManager.CurrentConfig.IconLayoutMode != "IconOnly")
                    {
                        ConfigManager.CurrentConfig.IconLayoutMode = "IconOnly";
                        SetComboBoxSelectedValue(IconLayoutModeComboBox, "IconOnly");
                    }
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }

            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            SyncUiToConfigAndSave(true);
        }

        private void SectorIconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SectorIconSizeSlider == null || SectorIconSizeLabel == null || ConfigManager.CurrentConfig == null || _isUpdatingUi) return;
            ConfigManager.CurrentConfig.SectorIconSize = e.NewValue;
            SectorIconSizeLabel.Text = $"{e.NewValue:0} px";
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            ScheduleAutoSave();
        }

        private void SectorFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SectorFontSizeSlider == null || SectorFontSizeLabel == null || ConfigManager.CurrentConfig == null || _isUpdatingUi) return;
            ConfigManager.CurrentConfig.SectorFontSize = e.NewValue;
            SectorFontSizeLabel.Text = $"{e.NewValue:0.0} px";
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            ScheduleAutoSave();
        }

        private void ResetDimensionsButton_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingUi = true;
            try
            {
                WheelRadiusSlider.Value = 138;
                WheelRadiusLabel.Text = "138";
                InnerRadiusSlider.Value = 52;
                InnerRadiusLabel.Text = "52";
                CoreRadiusSlider.Value = 50;
                CoreRadiusLabel.Text = "50";
                SectorGapSlider.Value = 2;
                SectorGapLabel.Text = "2 px";
                SectorCornerRadiusSlider.Value = 4;
                SectorCornerRadiusLabel.Text = "4 px";
                SectorIconSizeSlider.Value = 20.0;
                SectorIconSizeLabel.Text = "20 px";
                SectorFontSizeSlider.Value = 10.5;
                SectorFontSizeLabel.Text = "10.5 px";

                ConfigManager.CurrentConfig.WheelRadius = 138;
                ConfigManager.CurrentConfig.InnerRadius = 52;
                ConfigManager.CurrentConfig.CoreRadius = 50;
                ConfigManager.CurrentConfig.SectorGap = 2;
                ConfigManager.CurrentConfig.SectorCornerRadius = 4;
                ConfigManager.CurrentConfig.SectorIconSize = 20.0;
                ConfigManager.CurrentConfig.SectorFontSize = 10.5;
            }
            finally
            {
                _isUpdatingUi = false;
            }
            RenderLiveWheelPreview();
            SyncUiToConfigAndSave(true);
        }

        private void ShowCoreIconCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;
            ConfigManager.CurrentConfig.ShowCoreIcon = ShowCoreIconCheckBox.IsChecked == true;
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            SyncUiToConfigAndSave(true);
        }

        private void CoreIconTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;
            var selectedItem = CoreIconTypeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string coreType = selectedItem.Tag?.ToString() ?? "Exit";
                ConfigManager.CurrentConfig.CoreIconType = coreType;
                UpdateCoreIconPreviewUI();
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);
            }
        }

        private void PickCoreIconButton_Click(object sender, RoutedEventArgs e)
        {
            var picked = _dialogs.ShowIconPicker(ConfigManager.CurrentConfig.CoreCustomIconKey);
            if (picked != null)
            {
                ConfigManager.CurrentConfig.CoreCustomIconKey = picked.IconKey ?? "";
                UpdateCoreIconPreviewUI();
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);
            }
        }

        private void UpdateCoreIconPreviewUI()
        {
            if (ConfigManager.CurrentConfig == null) return;

            string coreType = ConfigManager.CurrentConfig.CoreIconType ?? "Exit";
            if (CustomCoreIconPanel != null)
            {
                CustomCoreIconPanel.Visibility = coreType == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            }

            if (CustomCoreImagePanel != null)
            {
                CustomCoreImagePanel.Visibility = coreType == "Image" ? Visibility.Visible : Visibility.Collapsed;
                if (coreType == "Image")
                {
                    UpdateCoreImageThumbnail(ConfigManager.CurrentConfig.CoreCustomImagePath);
                }
            }

            if (CustomCoreIconPreviewPath != null && CustomCoreIconNameLabel != null)
            {
                var geom = IconHelper.GetCoreIconGeometry(coreType, ConfigManager.CurrentConfig.CoreCustomIconKey, ConfigManager.CurrentConfig.CoreCustomIconSvg);
                CustomCoreIconPreviewPath.Data = geom;
                if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomIconKey))
                {
                    CustomCoreIconNameLabel.Text = ConfigManager.CurrentConfig.CoreCustomIconKey;
                }
                else if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomIconSvg))
                {
                    CustomCoreIconNameLabel.Text = "自定义 SVG 图标";
                }
                else
                {
                    CustomCoreIconNameLabel.Text = "默认五角星 (点击更换)";
                }
            }
        }

        private void UpdateCoreImageThumbnail(string? imagePath)
        {
            if (CoreImageThumbnail == null) return;
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    CoreImageThumbnail.Source = bmp;
                }
                catch
                {
                    CoreImageThumbnail.Source = null;
                }
            }
            else
            {
                CoreImageThumbnail.Source = null;
            }
        }

        private void CoreImagePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null || CoreImagePathTextBox == null) return;
            ConfigManager.CurrentConfig.CoreCustomImagePath = CoreImagePathTextBox.Text.Trim();
            UpdateCoreImageThumbnail(ConfigManager.CurrentConfig.CoreCustomImagePath);
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            ScheduleAutoSave();
        }

        private void BrowseCoreImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picked = _dialogs.ShowOpenFileDialog(
                "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico;*.gif|所有文件 (*.*)|*.*",
                "选择中心核圆图案图片");

            if (picked != null)
            {
                string selectedPath = picked.Path;
                if (CoreImagePathTextBox != null)
                {
                    CoreImagePathTextBox.Text = selectedPath;
                }
                ConfigManager.CurrentConfig.CoreCustomImagePath = selectedPath;
                UpdateCoreImageThumbnail(selectedPath);
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);
            }
        }

        private void ClearCoreImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (CoreImagePathTextBox != null)
            {
                CoreImagePathTextBox.Text = "";
            }
            ConfigManager.CurrentConfig.CoreCustomImagePath = "";
            UpdateCoreImageThumbnail("");
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            SyncUiToConfigAndSave(true);
        }

        private void HighlightGlowPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || HighlightGlowPresetComboBox == null || ConfigManager.CurrentConfig == null) return;
            var selectedItem = HighlightGlowPresetComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string preset = selectedItem.Tag?.ToString() ?? "Auto";
                ConfigManager.CurrentConfig.HighlightGlowPreset = preset;

                switch (preset)
                {
                    case "Lilac":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "#A855F7";
                        HighlightGlowColorTextBox.Text = "#A855F7";
                        break;
                    case "Blue":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "#3B82F6";
                        HighlightGlowColorTextBox.Text = "#3B82F6";
                        break;
                    case "Emerald":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "#10B981";
                        HighlightGlowColorTextBox.Text = "#10B981";
                        break;
                    case "Rose":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "#EC4899";
                        HighlightGlowColorTextBox.Text = "#EC4899";
                        break;
                    case "Amber":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "#F59E0B";
                        HighlightGlowColorTextBox.Text = "#F59E0B";
                        break;
                    case "Red":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "#EF4444";
                        HighlightGlowColorTextBox.Text = "#EF4444";
                        break;
                    case "White":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "#FFFFFF";
                        HighlightGlowColorTextBox.Text = "#FFFFFF";
                        break;
                    case "Auto":
                        ConfigManager.CurrentConfig.HighlightGlowColor = "";
                        HighlightGlowColorTextBox.Text = "";
                        break;
                }

                if (CustomHighlightGlowPanel != null)
                {
                    CustomHighlightGlowPanel.Visibility = (preset == "Custom" || !string.IsNullOrEmpty(HighlightGlowColorTextBox.Text)) ? Visibility.Visible : Visibility.Collapsed;
                }

                UpdateColorPreviews();
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
                SyncUiToConfigAndSave(true);
            }
        }

        private void HighlightGlowColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null || HighlightGlowColorTextBox == null) return;
            ConfigManager.CurrentConfig.HighlightGlowColor = HighlightGlowColorTextBox.Text.Trim();
            UpdateColorPreviews();
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            ScheduleAutoSave();
        }

        private void HighlightGlowRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || HighlightGlowRadiusLabel == null || ConfigManager.CurrentConfig == null) return;
            HighlightGlowRadiusLabel.Text = $"{e.NewValue:0} px";
            ConfigManager.CurrentConfig.HighlightGlowRadius = e.NewValue;
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            ScheduleAutoSave();
        }

        private void HighlightGlowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || HighlightGlowOpacityLabel == null || ConfigManager.CurrentConfig == null) return;
            HighlightGlowOpacityLabel.Text = $"{e.NewValue:0}%";
            ConfigManager.CurrentConfig.HighlightGlowOpacity = e.NewValue / 100.0;
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            ScheduleAutoSave();
        }

        private void PickIcon_Click(object sender, RoutedEventArgs e)
        {
            // 对话框编排已迁 SlotViewModel.PickIcon (T12)；此处只剩 View 层效果：
            // 图标变化影响轮盘预览，外观分区可见时刷新。
            if (sender is FrameworkElement elem && elem.DataContext is SlotViewModel vm && vm.PickIcon())
            {
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
            }
        }

        private void CustomColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;

            ConfigManager.CurrentConfig.CustomSectorBg = CustomSectorBgTextBox.Text.Trim();
            ConfigManager.CurrentConfig.CustomSectorBorder = CustomSectorBorderTextBox.Text.Trim();
            ConfigManager.CurrentConfig.CustomHighlightBg = CustomHighlightBgTextBox.Text.Trim();
            ConfigManager.CurrentConfig.CustomHighlightBorder = CustomHighlightBorderTextBox.Text.Trim();
            ConfigManager.CurrentConfig.CustomText = CustomTextTextBox.Text.Trim();

            UpdateColorPreviews();

            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
        }

        private void UpdateColorPreviews()
        {
            UpdateColorPreviewBorder(CustomSectorBgPreview, CustomSectorBgTextBox.Text);
            UpdateColorPreviewBorder(CustomSectorBorderPreview, CustomSectorBorderTextBox.Text);
            UpdateColorPreviewBorder(CustomHighlightBgPreview, CustomHighlightBgTextBox.Text);
            UpdateColorPreviewBorder(CustomHighlightBorderPreview, CustomHighlightBorderTextBox.Text);
            UpdateColorPreviewBorder(CustomTextPreview, CustomTextTextBox.Text);
            if (HighlightGlowColorPreview != null && HighlightGlowColorTextBox != null)
            {
                UpdateColorPreviewBorder(HighlightGlowColorPreview, HighlightGlowColorTextBox.Text);
            }
        }

        private void UpdateColorPreviewBorder(Border border, string hex)
        {
            try
            {
                if (!string.IsNullOrEmpty(hex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    border.Background = new SolidColorBrush(color);
                }
                else
                {
                    border.Background = Brushes.Transparent;
                }
            }
            catch
            {
                border.Background = Brushes.Transparent;
            }
        }

        private void PickCustomColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string tag)
            {
                TextBox? targetBox = GetColorTextBoxByTag(tag);
                if (targetBox != null)
                {
                    var picked = _dialogs.ShowColorPicker(targetBox.Text);
                    if (picked != null)
                    {
                        targetBox.Text = picked.HexColor;
                    }
                }
            }
        }

        private void PickEyedropper_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string tag)
            {
                TextBox? targetBox = GetColorTextBoxByTag(tag);
                if (targetBox != null)
                {
                    var picked = _dialogs.ShowEyedropper();
                    if (picked != null)
                    {
                        targetBox.Text = picked.HexColor;
                    }
                }
            }
        }

        private TextBox? GetColorTextBoxByTag(string tag)
        {
            switch (tag)
            {
                case "CustomSectorBg": return CustomSectorBgTextBox;
                case "CustomSectorBorder": return CustomSectorBorderTextBox;
                case "CustomHighlightBg": return CustomHighlightBgTextBox;
                case "CustomHighlightBorder": return CustomHighlightBorderTextBox;
                case "CustomText": return CustomTextTextBox;
                case "HighlightGlowColor": return HighlightGlowColorTextBox;
                default: return null;
            }
        }

        private void AppThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;
            string selectedTheme = (AppThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
            ConfigManager.CurrentConfig.AppTheme = selectedTheme;
            _themeService.ApplyTheme(this, selectedTheme);
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
            SyncUiToConfigAndSave(true);
        }

        private void DisableOnFullScreenCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;
            ConfigManager.CurrentConfig.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
            SyncUiToConfigAndSave(true);
        }

        private void ModifierCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || ConfigManager.CurrentConfig == null) return;
            ConfigManager.CurrentConfig.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
            ConfigManager.CurrentConfig.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
            ConfigManager.CurrentConfig.DisableOnAlt = AltModifierCheckBox.IsChecked == true;
            SyncUiToConfigAndSave(true);
        }

        private void BrowseBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picked = _dialogs.ShowProgramPicker();
                if (picked != null)
                {
                    string fileName = System.IO.Path.GetFileName(picked.Path).ToLower();
                    AddBlacklistProcess(fileName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BrowseBlacklistButton_Click Error]: {ex}");
            }
        }

        private void AddBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            string proc = NewBlacklistProcessTextBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(proc))
            {
                // If empty, open ProgramPickerWindow to let user pick directly!
                BrowseBlacklistButton_Click(sender, e);
                return;
            }

            AddBlacklistProcess(proc);
        }

        private void NewBlacklistProcessTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                AddBlacklistButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BlacklistListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back)
            {
                DeleteBlacklistButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void AddBlacklistProcess(string proc)
        {
            if (string.IsNullOrWhiteSpace(proc)) return;
            proc = proc.Trim().ToLower();
            if (!proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                proc += ".exe";
            }

            if (!BlacklistListBox.Items.Contains(proc))
            {
                BlacklistListBox.Items.Add(proc);
                BlacklistListBox.SelectedItem = proc;
                BlacklistListBox.ScrollIntoView(proc);

                if (ConfigManager.CurrentConfig.BlacklistedProcesses == null)
                {
                    ConfigManager.CurrentConfig.BlacklistedProcesses = new List<string>();
                }
                if (!ConfigManager.CurrentConfig.BlacklistedProcesses.Contains(proc))
                {
                    ConfigManager.CurrentConfig.BlacklistedProcesses.Add(proc);
                }
                NewBlacklistProcessTextBox.Clear();
                SyncUiToConfigAndSave(true);
            }
            else
            {
                BlacklistListBox.SelectedItem = proc;
                BlacklistListBox.ScrollIntoView(proc);
            }
        }

        private void DeleteBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = BlacklistListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selected) && BlacklistListBox.Items.Count > 0)
            {
                selected = BlacklistListBox.Items[BlacklistListBox.Items.Count - 1]?.ToString();
            }

            if (!string.IsNullOrEmpty(selected))
            {
                BlacklistListBox.Items.Remove(selected);
                ConfigManager.CurrentConfig.BlacklistedProcesses?.Remove(selected);
                SyncUiToConfigAndSave(true);
            }
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool enable = AutoStartCheckBox.IsChecked == true;
            ConfigManager.SetAutoStart(enable);
            SyncUiToConfigAndSave(true);
        }

        /// <summary>Relaunches elevated, then exits through the composition root. Also used by the tray menu.</summary>
        public void ElevateAndRestart()
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinPieGestures.exe");
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(startInfo);
                _exitApplication();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"提权重启失败或已取消: {ex.Message}", "管理员提权", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ElevatePrivileges_Click(object sender, RoutedEventArgs e)
        {
            ElevateAndRestart();
        }

        private void ExportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var picked = _dialogs.ShowSaveFileDialog(
                "JSON 配置文件 (*.json)|*.json",
                $"WinPieGestures_Config_Backup_{DateTime.Now:yyyyMMdd}.json",
                "导出配置文件");

            if (picked != null)
            {
                bool success = ConfigManager.ExportConfig(picked.Path);
                if (success)
                {
                    MessageBox.Show("配置导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("配置导出失败，请检查写入权限。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var picked = _dialogs.ShowOpenFileDialog("JSON 配置文件 (*.json)|*.json", "选择要导入的配置文件");

            if (picked != null)
            {
                bool success = ConfigManager.ImportConfig(picked.Path);
                if (success)
                {
                    MessageBox.Show("配置导入成功！正在应用新设置...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Reload controls
                    _isUpdatingUi = true;
                    try
                    {
                        // T11：列表源换由 ProfileListViewModel 重建（迁移前为整表重挂 ItemsSource + Items.Refresh）。
                        _profileList.Reload(ConfigManager.CurrentConfig.Profiles);
                        if (_profileList.Profiles.Count > 0)
                        {
                            ProfilesListBox.SelectedIndex = 0;
                            // 迁移前 _selectedProfile 在导入后滞留旧配置对象（选中态与列表脱节）；
                            // 现显式选中第一个方案，使扇区数、槽位与预览和导入内容一致。
                            _profileList.SelectProfile(_profileList.Profiles[0]);
                            UpdateSectorCountRadios();
                        }

                        ThresholdSlider.Value = ConfigManager.CurrentConfig.DragThreshold;
                        SetComboBoxSelectedValue(ThemeComboBox, ConfigManager.CurrentConfig.Theme);
                        SetComboBoxSelectedValue(UiStyleComboBox, ConfigManager.CurrentConfig.UiStyle);
                        SetComboBoxSelectedValue(ShapeComboBox, ConfigManager.CurrentConfig.Shape);
                        SetComboBoxSelectedValue(IconLayoutModeComboBox, ConfigManager.CurrentConfig.IconLayoutMode);

                        WheelRadiusSlider.Value = ConfigManager.CurrentConfig.WheelRadius;
                        InnerRadiusSlider.Value = ConfigManager.CurrentConfig.InnerRadius;
                        CoreRadiusSlider.Value = ConfigManager.CurrentConfig.CoreRadius;
                        SectorGapSlider.Value = ConfigManager.CurrentConfig.SectorGap;
                        SectorCornerRadiusSlider.Value = ConfigManager.CurrentConfig.SectorCornerRadius;
                        SectorIconSizeSlider.Value = ConfigManager.CurrentConfig.SectorIconSize > 0 ? ConfigManager.CurrentConfig.SectorIconSize : 20.0;
                        SectorIconSizeLabel.Text = $"{SectorIconSizeSlider.Value:0} px";
                        SectorFontSizeSlider.Value = ConfigManager.CurrentConfig.SectorFontSize > 0 ? ConfigManager.CurrentConfig.SectorFontSize : 10.5;
                        SectorFontSizeLabel.Text = $"{SectorFontSizeSlider.Value:0.0} px";

                        ShowTextCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowText;
                    }
                    finally
                    {
                        _isUpdatingUi = false;
                    }

                    RenderLiveWheelPreview();
                }
                else
                {
                    MessageBox.Show("导入失败：文件格式不匹配或已损坏。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TrimMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            MemoryOptimizer.TrimMemory(true);
            MessageBox.Show(this, "物理工作集内存已深度压缩！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenReleasesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "releases");
                if (!Directory.Exists(path))
                {
                    path = AppDomain.CurrentDomain.BaseDirectory;
                }
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开目录: {ex.Message}");
            }
        }

        private void OpenAppFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开目录: {ex.Message}");
            }
        }

        private void OpenChangelogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
                if (File.Exists(changelogPath))
                {
                    Process.Start(new ProcessStartInfo(changelogPath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("CHANGELOG.md 文件位于根目录。", "提示");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SyncUiToConfigAndSave(true);
            MessageBox.Show("配置已成功保存至硬盘！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SlotViewModel vm)
            {
                ActionExecutor.Execute(vm.Action);
            }
        }

        private void SetComboBoxSelectedValue(System.Windows.Controls.ComboBox comboBox, string value)
        {
            if (comboBox == null || string.IsNullOrEmpty(value)) return;
            string mappedValue = value;
            if (value == "RoundedRect" || value == "FloatingCapsules" || value == "Capsule") mappedValue = "RoundedCapsule";
            if (value == "OrganicPetals" || value == "ArcTracker" || value == "LiquidDroplets" || value == "MinimalArc") mappedValue = "Original";

            foreach (System.Windows.Controls.ComboBoxItem item in comboBox.Items)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (string.Equals(tag, value, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(tag, mappedValue, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(id);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        #region 60FPS Live Preview Canvas Rendering

        private void RenderLiveWheelPreview()
        {
            if (_isRenderingPreview || LiveWheelPreviewCanvas == null || ConfigManager.CurrentConfig == null) return;
            _isRenderingPreview = true;

            try
            {
                LiveWheelPreviewCanvas.Children.Clear();
                _previewSectorPaths.Clear();
                _previewTransforms.Clear();
                _previewAngles.Clear();
                _lastHoveredSector = -2;

                double canvasSize = 300.0;
                double cx = canvasSize / 2.0;
                double cy = canvasSize / 2.0;

                double maxR = Math.Max(80.0, ConfigManager.CurrentConfig.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);

                double outerR = Math.Max(30.0, ConfigManager.CurrentConfig.WheelRadius * scale);
                double innerR = Math.Max(15.0, ConfigManager.CurrentConfig.InnerRadius * scale);
                double coreR = Math.Max(10.0, ConfigManager.CurrentConfig.CoreRadius * scale);
                double gap = Math.Max(0.0, ConfigManager.CurrentConfig.SectorGap * scale);
                double cornerRadius = Math.Max(0.0, ConfigManager.CurrentConfig.SectorCornerRadius * scale);

                if (innerR >= outerR) innerR = outerR * 0.5;
                if (coreR >= innerR) coreR = innerR * 0.8;

                string uiStyle = ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing";
                string theme = ConfigManager.CurrentConfig.Theme ?? "System";
                string shape = ConfigManager.CurrentConfig.Shape ?? "Original";
                string layoutMode = ConfigManager.CurrentConfig.IconLayoutMode ?? "IconAndText";
                bool showText = ConfigManager.CurrentConfig.ShowText && layoutMode != "IconOnly";

                _previewStyleRenderer = StyleRendererFactory.CreateRenderer(uiStyle);
                _previewStyleRenderer.Initialize(theme, ConfigManager.CurrentConfig, _themeService.IsWindowsInDarkTheme());
                _previewDefaultBrush = _previewStyleRenderer.DefaultSectorBrush;
                _previewHighlightBrush = _previewStyleRenderer.HighlightSectorBrush;
                _previewBorderBrush = _previewStyleRenderer.SectorBorderBrush;
                _previewHighlightBorderBrush = _previewStyleRenderer.HighlightBorderBrush;
                _previewTextBrush = _previewStyleRenderer.TextColorBrush;
                _previewCoreBgBrush = _previewStyleRenderer.CoreBgBrush;
                _previewCoreBorderBrush = _previewStyleRenderer.CoreBorderBrush;

                // Prepare preview core grid and render style decorations
                var previewCoreGrid = new Grid
                {
                    Width = coreR * 2.0,
                    Height = coreR * 2.0
                };
                _previewCoreCircle = new Ellipse
                {
                    Width = coreR * 2.0,
                    Height = coreR * 2.0,
                    Fill = _previewCoreBgBrush,
                    Stroke = _previewCoreBorderBrush,
                    StrokeThickness = 1.5
                };
                previewCoreGrid.Children.Add(_previewCoreCircle);

                double exitSize = Math.Max(12, coreR * 0.42);
                string coreType = ConfigManager.CurrentConfig.CoreIconType ?? "Exit";

                if (coreType == "Image")
                {
                    double imgSize = coreR * 1.6;
                    var coreImg = new System.Windows.Controls.Image
                    {
                        Width = imgSize,
                        Height = imgSize,
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Clip = new EllipseGeometry(new Point(imgSize / 2, imgSize / 2), imgSize / 2, imgSize / 2),
                        Visibility = (ConfigManager.CurrentConfig.ShowCoreIcon && ConfigManager.CurrentConfig.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomImagePath) && File.Exists(ConfigManager.CurrentConfig.CoreCustomImagePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(ConfigManager.CurrentConfig.CoreCustomImagePath, UriKind.Absolute);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            coreImg.Source = bmp;
                        }
                        catch { }
                    }
                    previewCoreGrid.Children.Add(coreImg);
                }
                else
                {
                    _previewExitIcon = new System.Windows.Shapes.Path
                    {
                        Name = "CoreExitIcon",
                        Data = IconHelper.GetCoreIconGeometry(
                            coreType,
                            ConfigManager.CurrentConfig.CoreCustomIconKey,
                            ConfigManager.CurrentConfig.CoreCustomIconSvg),
                        Fill = _previewTextBrush,
                        Width = exitSize,
                        Height = exitSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Visibility = (ConfigManager.CurrentConfig.ShowCoreIcon && ConfigManager.CurrentConfig.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    previewCoreGrid.Children.Add(_previewExitIcon);
                }

                _previewStyleRenderer.RenderDecorations(LiveWheelPreviewCanvas, previewCoreGrid, cx, cy, outerR, coreR, 1, ConfigManager.CurrentConfig.ShowCoreIcon);

                var profile = _profileList.SelectedProfile?.Model ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault() ?? new WheelProfile { SectorCount = 8, Actions = new List<ActionItem>() };
                int n = profile.SectorCount > 0 ? profile.SectorCount : 8;
                double sectorSize = 360.0 / n;

                // Draw sectors
                for (int i = 0; i < n; i++)
                {
                    double midAngle = i * sectorSize;
                    double startAngle = midAngle - (sectorSize / 2.0);
                    double endAngle = midAngle + (sectorSize / 2.0);
                    double midAngleRad = midAngle * (Math.PI / 180.0);

                    double layoutR = (innerR + outerR) / 2.0;
                    double lx = cx + Math.Cos(midAngleRad) * layoutR;
                    double ly = cy + Math.Sin(midAngleRad) * layoutR;

                    Geometry geom = IconHelper.CreateAdvancedSectorGeometry(
                        cx, cy, startAngle, endAngle, innerR, outerR, shape, gap, cornerRadius);

                    var transform = new TranslateTransform(0, 0);
                    var path = new System.Windows.Shapes.Path
                    {
                        Data = geom,
                        Fill = _previewDefaultBrush,
                        Stroke = _previewBorderBrush,
                        StrokeThickness = _previewStyleRenderer.BorderThickness,
                        RenderTransform = transform,
                        Tag = i
                    };

                    LiveWheelPreviewCanvas.Children.Add(path);
                    _previewStyleRenderer?.ApplySectorHighlight(path, false);
                    _previewSectorPaths.Add(path);
                    _previewTransforms.Add(transform);
                    _previewAngles.Add(midAngleRad);

                    // Add icon & mini text inside sector
                    var sp = new StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        RenderTransform = transform
                    };

                    string actionName = "";
                    string iconKey = "";
                    string actionType = "Hotkey";
                    string parameter = "";

                    if (profile.Actions != null && i < profile.Actions.Count && profile.Actions[i] != null)
                    {
                        actionName = profile.Actions[i].Name ?? "";
                        iconKey = profile.Actions[i].IconKey ?? "";
                        actionType = profile.Actions[i].Type ?? "Hotkey";
                        parameter = profile.Actions[i].Parameter ?? "";
                    }

                    if (layoutMode != "TextOnly")
                    {
                        string? customSvg = (profile.Actions != null && i < profile.Actions.Count) ? profile.Actions[i]?.CustomIconSvg : null;
                        string? svgData = null;

                        IconHelper.CustomIconItem? customItem = null;
                        if (!string.IsNullOrEmpty(iconKey) && iconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                        {
                            customItem = IconHelper.GetCustomIcons().FirstOrDefault(c => c.Key == iconKey);
                        }

                        if (!string.IsNullOrEmpty(customSvg)) svgData = customSvg;
                        else if (customItem != null && customItem.IsSvg) svgData = customItem.SvgData;
                        else if (!string.IsNullOrEmpty(iconKey) && customItem == null) svgData = IconHelper.GetSvgPathByKey(iconKey);
                        else if (actionType == "Folder" || actionType == "OpenFolder") svgData = IconHelper.GetSvgPathByKey("Folder");
                        else if (actionType == "System" && !string.IsNullOrEmpty(parameter)) svgData = IconHelper.GetSvgPathByKey(parameter);

                        double configuredIconSize = ConfigManager.CurrentConfig.SectorIconSize > 0 ? ConfigManager.CurrentConfig.SectorIconSize : 20.0;
                        double scaleFactor = n == 12 ? 0.80 : (n == 4 ? 1.20 : 1.0);
                        double previewIconSize = ((layoutMode == "IconOnly") ? configuredIconSize * 1.35 : configuredIconSize) * 0.72 * scaleFactor;

                        if (!string.IsNullOrEmpty(svgData))
                        {
                            try
                            {
                                var iconPath = new System.Windows.Shapes.Path
                                {
                                    Data = Geometry.Parse(svgData),
                                    Fill = _previewTextBrush,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(iconPath);
                            }
                            catch { }
                        }
                        else if (customItem != null && !customItem.IsSvg)
                        {
                            var iconSrc = IconHelper.GetCustomImageSource(customItem.FilePath);
                            if (iconSrc != null)
                            {
                                var img = new System.Windows.Controls.Image
                                {
                                    Source = iconSrc,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(img);
                            }
                        }
                        else if (actionType == "Launch" && !string.IsNullOrEmpty(parameter))
                        {
                            var iconSrc = IconHelper.GetIcon(parameter);
                            if (iconSrc != null)
                            {
                                var img = new System.Windows.Controls.Image
                                {
                                    Source = iconSrc,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(img);
                            }
                        }
                        else
                        {
                            // Hotkey keyboard fallback icon
                            try
                            {
                                var iconPath = new System.Windows.Shapes.Path
                                {
                                    Data = Geometry.Parse("M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z"),
                                    Fill = _previewTextBrush,
                                    Width = previewIconSize,
                                    Height = previewIconSize,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0, 0, showText ? 1 : 0)
                                };
                                sp.Children.Add(iconPath);
                            }
                            catch { }
                        }
                    }

                    if (showText && !string.IsNullOrEmpty(actionName))
                    {
                        double baseFontSize = ConfigManager.CurrentConfig.SectorFontSize > 0 ? ConfigManager.CurrentConfig.SectorFontSize : 10.5;
                        double scaleFactor = n == 12 ? 0.80 : (n == 4 ? 1.20 : 1.0);
                        double previewFs = ((layoutMode == "TextOnly") ? baseFontSize + 1.0 : baseFontSize) * 0.85 * scaleFactor;
                        double textMaxW = n == 12 ? 44.0 : (n == 4 ? 76.0 : 64.0);

                        var tb = new TextBlock
                        {
                            Text = actionName,
                            FontSize = Math.Max(6.5, previewFs),
                            Foreground = _previewTextBrush,
                            FontWeight = FontWeights.Medium,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxWidth = textMaxW
                        };
                        sp.Children.Add(tb);
                    }

                    double containerW = n == 12 ? 46.0 : (n == 4 ? 76.0 : 60.0);
                    double containerH = n == 12 ? 38.0 : (n == 4 ? 54.0 : 44.0);

                    var container = new Grid
                    {
                        Width = containerW,
                        Height = containerH,
                        IsHitTestVisible = false,
                        RenderTransform = transform
                    };
                    container.Children.Add(sp);
                    Canvas.SetLeft(container, lx - containerW / 2.0);
                    Canvas.SetTop(container, ly - containerH / 2.0);
                    System.Windows.Controls.Panel.SetZIndex(container, 10);
                    LiveWheelPreviewCanvas.Children.Add(container);
                }

                // Position and add preview core grid
                Canvas.SetLeft(previewCoreGrid, cx - coreR);
                Canvas.SetTop(previewCoreGrid, cy - coreR);
                System.Windows.Controls.Panel.SetZIndex(previewCoreGrid, 15);
                LiveWheelPreviewCanvas.Children.Add(previewCoreGrid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderLiveWheelPreview Error]: {ex}");
            }
            finally
            {
                _isRenderingPreview = false;
            }
        }

        private static Stretch ParseStretchMode(string? stretch)
        {
            if (string.Equals(stretch, "Uniform", StringComparison.OrdinalIgnoreCase)) return Stretch.Uniform;
            if (string.Equals(stretch, "Fill", StringComparison.OrdinalIgnoreCase)) return Stretch.Fill;
            if (string.Equals(stretch, "None", StringComparison.OrdinalIgnoreCase)) return Stretch.None;
            return Stretch.UniformToFill;
        }

        private void LiveWheelPreviewCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_previewSectorPaths.Count == 0 || ConfigManager.CurrentConfig == null) return;

            try
            {
                Point p = e.GetPosition(LiveWheelPreviewCanvas);
                double dx = p.X - 150.0;
                double dy = p.Y - 150.0;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                double maxR = Math.Max(80.0, ConfigManager.CurrentConfig.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);
                double outerR = ConfigManager.CurrentConfig.WheelRadius * scale;
                double innerR = ConfigManager.CurrentConfig.InnerRadius * scale;
                double coreR = ConfigManager.CurrentConfig.CoreRadius * scale;

                int hoveredIndex = -2;

                if (dist <= coreR)
                {
                    hoveredIndex = -1; // Core hovered
                }
                else if (dist >= innerR * 0.75 && dist <= outerR * 1.2)
                {
                    double angleDeg = (Math.Atan2(dy, dx) * (180.0 / Math.PI) + 360.0) % 360.0;
                    double sectorSize = 360.0 / _previewSectorPaths.Count;
                    hoveredIndex = (int)Math.Round(angleDeg / sectorSize) % _previewSectorPaths.Count;
                }

                if (hoveredIndex == _lastHoveredSector) return;
                _lastHoveredSector = hoveredIndex;

                // Update sector highlights & magnetic pop-out
                for (int i = 0; i < _previewSectorPaths.Count; i++)
                {
                    var path = _previewSectorPaths[i];
                    var trans = _previewTransforms[i];
                    double angleRad = _previewAngles[i];

                    if (i == hoveredIndex)
                    {
                        path.Fill = _previewHighlightBrush;
                        path.Stroke = _previewHighlightBorderBrush;
                        path.StrokeThickness = (_previewStyleRenderer?.HighlightBorderThickness ?? 2.0);
                        _previewStyleRenderer?.ApplySectorHighlight(path, true);

                        // Pop out
                        trans.X = Math.Cos(angleRad) * 4.5;
                        trans.Y = Math.Sin(angleRad) * 4.5;
                    }
                    else
                    {
                        path.Fill = _previewDefaultBrush;
                        path.Stroke = _previewBorderBrush;
                        path.StrokeThickness = (_previewStyleRenderer?.BorderThickness ?? 1.5);
                        _previewStyleRenderer?.ApplySectorHighlight(path, false);

                        trans.X = 0;
                        trans.Y = 0;
                    }
                }

                // Core highlight
                if (_previewCoreCircle != null)
                {
                    if (hoveredIndex == -1)
                    {
                        _previewCoreCircle.Fill = new SolidColorBrush(Color.FromArgb(220, 244, 63, 94));
                        if (_previewExitIcon != null)
                        {
                            _previewExitIcon.Fill = Brushes.White;
                            _previewStyleRenderer?.ApplyExitHighlight(_previewExitIcon, true);
                        }
                    }
                    else
                    {
                        _previewCoreCircle.Fill = _previewCoreBgBrush;
                        if (_previewExitIcon != null)
                        {
                            _previewExitIcon.Fill = _previewTextBrush;
                            _previewStyleRenderer?.ApplyExitHighlight(_previewExitIcon, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Canvas MouseMove Error]: {ex}");
            }
        }

        private void LiveWheelPreviewCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                _lastHoveredSector = -2;
                for (int i = 0; i < _previewSectorPaths.Count; i++)
                {
                    var path = _previewSectorPaths[i];
                    var trans = _previewTransforms[i];

                    path.Fill = _previewDefaultBrush;
                    path.Stroke = _previewBorderBrush;
                    path.StrokeThickness = (_previewStyleRenderer?.BorderThickness ?? 1.5);
                    _previewStyleRenderer?.ApplySectorHighlight(path, false);
                    trans.X = 0;
                    trans.Y = 0;
                }

                if (_previewCoreCircle != null) _previewCoreCircle.Fill = _previewCoreBgBrush;
                if (_previewExitIcon != null) _previewExitIcon.Fill = _previewTextBrush;
            }
            catch { }
        }

        #endregion
    }
}
