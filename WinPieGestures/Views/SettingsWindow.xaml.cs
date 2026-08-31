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

namespace WinPieGestures.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly IThemeService _themeService;
        private readonly IDialogService _dialogs;
        private readonly IActionExecutorService _actionExecutor;
        // 根设置 ViewModel (T14)：聚合各分区子 ViewModel，是窗口 DataContext 的单一根源；
        // 分区间共享状态（配置导入后的分区重挂）经根协调。
        private readonly RootSettingsViewModel _root;
        // 分区子 ViewModel：根暴露的同一实例别名，供视图层事件接线（预览重绘、落盘、弹窗等）。
        private readonly AppearanceSettingsViewModel _appearanceVm;
        private readonly ProfileListViewModel _profileList;
        private readonly BehaviorSettingsViewModel _behavior;
        private readonly GeneralSettingsViewModel _general;

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

        public SettingsWindow(RootSettingsViewModel root, IThemeService themeService, IDialogService dialogs, IActionExecutorService actionExecutor)
        {
            InitializeComponent();
            _themeService = themeService;
            _dialogs = dialogs;
            _actionExecutor = actionExecutor;

            // T14：根 ViewModel 由组合根装配后注入（T16 起装配点在 Composition——窗口只消费，
            // 不再自建分区委托链）；窗口 DataContext 设为根，分区 DataContext 与列表 ItemsSource
            // 一律经 XAML 根路径绑定解析（单一根源），视图层只保留事件接线与 View 效果。
            _root = root;
            DataContext = _root;
            _appearanceVm = _root.Appearance;
            _profileList = _root.ProfileList;
            _behavior = _root.Behavior;
            _general = _root.General;

            // 外观分区子 ViewModel (T10)：状态与编排住 VM，绘制（实时预览）留在本视图层。
            // T17：落盘请求（AutoSaveRequested/SaveNowRequested）由根 VM 订阅编排，窗口不再接线。
            _appearanceVm.PreviewInvalidated += OnAppearancePreviewInvalidated;
            _appearanceVm.PresetListChanged += SyncThemePresetItems;
            _appearanceVm.DeleteConfirmRequested += preset =>
            {
                if (_dialogs.Confirm("确认删除配色方案", $"确定要删除自定义配色方案预设【{preset.Name}】吗？"))
                {
                    _appearanceVm.ConfirmDeleteCustomColorPreset(preset);
                }
            };
            _appearanceVm.PresetDeleted += name =>
                _dialogs.ShowInfo("提示", $"自定义配色方案【{name}】已成功删除！");
            _appearanceVm.PresetSaved += name =>
                _dialogs.ShowInfo("提示", $"配色预设【{name}】已成功保存！");
            SyncThemePresetItems();
            _behavior.BlacklistEntryAdded += proc =>
            {
                if (BlacklistListBox != null)
                {
                    BlacklistListBox.SelectedItem = proc;
                    BlacklistListBox.ScrollIntoView(proc);
                }
            };

            // T14：导入后的分区间 VM 重挂由根协调（ReloadAfterConfigImport），窗口只做控件同步。
            _root.PartitionsReloaded += ReloadAfterConfigImport;
            _general.NoticeRequested += ShowNotice;

            // ADR-0002：I18n 语言切换广播——语言切换后经此刷新全部界面文本。
            I18n.LanguageChanged += ApplyLocalization;

            // T17：核圆图片路径缩略图是 View 效果，随 VM 属性变更（绑定逐键推源）刷新。
            _appearanceVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppearanceSettingsViewModel.CoreCustomImagePath))
                {
                    UpdateCoreImageThumbnail(_appearanceVm.CoreCustomImagePath);
                }
            };

            _isUpdatingUi = true;
            try
            {
                // T14：方案列表 ItemsSource 经 XAML 绑定自根解析（ProfileList.Profiles）。

                // T13：行为分区控件初值从 ViewModel 读取（状态已由 VM 承载）。
                ThresholdSlider.Value = _behavior.DragThreshold;
                ThresholdValueLabel.Text = _behavior.DragThreshold.ToString("0");

                // T17：界面主题与中心核图标四项改由 XAML 双向绑定自根解析（初值绑定自动回填）；
                // 窗口主题应用是 View 效果，仍在此驱动。
                _themeService.ApplyTheme(this, _appearanceVm.AppTheme);
                UpdateCoreIconPreviewUI();

                // Load Scene Isolation settings
                DisableOnFullScreenCheckBox.IsChecked = _behavior.DisableOnFullScreen;
                CtrlModifierCheckBox.IsChecked = _behavior.DisableOnCtrl;
                ShiftModifierCheckBox.IsChecked = _behavior.DisableOnShift;
                AltModifierCheckBox.IsChecked = _behavior.DisableOnAlt;

                // T14：黑名单列表源经 XAML 绑定自根解析（Behavior.BlacklistProcesses）。

                // 外圈逃逸：初值同步自 VM（迁移前构造漏设，开关初始显示与配置脱节——与 T11
                // 修正选中态滞留同思路，同步后手势链路读取的配置值不变）。
                EnableOuterEscapeCheckBox.IsChecked = _behavior.EnableOuterEscapeCancel;
                OuterEscapeDistanceSlider.Value = _behavior.OuterEscapeDistance;
                OuterEscapeDistanceLabel.Text = $"{Math.Round(_behavior.OuterEscapeDistance):0} px";

                // Auto-start setting
                AutoStartCheckBox.IsChecked = _general.AutoStartEnabled;

                // Initialize Language setting
                SetComboBoxSelectedValue(LanguageComboBox, _general.LanguageCode);
                ApplyLocalization();

                // T14：槽位集合 ItemsSource 经 XAML 绑定自根解析（ProfileList.Slots）。

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
            // T13：语言切换编排（写配置、I18n.SetLanguage 触发广播）进 GeneralSettingsViewModel；
            // 文本刷新由窗口订阅的 I18n.LanguageChanged → ApplyLocalization 完成。
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                _general.ApplyLanguage(langCode);
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // App-level exit (ADR-0003): pending edits were already flushed by the
            // composition root — allow the close. Any other close hides to the tray.
            if (Composition.IsExiting) return;

            // T17：关闭隐藏前的兜底落盘上移至根 VM（冲刷挂起防抖 + 立即落盘）。
            _root.FlushPendingSave();
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

            // T13：托盘驻留气泡提示编排进 GeneralSettingsViewModel（经组合根已有委托传递）。
            _general.NotifyMinimizedToTray();
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
            _root.FlushPendingSave();
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // T06：程序选择走对话框服务（VM 化链路），取消与无效统一 null。
            var picked = _dialogs.ShowProgramPicker();
            if (picked != null)
            {
                string procName = Path.GetFileName(picked.Path).ToLower();
                if (_profileList.IsProcessNameTaken(procName))
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
                _root.FlushPendingSave();
            }
        }

        private void AddCustomProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var result = _dialogs.ShowInputDialog(
                title: "新建自定义配置",
                prompt: "请输入新配置方案名称（如：游戏模式、绘图工作流、PS修图 或 myapp.exe）：",
                defaultText: _profileList.CreateDefaultCustomProfileName(),
                validator: input =>
                {
                    if (_profileList.IsProcessNameTaken(input))
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
                _root.FlushPendingSave();
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
                    if (_profileList.IsProcessNameTaken(input))
                    {
                        return (false, "已存在同名的配置方案，请换一个名称！");
                    }
                    return (true, "");
                });

            if (result != null)
            {
                selected.Model.ProcessName = result.Text;
                selected.RefreshDisplay();
                _root.FlushPendingSave();
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
                _root.FlushPendingSave();
            }
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThresholdValueLabel != null)
            {
                ThresholdValueLabel.Text = e.NewValue.ToString("0");
            }
            // T13：阈值 live-apply 编排进 VM（写回运行态配置 + 防抖落盘请求）。
            if (_isUpdatingUi || _root.CurrentConfig == null) return;
            _behavior.DragThreshold = e.NewValue;
        }

        private void OuterEscapeDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _root.CurrentConfig == null) return;
            // T16：滑杆值交 VM 管线（取整写穿配置 + 立即落盘请求），标签显示取整值——与迁移前一致。
            _behavior.OuterEscapeDistance = e.NewValue;
            if (OuterEscapeDistanceLabel != null)
            {
                OuterEscapeDistanceLabel.Text = $"{Math.Round(e.NewValue):0} px";
            }
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
            if (_isUpdatingUi || _root.CurrentConfig == null) return;
            // T13：外圈逃逸开关编排进 VM（live-apply 写回配置）；面板可见性是 View 层效果。
            if (OuterEscapeDistancePanel != null) OuterEscapeDistancePanel.Visibility = Visibility.Visible;
            _behavior.EnableOuterEscapeCancel = true;
        }

        private void OuterEscapeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _root.CurrentConfig == null) return;
            if (OuterEscapeDistancePanel != null) OuterEscapeDistancePanel.Visibility = Visibility.Collapsed;
            _behavior.EnableOuterEscapeCancel = false;
        }

        private void ShowCoreIconCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // T17：状态经 IsChecked 双向绑定写穿外观子 VM（落盘由 VM 管线上报根编排）；
            // 本处理器只剩预览重绘 View 效果。
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
        }

        private void CoreIconTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // T17：状态经 SelectedValue 双向绑定写穿外观子 VM（预览重绘与落盘由 VM 管线上报）；
            // 本处理器只剩核圆预览面板/图标同步 View 效果。
            UpdateCoreIconPreviewUI();
        }

        private void PickCoreIconButton_Click(object sender, RoutedEventArgs e)
        {
            // T16：对话框与写回编排进外观子 VM（PickCoreIcon，SlotViewModel.PickIcon 先例）；
            // T17 起落盘由 VM 经 SaveNowRequested 上报根编排，此处只剩 View 层效果：
            // 核圆图标预览与轮盘预览重绘。
            if (_appearanceVm.PickCoreIcon())
            {
                UpdateCoreIconPreviewUI();
                if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
                {
                    RenderLiveWheelPreview();
                }
            }
        }

        private void UpdateCoreIconPreviewUI()
        {
            if (_root.CurrentConfig == null) return;

            string coreType = _appearanceVm.CoreIconType;
            if (CustomCoreIconPanel != null)
            {
                CustomCoreIconPanel.Visibility = coreType == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            }

            if (CustomCoreImagePanel != null)
            {
                CustomCoreImagePanel.Visibility = coreType == "Image" ? Visibility.Visible : Visibility.Collapsed;
                if (coreType == "Image")
                {
                    UpdateCoreImageThumbnail(_appearanceVm.CoreCustomImagePath);
                }
            }

            if (CustomCoreIconPreviewPath != null && CustomCoreIconNameLabel != null)
            {
                var geom = IconHelper.GetCoreIconGeometry(coreType, _appearanceVm.CoreCustomIconKey, _appearanceVm.CoreCustomIconSvg);
                CustomCoreIconPreviewPath.Data = geom;
                if (!string.IsNullOrEmpty(_appearanceVm.CoreCustomIconKey))
                {
                    CustomCoreIconNameLabel.Text = _appearanceVm.CoreCustomIconKey;
                }
                else if (!string.IsNullOrEmpty(_appearanceVm.CoreCustomIconSvg))
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

        private void BrowseCoreImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picked = _dialogs.ShowOpenFileDialog(
                "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico;*.gif|所有文件 (*.*)|*.*",
                "选择中心核圆图案图片");

            if (picked != null)
            {
                // T17：只写 VM——绑定把值回填文本框，缩略图随 VM PropertyChanged 刷新，
                // 预览重绘与落盘由 VM 管线上报（预览/防抖落盘）。
                _appearanceVm.CoreCustomImagePath = picked.Path;
            }
        }

        private void ClearCoreImageButton_Click(object sender, RoutedEventArgs e)
        {
            // T17：同 Browse——只写 VM，文本框与缩略图随绑定/PropertyChanged 刷新。
            _appearanceVm.CoreCustomImagePath = "";
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

        private void AppThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // T17：状态经 SelectedValue 双向绑定写穿外观子 VM（落盘由 VM 管线上报根编排）；
            // 主题应用（窗口视觉）是 View 效果，留置本层。
            _themeService.ApplyTheme(this, _appearanceVm.AppTheme);
            if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
            {
                RenderLiveWheelPreview();
            }
        }

        private void DisableOnFullScreenCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _root.CurrentConfig == null) return;
            // T13：全屏禁用编排进 VM（live-apply + 落盘请求）。
            _behavior.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
        }

        private void ModifierCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _root.CurrentConfig == null) return;
            // T13：修饰键旁路编排进 VM（live-apply + 落盘请求）。
            _behavior.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
            _behavior.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
            _behavior.DisableOnAlt = AltModifierCheckBox.IsChecked == true;
        }

        /// <summary>把输入框文本与 VM 同步后执行黑名单命令，再按 VM 状态回写输入框：
        /// 成功添加新项后 VM 清空输入文本（对应迁移前 NewBlacklistProcessTextBox.Clear()）；
        /// 取消选择或重复项时 VM 文本不变，输入框保留用户输入——与迁移前一致。</summary>
        private void RunBlacklistCommand(CommunityToolkit.Mvvm.Input.IRelayCommand command)
        {
            _behavior.NewBlacklistProcess = NewBlacklistProcessTextBox.Text;
            command.Execute(null);
            NewBlacklistProcessTextBox.Text = _behavior.NewBlacklistProcess;
        }

        private void BrowseBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            RunBlacklistCommand(_behavior.BrowseBlacklistCommand);
        }

        private void AddBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            RunBlacklistCommand(_behavior.AddBlacklistFromInputCommand);
        }

        private void NewBlacklistProcessTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                RunBlacklistCommand(_behavior.AddBlacklistFromInputCommand);
                e.Handled = true;
            }
        }

        private void BlacklistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 列表选中态与 VM 双向同步（AddBlacklistProcess 经 BlacklistEntryAdded 回设选中为同值，无循环）。
            _behavior.SelectedBlacklistProcess = BlacklistListBox.SelectedItem as string;
        }

        private void BlacklistListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back)
            {
                _behavior.DeleteBlacklistProcessCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void DeleteBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            _behavior.DeleteBlacklistProcessCommand.Execute(null);
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // T13：开机自启编排进 VM（注册表读写经注入委托，组合根接线 AutostartRegistry）。
            _general.SetAutoStart(AutoStartCheckBox.IsChecked == true);
        }

        private void ElevatePrivileges_Click(object sender, RoutedEventArgs e)
        {
            // T13：提权重启编排进 GeneralSettingsViewModel；T17 起组合根托盘菜单也直调分区 VM，
            // 窗口不再保留透传方法。
            _general.ElevateAndRestart();
        }

        private void ExportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            _general.ExportConfigCommand.Execute(null);
        }

        private void ImportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            _general.ImportConfigCommand.Execute(null);
        }

        private void ShowNotice(GeneralSettingsViewModel.NoticeRequest notice)
        {
            var image = notice.Kind switch
            {
                GeneralSettingsViewModel.NoticeKind.Error => MessageBoxImage.Error,
                GeneralSettingsViewModel.NoticeKind.Warning => MessageBoxImage.Warning,
                _ => MessageBoxImage.Information
            };
            MessageBox.Show(notice.Message, notice.Title, MessageBoxButton.OK, image);
        }

        /// <summary>配置导入成功后同步各分区控件显示。T14 起分区间 VM 状态重挂（方案列表、
        /// 行为、通用）已由根 <see cref="RootSettingsViewModel"/> 经 PartitionsReloaded 协调
        /// 完成，此处只做 View 层控件同步。</summary>
        private void ReloadAfterConfigImport()
        {
            // Reload controls
            _isUpdatingUi = true;
            try
            {
                // 方案列表选中态回落到第一项，使扇区数、槽位与预览和导入内容一致
                // （迁移前 _selectedProfile 在导入后滞留旧配置对象、选中态与列表脱节，T11 修正保留）。
                if (_profileList.Profiles.Count > 0)
                {
                    ProfilesListBox.SelectedIndex = 0;
                    _profileList.SelectProfile(_profileList.Profiles[0]);
                    UpdateSectorCountRadios();
                }

                // 行为分区控件同步实际导入值。
                ThresholdSlider.Value = _behavior.DragThreshold;
                ThresholdValueLabel.Text = _behavior.DragThreshold.ToString("0");
                DisableOnFullScreenCheckBox.IsChecked = _behavior.DisableOnFullScreen;
                CtrlModifierCheckBox.IsChecked = _behavior.DisableOnCtrl;
                ShiftModifierCheckBox.IsChecked = _behavior.DisableOnShift;
                AltModifierCheckBox.IsChecked = _behavior.DisableOnAlt;
                EnableOuterEscapeCheckBox.IsChecked = _behavior.EnableOuterEscapeCancel;
                OuterEscapeDistanceSlider.Value = _behavior.OuterEscapeDistance;
                OuterEscapeDistanceLabel.Text = $"{Math.Round(_behavior.OuterEscapeDistance):0} px";

                // 外观分区控件经双向绑定把导入值回推 VM（VM live-apply 写回同值，语义不变）。
                SetComboBoxSelectedValue(ThemeComboBox, _root.CurrentConfig.Theme);
                SetComboBoxSelectedValue(UiStyleComboBox, _root.CurrentConfig.UiStyle);
                SetComboBoxSelectedValue(ShapeComboBox, _root.CurrentConfig.Shape);
                SetComboBoxSelectedValue(IconLayoutModeComboBox, _root.CurrentConfig.IconLayoutMode);

                WheelRadiusSlider.Value = _root.CurrentConfig.WheelRadius;
                InnerRadiusSlider.Value = _root.CurrentConfig.InnerRadius;
                CoreRadiusSlider.Value = _root.CurrentConfig.CoreRadius;
                SectorGapSlider.Value = _root.CurrentConfig.SectorGap;
                SectorCornerRadiusSlider.Value = _root.CurrentConfig.SectorCornerRadius;
                SectorIconSizeSlider.Value = _root.CurrentConfig.SectorIconSize > 0 ? _root.CurrentConfig.SectorIconSize : 20.0;
                SectorIconSizeLabel.Text = $"{SectorIconSizeSlider.Value:0} px";
                SectorFontSizeSlider.Value = _root.CurrentConfig.SectorFontSize > 0 ? _root.CurrentConfig.SectorFontSize : 10.5;
                SectorFontSizeLabel.Text = $"{SectorFontSizeSlider.Value:0.0} px";

                ShowTextCheckBox.IsChecked = _root.CurrentConfig.ShowText;

                // T17：界面主题与中心核四项导入后按新配置回填控件（处理器被 _isUpdatingUi 抑制，
                // 绑定回推同值被 VM 等值守卫短路，不产生落盘/预览事件）；窗口主题应用为 View 效果。
                SetComboBoxSelectedValue(AppThemeComboBox, _appearanceVm.AppTheme);
                _themeService.ApplyTheme(this, _appearanceVm.AppTheme);
                ShowCoreIconCheckBox.IsChecked = _appearanceVm.ShowCoreIcon;
                SetComboBoxSelectedValue(CoreIconTypeComboBox, _appearanceVm.CoreIconType);
                CoreImagePathTextBox.Text = _appearanceVm.CoreCustomImagePath;
                UpdateCoreIconPreviewUI();
            }
            finally
            {
                _isUpdatingUi = false;
            }

            RenderLiveWheelPreview();
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
            // T17：显式保存按钮退化为"立即冲刷挂起防抖 + 落盘"（自动保存之外的兜底交互）。
            _root.FlushPendingSave();
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
                _actionExecutor.Execute(vm.Action);
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
            if (_isRenderingPreview || LiveWheelPreviewCanvas == null || _root.CurrentConfig == null) return;
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
                _previewStyleRenderer.Initialize(theme, _root.CurrentConfig, _themeService.IsWindowsInDarkTheme());
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
                string coreType = _appearanceVm.CoreIconType;

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
                        Visibility = (_appearanceVm.ShowCoreIcon && _appearanceVm.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    if (!string.IsNullOrEmpty(_appearanceVm.CoreCustomImagePath) && File.Exists(_appearanceVm.CoreCustomImagePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(_appearanceVm.CoreCustomImagePath, UriKind.Absolute);
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
                            _appearanceVm.CoreCustomIconKey,
                            _appearanceVm.CoreCustomIconSvg),
                        Fill = _previewTextBrush,
                        Width = exitSize,
                        Height = exitSize,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Visibility = (_appearanceVm.ShowCoreIcon && _appearanceVm.UiStyle != "CatPaw") ? Visibility.Visible : Visibility.Collapsed
                    };
                    previewCoreGrid.Children.Add(_previewExitIcon);
                }

                _previewStyleRenderer.RenderDecorations(LiveWheelPreviewCanvas, previewCoreGrid, cx, cy, outerR, coreR, 1, _appearanceVm.ShowCoreIcon);

                var profile = _profileList.SelectedProfile?.Model ?? _root.CurrentConfig.Profiles.FirstOrDefault() ?? new WheelProfile { SectorCount = 8, Actions = new List<ActionItem>() };
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
            if (_previewSectorPaths.Count == 0 || _root.CurrentConfig == null) return;

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
}
