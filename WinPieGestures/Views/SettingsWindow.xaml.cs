using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly AppearanceSettingsViewModel _appearanceVm;
        private WheelProfile? _selectedProfile;
        private readonly ObservableCollection<SlotViewModel> _slotViewModels = new ObservableCollection<SlotViewModel>();

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

        // Direction Labels
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

        public SettingsWindow(IThemeService themeService, IDialogService dialogs, Action exitApplication, Action<string, string> showTrayBalloonTip)
        {
            InitializeComponent();
            _themeService = themeService;
            _exitApplication = exitApplication;
            _showTrayBalloonTip = showTrayBalloonTip;
            _dialogs = dialogs;

            // 外观分区子 ViewModel (T10)：状态与编排住 VM，绘制（实时预览）留在本视图层
            _appearanceVm = new AppearanceSettingsViewModel(ConfigManager.ConfigService, _dialogs);
            AppearanceSettingsGrid.DataContext = _appearanceVm;
            _appearanceVm.PreviewInvalidated += OnAppearancePreviewInvalidated;
            _appearanceVm.AutoSaveRequested += ScheduleAutoSave;
            _appearanceVm.SaveNowRequested += () => SyncUiToConfigAndSave(true);
            _appearanceVm.PresetListChanged += SyncThemePresetItems;
            _appearanceVm.DeleteConfirmRequested += preset =>
            {
                var result = MessageBox.Show(this, $"确定要删除自定义配色方案预设【{preset.Name}】吗？", "确认删除配色方案", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _appearanceVm.ConfirmDeleteCustomColorPreset(preset);
                }
            };
            _appearanceVm.PresetDeleted += name =>
                MessageBox.Show(this, $"自定义配色方案【{name}】已成功删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            _appearanceVm.PresetSaved += name =>
                MessageBox.Show($"配色预设【{name}】已成功保存！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            SyncThemePresetItems();

            _isUpdatingUi = true;
            try
            {
                // Load profiles to listbox
                ProfilesListBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
                ThresholdSlider.Value = ConfigManager.CurrentConfig.DragThreshold;
                ThresholdValueLabel.Text = ConfigManager.CurrentConfig.DragThreshold.ToString("0");

                // Load App Interface Theme
                SetComboBoxSelectedValue(AppThemeComboBox, ConfigManager.CurrentConfig.AppTheme ?? "System");
                _themeService.ApplyTheme(this, ConfigManager.CurrentConfig.AppTheme ?? "System");

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

                SlotsItemsControl.ItemsSource = _slotViewModels;

                // Check UAC privileges and show warning if not elevated
                bool isAdmin = IsRunningAsAdmin();
                UacWarningCard.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

                // Select default profile
                _selectedProfile = ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
                if (_selectedProfile != null)
                {
                    ProfilesListBox.SelectedItem = _selectedProfile;
                    if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
                    if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
                    if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;
                    RefreshSlots();
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
                if (_selectedProfile == null && ConfigManager.CurrentConfig.Profiles.Count > 0)
                {
                    _selectedProfile = ConfigManager.CurrentConfig.Profiles[0];
                    if (ProfilesListBox != null) ProfilesListBox.SelectedItem = _selectedProfile;
                }
                if (_selectedProfile != null)
                {
                    _isUpdatingUi = true;
                    try
                    {
                        if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
                        if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
                        if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;
                        RefreshSlots();
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
                // 外观分区（T10）各设置项已由 AppearanceSettingsViewModel 即时写穿配置，此处不再从控件回读。

                if (AppThemeComboBox?.SelectedItem is ComboBoxItem appThemeItem)
                {
                    ConfigManager.CurrentConfig.AppTheme = appThemeItem.Tag?.ToString() ?? "System";
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

                if (ThresholdSlider != null) ConfigManager.CurrentConfig.DragThreshold = ThresholdSlider.Value;

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

            _selectedProfile = ProfilesListBox.SelectedItem as WheelProfile;
            if (_selectedProfile == null) return;

            _isUpdatingUi = true;
            try
            {
                // Update radio buttons
                if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
                if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
                if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;

                RefreshSlots();
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

        private void RefreshSlots()
        {
            try
            {
                _slotViewModels.Clear();
                if (_selectedProfile == null)
                {
                    _selectedProfile = ProfilesListBox?.SelectedItem as WheelProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
                }
                if (_selectedProfile == null) return;

                int count = _selectedProfile.SectorCount;
                if (count != 4 && count != 8 && count != 12) count = 8;

                string[] directions = count switch
                {
                    4 => Directions4,
                    12 => Directions12,
                    _ => Directions8
                };

                if (_selectedProfile.Actions == null)
                {
                    _selectedProfile.Actions = new List<ActionItem>();
                }

                while (_selectedProfile.Actions.Count < count)
                {
                    int idx = _selectedProfile.Actions.Count;
                    if (count == 12 && idx < DefaultPresets12.Length)
                    {
                        var p = DefaultPresets12[idx];
                        _selectedProfile.Actions.Add(new ActionItem { Type = p.Type, Name = p.Name, Parameter = p.Parameter, IconKey = p.IconKey });
                    }
                    else if (count == 4 && idx < DefaultPresets4.Length)
                    {
                        var p = DefaultPresets4[idx];
                        _selectedProfile.Actions.Add(new ActionItem { Type = p.Type, Name = p.Name, Parameter = p.Parameter, IconKey = p.IconKey });
                    }
                    else
                    {
                        _selectedProfile.Actions.Add(new ActionItem { Type = "Hotkey", Name = $"快捷动作 {idx + 1}", Parameter = "" });
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    var action = _selectedProfile.Actions[i];
                    var vm = new SlotViewModel(directions[i], action);
                    _slotViewModels.Add(vm);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RefreshSlots Error]: {ex}");
            }
        }

        private void SectorCountRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;

            if (_selectedProfile == null)
            {
                _selectedProfile = ProfilesListBox?.SelectedItem as WheelProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
            }
            if (_selectedProfile == null) return;

            int newCount = 8;
            if (SectorCount4Radio?.IsChecked == true) newCount = 4;
            else if (SectorCount8Radio?.IsChecked == true) newCount = 8;
            else if (SectorCount12Radio?.IsChecked == true) newCount = 12;

            _selectedProfile.SectorCount = newCount;
            RefreshSlots();

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

                int currentSectorCount = _selectedProfile?.SectorCount ?? 8;
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

                ConfigManager.CurrentConfig.Profiles.Add(newProfile);
                ProfilesListBox.Items.Refresh();
                ProfilesListBox.SelectedItem = newProfile;
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
                int currentSectorCount = _selectedProfile?.SectorCount ?? 8;
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

                ConfigManager.CurrentConfig.Profiles.Add(newProfile);
                ProfilesListBox.Items.Refresh();
                ProfilesListBox.SelectedItem = newProfile;
                SyncUiToConfigAndSave(true);
            }
        }

        private void RenameProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null)
            {
                MessageBox.Show("请先在列表中选择要重命名的配置方案！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedProfile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("「Global」为系统全局默认基础配置，不可重命名。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string oldName = _selectedProfile.ProcessName;
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
                _selectedProfile.ProcessName = result.Text;
                ProfilesListBox.Items.Refresh();
                SyncUiToConfigAndSave(true);
            }
        }

        private void ProfilesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is WheelProfile profile && !profile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                RenameProfileButton_Click(sender, e);
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null) return;

            if (_selectedProfile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("全局默认配置 (Global) 不能删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"确定要删除配置方案 [{_selectedProfile.ProcessName}] 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ConfigManager.CurrentConfig.Profiles.Remove(_selectedProfile);
                ProfilesListBox.Items.Refresh();
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

        /// <summary>按 VM 的预设列表重建配色方案下拉的动态项（CustomPreset_*），并恢复当前选中 Tag（视图层条目管理，数据来自 VM）。</summary>
        private void SyncThemePresetItems()
        {
            if (ThemeComboBox == null) return;

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

            foreach (var preset in _appearanceVm.CustomPresets)
            {
                ThemeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"🎨 {preset.Name} (自定义预设)",
                    Tag = "CustomPreset_" + preset.Id
                });
            }

            SetComboBoxSelectedValue(ThemeComboBox, _appearanceVm.SelectedTheme);
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

        private void PickIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SlotViewModel vm)
            {
                var picked = _dialogs.ShowIconPicker(vm.IconKey);
                if (picked != null)
                {
                    vm.IconKey = picked.IconKey ?? "";
                    if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                    {
                        RenderLiveWheelPreview();
                    }
                }
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
                        ProfilesListBox.ItemsSource = null;
                        ProfilesListBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
                        ProfilesListBox.SelectedIndex = 0;

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

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SlotViewModel vm)
            {
                var picked = _dialogs.ShowProgramPicker();
                if (picked != null)
                {
                    vm.Parameter = picked.Path;
                    if (string.IsNullOrEmpty(vm.Name) || vm.Name.StartsWith("动作") || vm.Name == "快捷动作")
                    {
                        vm.Name = !string.IsNullOrEmpty(picked.Name) ? picked.Name : Path.GetFileNameWithoutExtension(picked.Path);
                    }
                }
            }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SlotViewModel vm)
            {
                try
                {
                    var dialog = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = I18n.T("BtnBrowseFolder"),
                        Multiselect = false
                    };
                    if (!string.IsNullOrWhiteSpace(vm.Parameter) && System.IO.Directory.Exists(vm.Parameter))
                    {
                        dialog.InitialDirectory = vm.Parameter;
                    }

                    if (dialog.ShowDialog(this) == true)
                    {
                        string selectedFolder = dialog.FolderName;
                        if (!string.IsNullOrEmpty(selectedFolder))
                        {
                            vm.Parameter = selectedFolder;
                            if (string.IsNullOrEmpty(vm.Name) || vm.Name.StartsWith("快捷动作") || vm.Name.StartsWith("动作") || vm.Name == "打开文件夹")
                            {
                                var dirInfo = new System.IO.DirectoryInfo(selectedFolder);
                                vm.Name = dirInfo.Name;
                            }
                            if (string.IsNullOrEmpty(vm.IconKey))
                            {
                                vm.IconKey = "Folder";
                            }
                            SyncUiToConfigAndSave();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BrowseFolder_Click Error]: {ex}");
                }
            }
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

        /// <summary>外观设置变化（VM PreviewInvalidated）→ 分区可见时重绘实时预览。</summary>
        private void OnAppearancePreviewInvalidated()
        {
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
        }

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

                // 预览输入（当前外观设置值）从外观子 ViewModel 读取；绘制逻辑留在视图层。
                // 渲染器 Initialize 仍传 AppConfig：VM 已即时写穿，两者恒等。
                double canvasSize = 300.0;
                double cx = canvasSize / 2.0;
                double cy = canvasSize / 2.0;

                double maxR = Math.Max(80.0, _appearanceVm.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);

                double outerR = Math.Max(30.0, _appearanceVm.WheelRadius * scale);
                double innerR = Math.Max(15.0, _appearanceVm.InnerRadius * scale);
                double coreR = Math.Max(10.0, _appearanceVm.CoreRadius * scale);
                double gap = Math.Max(0.0, _appearanceVm.SectorGap * scale);
                double cornerRadius = Math.Max(0.0, _appearanceVm.SectorCornerRadius * scale);

                if (innerR >= outerR) innerR = outerR * 0.5;
                if (coreR >= innerR) coreR = innerR * 0.8;

                string uiStyle = _appearanceVm.UiStyle ?? "ClassicRing";
                string theme = _appearanceVm.SelectedTheme ?? "System";
                string shape = _appearanceVm.Shape ?? "Original";
                string layoutMode = _appearanceVm.IconLayoutMode ?? "IconAndText";
                bool showText = _appearanceVm.ShowText && layoutMode != "IconOnly";

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

                var profile = _selectedProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault() ?? new WheelProfile { SectorCount = 8, Actions = new List<ActionItem>() };
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

                        double configuredIconSize = _appearanceVm.SectorIconSize > 0 ? _appearanceVm.SectorIconSize : 20.0;
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
                        double baseFontSize = _appearanceVm.SectorFontSize > 0 ? _appearanceVm.SectorFontSize : 10.5;
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

                double maxR = Math.Max(80.0, _appearanceVm.WheelRadius);
                double scale = 135.0 / Math.Max(135.0, maxR);
                double outerR = _appearanceVm.WheelRadius * scale;
                double innerR = _appearanceVm.InnerRadius * scale;
                double coreR = _appearanceVm.CoreRadius * scale;

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

    public class SystemPresetItem
    {
        public string Key { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DefaultName { get; set; } = "";
        public string DefaultIconKey { get; set; } = "";
        public string FormattedDisplay => $"[{Category}] {DisplayName}";
    }

    public class SlotViewModel : INotifyPropertyChanged
    {
        public static readonly List<SystemPresetItem> SystemPresetList = new List<SystemPresetItem>
        {
            // 窗口与工作区
            new SystemPresetItem { Key = "CloseWindow", Category = "窗口管理", DisplayName = "关闭当前窗口 (Close / Alt+F4)", DefaultName = "关闭窗口", DefaultIconKey = "CloseWindow" },
            new SystemPresetItem { Key = "Minimize", Category = "窗口管理", DisplayName = "最小化窗口 (Minimize / Win+Down)", DefaultName = "最小化", DefaultIconKey = "Minimize" },
            new SystemPresetItem { Key = "Maximize", Category = "窗口管理", DisplayName = "最大化/还原 (Maximize / Win+Up)", DefaultName = "最大化", DefaultIconKey = "Maximize" },
            new SystemPresetItem { Key = "SnapLeft", Category = "窗口管理", DisplayName = "左半屏贴靠 (Snap Left / Win+Left)", DefaultName = "靠左分屏", DefaultIconKey = "SnapLeft" },
            new SystemPresetItem { Key = "SnapRight", Category = "窗口管理", DisplayName = "右半屏贴靠 (Snap Right / Win+Right)", DefaultName = "靠右分屏", DefaultIconKey = "SnapRight" },
            new SystemPresetItem { Key = "TaskView", Category = "窗口管理", DisplayName = "任务视图/多任务 (Task View / Win+Tab)", DefaultName = "任务视图", DefaultIconKey = "TaskView" },
            new SystemPresetItem { Key = "PrevDesktop", Category = "窗口管理", DisplayName = "上一虚拟桌面 (Prev Desktop)", DefaultName = "上一桌面", DefaultIconKey = "PrevDesktop" },
            new SystemPresetItem { Key = "NextDesktop", Category = "窗口管理", DisplayName = "下一虚拟桌面 (Next Desktop)", DefaultName = "下一桌面", DefaultIconKey = "NextDesktop" },
            new SystemPresetItem { Key = "ShowDesktop", Category = "窗口管理", DisplayName = "显示桌面 (Desktop / Win+D)", DefaultName = "显示桌面", DefaultIconKey = "ShowDesktop" },
            new SystemPresetItem { Key = "FullScreen", Category = "窗口管理", DisplayName = "全屏切换 (Full Screen / F11)", DefaultName = "全屏切换", DefaultIconKey = "FullScreen" },
            new SystemPresetItem { Key = "Screenshot", Category = "窗口管理", DisplayName = "屏幕截图 (Screenshot / Win+Shift+S)", DefaultName = "屏幕截图", DefaultIconKey = "Screenshot" },

            // 系统管理与实用工具
            new SystemPresetItem { Key = "TaskManager", Category = "系统工具", DisplayName = "任务管理器 (Task Manager / Ctrl+Shift+Esc)", DefaultName = "任务管理器", DefaultIconKey = "TaskManager" },
            new SystemPresetItem { Key = "Explorer", Category = "系统工具", DisplayName = "文件资源管理器 (Explorer / Win+E)", DefaultName = "资源管理器", DefaultIconKey = "Explorer" },
            new SystemPresetItem { Key = "Settings", Category = "系统工具", DisplayName = "Windows 设置 (Settings / Win+I)", DefaultName = "系统设置", DefaultIconKey = "Settings" },
            new SystemPresetItem { Key = "Calculator", Category = "系统工具", DisplayName = "计算器 (Calculator / calc.exe)", DefaultName = "计算器", DefaultIconKey = "Calculator" },
            new SystemPresetItem { Key = "RunDialog", Category = "系统工具", DisplayName = "运行窗口 (Run / Win+R)", DefaultName = "运行", DefaultIconKey = "RunDialog" },
            new SystemPresetItem { Key = "WindowsSearch", Category = "系统工具", DisplayName = "系统搜索 (Search / Win+S)", DefaultName = "搜索", DefaultIconKey = "WindowsSearch" },
            new SystemPresetItem { Key = "ClipboardHistory", Category = "系统工具", DisplayName = "剪贴板历史 (Clipboard / Win+V)", DefaultName = "剪贴板", DefaultIconKey = "ClipboardHistory" },
            new SystemPresetItem { Key = "Lock", Category = "系统工具", DisplayName = "锁定电脑 (Lock Workstation)", DefaultName = "锁定电脑", DefaultIconKey = "Lock" },

            // 多媒体与音量
            new SystemPresetItem { Key = "VolumeUp", Category = "媒体音效", DisplayName = "音量增加 (Volume Up)", DefaultName = "音量加", DefaultIconKey = "VolumeUp" },
            new SystemPresetItem { Key = "VolumeDown", Category = "媒体音效", DisplayName = "音量减小 (Volume Down)", DefaultName = "音量减", DefaultIconKey = "VolumeDown" },
            new SystemPresetItem { Key = "VolumeMute", Category = "媒体音效", DisplayName = "静音切换 (Mute)", DefaultName = "静音切换", DefaultIconKey = "VolumeMute" },
            new SystemPresetItem { Key = "PlayPause", Category = "媒体音效", DisplayName = "播放/暂停 (Play/Pause)", DefaultName = "播放/暂停", DefaultIconKey = "PlayPause" },
            new SystemPresetItem { Key = "NextTrack", Category = "媒体音效", DisplayName = "下一曲 (Next Track)", DefaultName = "下一曲", DefaultIconKey = "NextTrack" },
            new SystemPresetItem { Key = "PrevTrack", Category = "媒体音效", DisplayName = "上一曲 (Previous Track)", DefaultName = "上一曲", DefaultIconKey = "PrevTrack" },
            new SystemPresetItem { Key = "StopMedia", Category = "媒体音效", DisplayName = "停止播放 (Stop)", DefaultName = "停止", DefaultIconKey = "VolumeMute" },

            // 浏览器与文档
            new SystemPresetItem { Key = "NewTab", Category = "网页浏览", DisplayName = "新建标签页 (New Tab / Ctrl+T)", DefaultName = "新建标签", DefaultIconKey = "NewTab" },
            new SystemPresetItem { Key = "CloseTab", Category = "网页浏览", DisplayName = "关闭标签页 (Close Tab / Ctrl+W)", DefaultName = "关闭标签", DefaultIconKey = "CloseTab" },
            new SystemPresetItem { Key = "ReopenTab", Category = "网页浏览", DisplayName = "恢复关闭标签 (Reopen / Ctrl+Shift+T)", DefaultName = "恢复标签", DefaultIconKey = "ReopenTab" },
            new SystemPresetItem { Key = "Refresh", Category = "网页浏览", DisplayName = "刷新页面 (Refresh / F5)", DefaultName = "刷新", DefaultIconKey = "Refresh" },
            new SystemPresetItem { Key = "HardRefresh", Category = "网页浏览", DisplayName = "强制刷新 (Hard Refresh / Ctrl+F5)", DefaultName = "强制刷新", DefaultIconKey = "Refresh" },
            new SystemPresetItem { Key = "ZoomIn", Category = "网页浏览", DisplayName = "页面放大 (Zoom In / Ctrl++)", DefaultName = "放大", DefaultIconKey = "ZoomIn" },
            new SystemPresetItem { Key = "ZoomOut", Category = "网页浏览", DisplayName = "页面缩小 (Zoom Out / Ctrl+-)", DefaultName = "缩小", DefaultIconKey = "ZoomOut" },
            new SystemPresetItem { Key = "ZoomReset", Category = "网页浏览", DisplayName = "默认缩放 (Reset Zoom / Ctrl+0)", DefaultName = "默认缩放", DefaultIconKey = "ZoomReset" },

            // 电源管理
            new SystemPresetItem { Key = "Sleep", Category = "电源控制", DisplayName = "系统睡眠 (Sleep)", DefaultName = "睡眠", DefaultIconKey = "Sleep" },
            new SystemPresetItem { Key = "Restart", Category = "电源控制", DisplayName = "重启电脑 (Restart)", DefaultName = "重启", DefaultIconKey = "Restart" },
            new SystemPresetItem { Key = "Shutdown", Category = "电源控制", DisplayName = "关闭电脑 (Shutdown)", DefaultName = "关机", DefaultIconKey = "Shutdown" }
        };

        public static readonly Dictionary<string, string> SystemPresets = SystemPresetList.ToDictionary(x => x.Key, x => x.FormattedDisplay);

        public string DirectionLabel { get; }
        public ActionItem Action { get; }

        public string Name
        {
            get => Action.Name ?? "";
            set
            {
                if (Action.Name != value)
                {
                    Action.Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Type
        {
            get => string.IsNullOrEmpty(Action.Type) ? "Hotkey" : Action.Type;
            set
            {
                if (Action.Type != value && !string.IsNullOrEmpty(value))
                {
                    Action.Type = value;
                    if ((value == "Folder" || value == "OpenFolder") && string.IsNullOrEmpty(IconKey))
                    {
                        IconKey = "Folder";
                        if (string.IsNullOrEmpty(Name) || Name.StartsWith("快捷动作") || Name.StartsWith("动作"))
                        {
                            Name = I18n.T("ActionTypeFolderShort");
                        }
                    }
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(IsHotkeyType));
                    OnPropertyChanged(nameof(IsLaunchType));
                    OnPropertyChanged(nameof(IsFolderType));
                    OnPropertyChanged(nameof(IsSystemType));
                }
            }
        }

        public string Parameter
        {
            get => Action.Parameter ?? "";
            set
            {
                if (Action.Parameter != value)
                {
                    Action.Parameter = value;
                    OnPropertyChanged(nameof(Parameter));
                }
            }
        }

        public string Arguments
        {
            get => Action.Arguments ?? "";
            set
            {
                if (Action.Arguments != value)
                {
                    Action.Arguments = value;
                    OnPropertyChanged(nameof(Arguments));
                }
            }
        }

        public string IconKey
        {
            get => Action.IconKey ?? "";
            set
            {
                if (Action.IconKey != value)
                {
                    Action.IconKey = value;
                    OnPropertyChanged(nameof(IconKey));
                    OnPropertyChanged(nameof(IconDisplayText));
                    OnPropertyChanged(nameof(HasVectorIcon));
                    OnPropertyChanged(nameof(VectorIconData));
                }
            }
        }

        public string CustomIconSvg
        {
            get => Action.CustomIconSvg ?? "";
            set
            {
                if (Action.CustomIconSvg != value)
                {
                    Action.CustomIconSvg = value;
                    OnPropertyChanged(nameof(CustomIconSvg));
                    OnPropertyChanged(nameof(IconDisplayText));
                    OnPropertyChanged(nameof(HasVectorIcon));
                    OnPropertyChanged(nameof(VectorIconData));
                }
            }
        }

        public string IconDisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(IconKey)) return IconKey;
                if (!string.IsNullOrEmpty(CustomIconSvg)) return "自定义SVG";
                return "图标...";
            }
        }

        public bool HasVectorIcon => VectorIconData != null;

        public Geometry? VectorIconData
        {
            get
            {
                string? data = null;
                if (!string.IsNullOrEmpty(CustomIconSvg)) data = CustomIconSvg;
                else if (!string.IsNullOrEmpty(IconKey))
                {
                    if (IconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                    {
                        var custom = IconHelper.GetCustomIcons().FirstOrDefault(c => c.Key == IconKey);
                        if (custom != null && custom.IsSvg) data = custom.SvgData;
                    }
                    else
                    {
                        data = IconHelper.GetSvgPathByKey(IconKey);
                    }
                }
                
                if (!string.IsNullOrEmpty(data))
                {
                    try
                    {
                        return Geometry.Parse(data);
                    }
                    catch { }
                }
                return null;
            }
        }

        public string SelectedSystemPreset
        {
            get => Action.Type == "System" ? (Action.Parameter ?? "Lock") : "Lock";
            set
            {
                if (Action.Parameter != value && !string.IsNullOrEmpty(value))
                {
                    Action.Parameter = value;
                    OnPropertyChanged(nameof(SelectedSystemPreset));
                    OnPropertyChanged(nameof(Parameter));

                    // Auto associate default friendly name and icon if matching
                    var preset = SystemPresetList.FirstOrDefault(x => string.Equals(x.Key, value, StringComparison.OrdinalIgnoreCase));
                    if (preset != null)
                    {
                        if (string.IsNullOrEmpty(Name) || Name == "快捷动作" || SystemPresetList.Any(p => p.DefaultName == Name))
                        {
                            Name = preset.DefaultName;
                        }
                        if (string.IsNullOrEmpty(IconKey) || SystemPresetList.Any(p => p.DefaultIconKey == IconKey))
                        {
                            IconKey = preset.DefaultIconKey;
                        }
                    }
                }
            }
        }

        public bool IsHotkeyType => Type == "Hotkey";
        public bool IsLaunchType => Type == "Launch";
        public bool IsFolderType => Type == "Folder" || Type == "OpenFolder";
        public bool IsSystemType => Type == "System";

        public class ActionTypeOption
        {
            public string Tag { get; set; } = "";
            public string DisplayText { get; set; } = "";
        }

        public List<ActionTypeOption> ActionTypes => new List<ActionTypeOption>
        {
            new ActionTypeOption { Tag = "Hotkey", DisplayText = I18n.T("ActionTypeHotkeyShort") },
            new ActionTypeOption { Tag = "Launch", DisplayText = I18n.T("ActionTypeLaunchShort") },
            new ActionTypeOption { Tag = "Folder", DisplayText = I18n.T("ActionTypeFolderShort") },
            new ActionTypeOption { Tag = "System", DisplayText = I18n.T("ActionTypeSystemShort") }
        };

        public string TestButtonText => I18n.T("BtnTest");

        public SlotViewModel(string directionLabel, ActionItem action)
        {
            DirectionLabel = directionLabel;
            Action = action ?? new ActionItem { Type = "Hotkey", Name = "快捷动作", Parameter = "" };

            I18n.LanguageChanged += () =>
            {
                OnPropertyChanged(nameof(ActionTypes));
                OnPropertyChanged(nameof(TestButtonText));
                OnPropertyChanged(nameof(IconDisplayText));
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
