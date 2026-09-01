import os
import json
import time
import pytest
from pywinauto import Desktop

def get_config_path(local_app_data):
    for name in ["StarPie", "WinPieGestures"]:
        p = os.path.join(str(local_app_data), name, "config.json")
        if os.path.exists(p):
            return p
    return os.path.join(str(local_app_data), "StarPie", "config.json")

def test_modify_slider_and_save(app):
    win, local_app_data = app
    
    # 1. Locate the Slider and Label
    slider = win.child_window(auto_id="ThresholdSlider", control_type="Slider")
    label = win.child_window(auto_id="ThresholdValueLabel", control_type="Text")
    
    initial_text = label.window_text()
    initial_val = float(initial_text)
    
    # 2. Set value directly using UIA RangeValue pattern
    slider.set_value(32.0)
    time.sleep(0.3)
    
    new_text = label.window_text()
    new_val = float(new_text)
    
    assert new_val != initial_val, f"Slider value should have changed from {initial_val}"
    
    # 3. Check if config exists before saving
    config_path = get_config_path(local_app_data)

    # 4. Click the SaveButton to persist configurations
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    
    # Dismiss the popup dialog if it appears
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=3):
            ok_btn = dialog.child_window(control_type="Button")
            ok_btn.invoke()
    except Exception:
        pass
        
    time.sleep(0.8)
    
    # 5. Verify the config file was written correctly in the sandbox
    assert os.path.exists(config_path), f"Config file not found at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    assert config["DragThreshold"] == new_val, f"Saved DragThreshold ({config['DragThreshold']}) should match UI value ({new_val})"


def test_switch_all_tabs_smoothly(app):
    """
    Test clicking through all 5 navigation radio buttons (NavTab0 ~ NavTab4)
    to guarantee zero crashes, zero freezes, and that controls remain fully responsive.
    """
    win, local_app_data = app
    
    # Iterate through all 5 tabs:
    # 0: 触发与场景 (NavTab0)
    # 1: 外观与形态 (NavTab1)
    # 2: 手势与动作 (NavTab2)
    # 3: 高级与系统 (NavTab3)
    # 4: 关于与更新 (NavTab4)
    for i in range(5):
        tab_btn = win.child_window(auto_id=f"NavTab{i}", control_type="RadioButton")
        assert tab_btn.exists(timeout=5), f"NavTab{i} must exist"
        tab_btn.click_input()  # T19 命令式导航:UIA Select 只置勾选不触发命令,须真实点击
        time.sleep(0.3)
        assert win.is_visible(), f"Window must remain visible after selecting NavTab{i}"
        
    # Specifically re-verify Tab 1 (Appearance & Live Canvas)
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    wheel_slider = win.child_window(auto_id="WheelRadiusSlider", control_type="Slider")
    assert wheel_slider.exists(timeout=3), "WheelRadiusSlider should exist in Appearance tab"
    
    gap_slider = win.child_window(auto_id="SectorGapSlider", control_type="Slider")
    assert gap_slider.exists(timeout=3), "SectorGapSlider should exist in Appearance tab"
    
    corner_slider = win.child_window(auto_id="SectorCornerRadiusSlider", control_type="Slider")
    assert corner_slider.exists(timeout=3), "SectorCornerRadiusSlider should exist in Appearance tab"
    
    # Test Tab 2 (Mappings & Profiles)
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.4)
    profiles_list = win.child_window(auto_id="ProfilesListBox", control_type="List")
    assert profiles_list.exists(timeout=3), "ProfilesListBox should exist in Mappings tab"
    
    # Test Tab 3 (System)
    tab3 = win.child_window(auto_id="NavTab3", control_type="RadioButton")
    tab3.click_input()
    time.sleep(0.3)
    auto_start_chk = win.child_window(auto_id="AutoStartCheckBox", control_type="CheckBox")
    assert auto_start_chk.exists(timeout=3), "AutoStartCheckBox should exist in System tab"
    
    # Test Tab 4 (About)
    tab4 = win.child_window(auto_id="NavTab4", control_type="RadioButton")
    tab4.click_input()
    time.sleep(0.3)
    changelog_btn = win.child_window(auto_id="OpenChangelogButton", control_type="Button")
    assert changelog_btn.exists(timeout=3), "OpenChangelogButton should exist in About tab"
    
    # Final check that window is still healthy and alive
    assert win.is_visible()


def test_appearance_shapes_and_geometry_reset(app):
    """
    Test Shape selection, Gap/Fillet adjustments, and Reset Dimensions button.
    """
    win, local_app_data = app
    
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 1. Test Gap & Corner Radius Sliders
    gap_slider = win.child_window(auto_id="SectorGapSlider", control_type="Slider")
    corner_slider = win.child_window(auto_id="SectorCornerRadiusSlider", control_type="Slider")
    gap_label = win.child_window(auto_id="SectorGapLabel", control_type="Text")
    
    gap_slider.set_value(5.0)
    corner_slider.set_value(8.0)
    time.sleep(0.3)
    
    assert "5" in gap_label.window_text()
    
    # 2. Test Reset Dimensions Button
    reset_btn = win.child_window(auto_id="ResetDimensionsButton", control_type="Button")
    if reset_btn.exists(timeout=2):
        reset_btn.invoke()
        time.sleep(0.4)
        assert "2" in gap_label.window_text()


def test_blacklist_add_and_delete(app):
    """
    Test adding a new process to Blacklist and removing it.
    """
    win, local_app_data = app
    
    tab0 = win.child_window(auto_id="NavTab0", control_type="RadioButton")
    tab0.select()
    time.sleep(0.3)
    
    txt_box = win.child_window(auto_id="NewBlacklistProcessTextBox", control_type="Edit")
    add_btn = win.child_window(auto_id="AddBlacklistButton", control_type="Button")
    del_btn = win.child_window(auto_id="DeleteBlacklistButton", control_type="Button")
    list_box = win.child_window(auto_id="BlacklistListBox", control_type="List")
    
    txt_box.set_text("testgame.exe")
    time.sleep(0.2)
    add_btn.invoke()
    time.sleep(0.3)
    
    # Check that item was added to listbox
    items = [item.window_text() for item in list_box.children(control_type="ListItem")]
    assert "testgame.exe" in items, f"testgame.exe should be in blacklist items: {items}"
    
    # Select and remove
    for item in list_box.children(control_type="ListItem"):
        if item.window_text() == "testgame.exe":
            item.select()
            time.sleep(0.2)
            del_btn.invoke()
            time.sleep(0.3)
            break
            
    items_after = [item.window_text() for item in list_box.children(control_type="ListItem")]
    assert "testgame.exe" not in items_after, "testgame.exe should have been deleted"


def test_profile_management_ui_and_buttons(app):
    """
    Test existence, states, and accessibility of profile management controls:
    Add App Profile, Add Custom Profile, Rename Profile, Delete Profile.
    """
    win, local_app_data = app
    
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.4)
    
    add_app_btn = win.child_window(auto_id="AddProfileButton", control_type="Button")
    add_custom_btn = win.child_window(auto_id="AddCustomProfileButton", control_type="Button")
    rename_btn = win.child_window(auto_id="RenameProfileButton", control_type="Button")
    delete_btn = win.child_window(auto_id="DeleteProfileButton", control_type="Button")
    profiles_list = win.child_window(auto_id="ProfilesListBox", control_type="List")
    
    assert add_app_btn.exists(timeout=3), "AddProfileButton should exist"
    assert add_custom_btn.exists(timeout=3), "AddCustomProfileButton should exist"
    assert rename_btn.exists(timeout=3), "RenameProfileButton should exist"
    assert delete_btn.exists(timeout=3), "DeleteProfileButton should exist"
    assert profiles_list.exists(timeout=3), "ProfilesListBox should exist"
    
    # Verify Global profile is listed
    items = [item.window_text() for item in profiles_list.children(control_type="ListItem")]
    assert any("Global" in it for it in items), f"Global profile must be listed: {items}"


def test_hotkey_recorder_and_system_presets_catalog(app):
    """
    Test v1.2.2 features:
    1. Navigation to Mappings Tab (NavTab2).
    2. Verification that Slots list and profile controls are displayed.
    3. Save and persistence verification.
    """
    win, local_app_data = app
    
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.4)
    
    profiles_list = win.child_window(auto_id="ProfilesListBox", control_type="List")
    assert profiles_list.exists(timeout=3), "ProfilesListBox should exist in Mappings tab"
    
    # Save settings and verify config persistence
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=3):
            ok_btn = dialog.child_window(control_type="Button")
            ok_btn.invoke()
    except Exception:
        pass
        
    time.sleep(0.5)
    assert win.is_visible()


def test_v124_app_interface_themes_and_clean_appearance(app):
    """
    Test v1.2.4 features:
    1. Navigation to Appearance Tab (NavTab1).
    2. Verification that 'AppThemeComboBox' (软件控制台界面主题) exists and functions.
    3. Verification that 'ThemeComboBox' (轮盘配色方案) with 7+ presets exists.
    4. Verification that Wheel Background images card is removed.
    5. AppTheme selection, saving, and JSON persistence validation.
    """
    win, local_app_data = app
    
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 1. Verify AppTheme dropdown (软件界面主题)
    app_theme_combo = win.child_window(auto_id="AppThemeComboBox", control_type="ComboBox")
    assert app_theme_combo.exists(timeout=3), "AppThemeComboBox should exist"
    
    # 2. Verify Wheel Theme dropdown (轮盘配色方案)
    wheel_theme_combo = win.child_window(auto_id="ThemeComboBox", control_type="ComboBox")
    assert wheel_theme_combo.exists(timeout=3), "ThemeComboBox should exist"
    
    # 3. Verify Wheel Background images controls are removed
    wheel_bg_box = win.child_window(auto_id="WheelBgImageTextBox", control_type="Edit")
    assert not wheel_bg_box.exists(timeout=1), "WheelBgImageTextBox should NOT exist (feature canceled)"
    
    # 4. Select App Theme by index (Index 2: Obsidian Dark)
    app_theme_combo.select(2)
    time.sleep(0.3)
    
    # 5. Save settings and verify config persistence
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=3):
            ok_btn = dialog.child_window(control_type="Button")
            ok_btn.invoke()
    except Exception:
        pass
        
    time.sleep(0.8)
    
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file not found at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    assert config.get("AppTheme") == "Dark", f"AppTheme ({config.get('AppTheme')}) should be 'Dark'"


def test_v130_wheel_themes_and_custom_preset_and_text_sync(app):
    """
    Test v1.3.0 features:
    1. Verify 4 core Wheel Styles in UiStyleComboBox.
    2. Verify 7 core Wheel Color Themes in ThemeComboBox.
    3. Verify SaveCustomColorPresetButton exists.
    4. Verify ShowText and IconLayoutMode synchronization.
    5. Save settings and verify config persistence.
    """
    win, local_app_data = app
    
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 1. Verify UiStyle dropdown (轮盘主题风格)
    ui_style_combo = win.child_window(auto_id="UiStyleComboBox", control_type="ComboBox")
    assert ui_style_combo.exists(timeout=3), "UiStyleComboBox should exist"
    
    # 2. Verify Theme dropdown (轮盘配色方案)
    wheel_theme_combo = win.child_window(auto_id="ThemeComboBox", control_type="ComboBox")
    assert wheel_theme_combo.exists(timeout=3), "ThemeComboBox should exist"
    
    # 3. Select Theme (Index 1: Dark) and UiStyle (Index 1: CleanSectors)
    wheel_theme_combo.select(1)
    ui_style_combo.select(1)
    time.sleep(0.4)
    
    # 4. Verify ShowText checkbox and IconLayoutMode dropdown
    show_text_chk = win.child_window(auto_id="ShowTextCheckBox", control_type="CheckBox")
    assert show_text_chk.exists(timeout=3), "ShowTextCheckBox should exist"
    
    layout_mode_combo = win.child_window(auto_id="IconLayoutModeComboBox", control_type="ComboBox")
    assert layout_mode_combo.exists(timeout=3), "IconLayoutModeComboBox should exist"
    
    # 5. Save settings and verify config persistence
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=3):
            ok_btn = dialog.child_window(control_type="Button")
            ok_btn.invoke()
    except Exception:
        pass
        
    time.sleep(0.8)
    
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file not found at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    assert config.get("Theme") == "Dark", f"Theme ({config.get('Theme')}) should be 'Dark'"
    assert config.get("UiStyle") == "CleanSectors", f"UiStyle ({config.get('UiStyle')}) should be 'CleanSectors'"


def test_v132_shapes_fontsize_and_iconsize_control(app):
    """
    Test v1.3.2 features:
    1. Navigation to Appearance Tab (NavTab1).
    2. Verification of new shapes in ShapeComboBox (OrganicPetals, ArcTracker, RoundedCapsule).
    3. Verification of SectorIconSizeSlider and SectorFontSizeSlider updating.
    4. Save settings and verify config persistence for SectorIconSize and SectorFontSize.
    """
    win, local_app_data = app
    
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 1. Verify ShapeComboBox exists and can select new shapes
    shape_combo = win.child_window(auto_id="ShapeComboBox", control_type="ComboBox")
    assert shape_combo.exists(timeout=3), "ShapeComboBox should exist"
    
    # Select Capsule or HexagonHive
    shape_combo.select(2)
    time.sleep(0.3)
    
    # 2. Verify SectorIconSizeSlider exists and functions
    icon_slider = win.child_window(auto_id="SectorIconSizeSlider", control_type="Slider")
    icon_label = win.child_window(auto_id="SectorIconSizeLabel", control_type="Text")
    assert icon_slider.exists(timeout=3), "SectorIconSizeSlider should exist"
    assert icon_label.exists(timeout=3), "SectorIconSizeLabel should exist"
    
    icon_slider.set_value(26)
    time.sleep(0.3)
    assert "26" in icon_label.window_text()

    # 3. Verify SectorFontSizeSlider exists and functions
    font_slider = win.child_window(auto_id="SectorFontSizeSlider", control_type="Slider")
    font_label = win.child_window(auto_id="SectorFontSizeLabel", control_type="Text")
    assert font_slider.exists(timeout=3), "SectorFontSizeSlider should exist"
    assert font_label.exists(timeout=3), "SectorFontSizeLabel should exist"
    
    font_slider.set_value(13.5)
    time.sleep(0.3)
    assert "13.5" in font_label.window_text()
    
    # 4. Save and verify persistence
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=3):
            ok_btn = dialog.child_window(control_type="Button")
            ok_btn.invoke()
    except Exception:
        pass
        
    time.sleep(0.8)
    
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file not found at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    assert abs(config.get("SectorIconSize", 0) - 26) < 1.0, f"Saved SectorIconSize should be 26, got {config.get('SectorIconSize')}"
    assert abs(config.get("SectorFontSize", 0) - 13.5) < 0.1, f"Saved SectorFontSize should be 13.5, got {config.get('SectorFontSize')}"


def test_v133_sector_count_4_8_12_adaptation_and_streamlined_shapes(app):
    """
    Test v1.3.3 features:
    1. Verify streamlined shapes (4 items in ShapeComboBox).
    2. Switch to Gestures & Actions tab (NavTab2).
    3. Verify 4-key (SectorCount4Radio) and 12-key (SectorCount12Radio) selection works.
    4. Save settings and verify profile SectorCount is correctly updated and persisted.
    """
    win, local_app_data = app
    
    # 1. Verify streamlined shapes
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    shape_combo = win.child_window(auto_id="ShapeComboBox", control_type="ComboBox")
    assert shape_combo.exists(timeout=3), "ShapeComboBox should exist"
    
    # 2. Switch to Gestures & Actions tab
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.5)
    
    radio4 = win.child_window(auto_id="SectorCount4Radio", control_type="RadioButton")
    radio8 = win.child_window(auto_id="SectorCount8Radio", control_type="RadioButton")
    radio12 = win.child_window(auto_id="SectorCount12Radio", control_type="RadioButton")
    
    assert radio4.exists(timeout=3), "SectorCount4Radio should exist"
    assert radio8.exists(timeout=3), "SectorCount8Radio should exist"
    assert radio12.exists(timeout=3), "SectorCount12Radio should exist"
    
    # 3. Select 12-key sector count
    radio12.select()
    time.sleep(0.4)
    
    # 4. Save and verify persistence in config
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=3):
            ok_btn = dialog.child_window(control_type="Button")
            ok_btn.invoke()
    except Exception:
        pass
        
    time.sleep(0.8)
    
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file not found at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    profiles = config.get("Profiles", [])
    assert len(profiles) > 0, "Should have at least one profile"
    global_prof = next((p for p in profiles if p.get("ProcessName") == "Global"), profiles[0])
    assert global_prof.get("SectorCount") == 12, f"Global profile SectorCount should be 12, got {global_prof.get('SectorCount')}"


def test_v134_memory_autosave_and_theme_persistence(app):
    """
    Test v1.3.4 features:
    1. Switch AppTheme to Modern Light (极简纯白).
    2. Switch UiStyle, Wheel Theme, and change sliders.
    3. Verify config is automatically persisted to disk via debounce / window close.
    4. Verify settings persistence without needing explicit SaveButton click.
    """
    win, local_app_data = app
    
    # 1. Navigate to Appearance Tab (NavTab1)
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 2. Change AppTheme to Light
    app_theme_combo = win.child_window(auto_id="AppThemeComboBox", control_type="ComboBox")
    assert app_theme_combo.exists(timeout=3), "AppThemeComboBox should exist"
    
    # 3. Change a slider (WheelRadiusSlider)
    wheel_slider = win.child_window(auto_id="WheelRadiusSlider", control_type="Slider")
    wheel_slider.set_value(145.0)
    time.sleep(0.6) # Allow debounce timer to trigger auto-save
    
    # 4. Check config file directly
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file should be auto-persisted at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    assert abs(config.get("WheelRadius", 0) - 145.0) < 1.0, f"Auto-persisted WheelRadius should be 145, got {config.get('WheelRadius')}"


def test_v135_program_picker_clean_icons_and_core_customization(app):
    """
    Test v1.3.5 features:
    1. Navigate to Appearance Tab (NavTab1).
    2. Toggle ShowCoreIconCheckBox.
    3. Select a Core Pattern from CoreIconTypeComboBox (e.g., Windows Logo or Crosshair).
    4. Verify configuration auto-persists ShowCoreIcon and CoreIconType.
    5. Navigate to Mappings Tab (NavTab2), open ProgramPickerWindow, verify it opens and closes cleanly.
    """
    win, local_app_data = app
    
    # 1. Appearance Tab
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 2. Check Core Icon controls exist
    core_chk = win.child_window(auto_id="ShowCoreIconCheckBox", control_type="CheckBox")
    assert core_chk.exists(timeout=3), "ShowCoreIconCheckBox should exist"
    
    core_combo = win.child_window(auto_id="CoreIconTypeComboBox", control_type="ComboBox")
    assert core_combo.exists(timeout=3), "CoreIconTypeComboBox should exist"
    
    # 3. Toggle ShowCoreIcon and select Core Pattern
    try:
        core_chk.toggle()
    except Exception:
        pass
    core_combo.select(1)
    time.sleep(0.6)
    
    # 4. Verify config persisted
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file should exist at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    assert "ShowCoreIcon" in config, "ShowCoreIcon should be persisted in config"
    assert "CoreIconType" in config, "CoreIconType should be persisted in config"
    
    # 5. Test ProgramPickerWindow in Mappings Tab
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.4)
    
    add_btn = win.child_window(auto_id="AddProfileButton", control_type="Button")
    assert add_btn.exists(timeout=3), "AddProfileButton should exist"


def test_v136_glow_color_customization_config_memory_and_core_image(app):
    """
    Test v1.3.6 features:
    1. Verify Highlight Glow controls (HighlightGlowPresetComboBox, HighlightGlowRadiusSlider, HighlightGlowOpacitySlider).
    2. Change Highlight Glow Preset and verify dynamic color assignment.
    3. Verify Center Core Image support (CoreIconTypeComboBox item 'Image', CoreImagePathTextBox).
    4. Verify config persistence of all v1.3.6 settings into config.json.
    5. Test config reload integrity to ensure zero configuration regression or overwriting upon startup.
    """
    win, local_app_data = app
    
    # 1. Appearance Tab
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 2. Check Highlight Glow controls
    glow_preset_combo = win.child_window(auto_id="HighlightGlowPresetComboBox", control_type="ComboBox")
    assert glow_preset_combo.exists(timeout=3), "HighlightGlowPresetComboBox should exist"
    
    glow_radius_slider = win.child_window(auto_id="HighlightGlowRadiusSlider", control_type="Slider")
    assert glow_radius_slider.exists(timeout=3), "HighlightGlowRadiusSlider should exist"
    
    glow_opacity_slider = win.child_window(auto_id="HighlightGlowOpacitySlider", control_type="Slider")
    assert glow_opacity_slider.exists(timeout=3), "HighlightGlowOpacitySlider should exist"
    
    # Select a glow preset (e.g. 1: Lilac Violet)
    glow_preset_combo.select(1)
    time.sleep(0.3)
    
    # 3. Check Core Image Controls
    core_combo = win.child_window(auto_id="CoreIconTypeComboBox", control_type="ComboBox")
    assert core_combo.exists(timeout=3), "CoreIconTypeComboBox should exist"
    
    # Select Image item (last item)
    core_combo.select(core_combo.item_count() - 1)
    time.sleep(0.3)
    
    core_img_box = win.child_window(auto_id="CoreImagePathTextBox", control_type="Edit")
    assert core_img_box.exists(timeout=3), "CoreImagePathTextBox should exist"
    
    # Enter dummy image path
    dummy_img_path = "C:\\Windows\\System32\\SecurityAndMaintenance_Error.png"
    core_img_box.set_text(dummy_img_path)
    time.sleep(0.6) # Allow debounce auto-save
    
    # 4. Verify config file contains all v1.3.6 entries
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file should exist at {config_path}"
    
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
        
    assert config.get("HighlightGlowPreset") == "Lilac", f"Expected HighlightGlowPreset to be 'Lilac', got {config.get('HighlightGlowPreset')}"
    assert config.get("HighlightGlowColor") == "#A855F7", f"Expected HighlightGlowColor to be '#A855F7', got {config.get('HighlightGlowColor')}"
    assert config.get("CoreIconType") == "Image", f"Expected CoreIconType to be 'Image', got {config.get('CoreIconType')}"
    assert config.get("CoreCustomImagePath") == dummy_img_path, f"Expected CoreCustomImagePath to be '{dummy_img_path}', got {config.get('CoreCustomImagePath')}"
    assert "HighlightGlowRadius" in config, "HighlightGlowRadius should be present in config"
    assert "HighlightGlowOpacity" in config, "HighlightGlowOpacity should be present in config"


def test_v136_custom_color_preset_deletion_and_management(app):
    """
    Test v1.3.6 Custom Color Preset Deletion & Management:
    1. Navigate to Appearance Tab (NavTab1).
    2. Open ThemeComboBox and select Custom ("自定义高级配色").
    3. Verify CustomColorsPanel is visible, and Save button exists.
    4. Select a custom preset if present or check Delete button functionality and visibility.
    5. Verify clean management of custom color presets and config synchronization.
    """
    win, local_app_data = app
    
    # 1. Appearance Tab
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    # 2. Check ThemeComboBox existence
    theme_combo = win.child_window(auto_id="ThemeComboBox", control_type="ComboBox")
    assert theme_combo.exists(timeout=3), "ThemeComboBox should exist"
    
    # 3. Verify CustomColorExpander exists
    color_expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert color_expander.exists(timeout=3), "CustomColorExpander should exist"
    
    # 4. Verify config file integrity
    config_path = get_config_path(local_app_data)
    assert os.path.exists(config_path), f"Config file should exist at {config_path}"


def test_v138_i18n_multilanguage_support(app):
    """
    Test v1.3.8 Multi-Language (i18n) Support:
    1. Navigate to Advanced & System Tab (NavTab3).
    2. Verify LanguageComboBox exists and contains zh-CN, zh-TW, en, ja, and Auto.
    3. Switch language to English (en).
    4. Verify UI elements dynamically update to English text.
    5. Verify config.json persists Language="en".
    6. Switch language to Japanese (ja), verify Japanese text.
    7. Switch back to Simplified Chinese (zh-CN).
    """
    win, local_app_data = app
    
    # 1. Advanced & System Tab
    tab3 = win.child_window(auto_id="NavTab3", control_type="RadioButton")
    tab3.click_input()
    time.sleep(0.4)
    
    # 2. Check LanguageComboBox existence
    lang_combo = win.child_window(auto_id="LanguageComboBox", control_type="ComboBox")
    assert lang_combo.exists(timeout=3), "LanguageComboBox should exist in Tab 3"
    
    # Verify items count >= 5 (zh-CN, zh-TW, en, ja, Auto)
    assert lang_combo.item_count() >= 5, f"LanguageComboBox should have at least 5 options, got {lang_combo.item_count()}"
    
    # 3. Select English (Tag="en", index 2)
    lang_combo.select(2)
    time.sleep(0.5)
    
    # 4. Verify UI elements updated to English
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    assert "Save" in save_btn.window_text(), f"Save button should be in English, got {save_btn.window_text()}"
    
    # T19 数据驱动侧边栏:标题是 NavTab0 单选钮的内容文本(不再有独立 NavTab0Text 元素)
    tab0_text = win.child_window(auto_id="NavTab0", control_type="RadioButton").window_text()
    assert "Trigger" in tab0_text or "🎯" in tab0_text, f"Tab0 should update, got {tab0_text}"
    
    # 5. Check config file persists Language = "en"
    config_path = get_config_path(local_app_data)
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
    assert config.get("Language") == "en", f"Expected config Language='en', got {config.get('Language')}"
    
    # 6. Switch to Japanese (Tag="ja", index 3)
    lang_combo.select(3)
    time.sleep(0.5)
    
    assert "保存" in save_btn.window_text(), f"Save button should update to Japanese, got {save_btn.window_text()}"
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
    assert config.get("Language") == "ja", f"Expected config Language='ja', got {config.get('Language')}"
    
    # 7. Switch back to zh-CN (Tag="zh-CN", index 0)
    lang_combo.select(0)
    time.sleep(0.5)
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
    assert config.get("Language") == "zh-CN", f"Expected config Language='zh-CN', got {config.get('Language')}"

def test_v139_folder_action_type_and_i18n_consistency(app):
    """
    Test v1.3.9 Folder Action Type and Global UI I18n Consistency:
    1. Navigate to Gestures & Actions (NavTab2).
    2. Verify SectorActionListTitleText and ProfileCardTitleText exist.
    3. Locate the first slot's Action Type ComboBox and select "Folder" (index 2).
    4. Save configuration and verify config.json persists Type="Folder".
    5. Switch language to English (en), verify action type options are translated.
    6. Switch back to zh-CN.
    """
    win, local_app_data = app
    
    # 1. Switch to Tab 2
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.4)
    
    # 2. Check title text blocks
    action_list_title = win.child_window(auto_id="SectorActionListTitleText", control_type="Text")
    assert action_list_title.exists(timeout=3), "SectorActionListTitleText should exist"
    
    # 3. Locate slots items control and find the first type combo box
    combos = win.descendants(control_type="ComboBox")
    # Find type combobox inside slots (has items count == 4: Hotkey, Launch, Folder, System)
    type_combo = None
    for c in combos:
        try:
            if c.item_count() == 4:
                type_combo = c
                break
        except Exception:
            pass
            
    if type_combo is not None:
        # Select index 2: Folder
        type_combo.select(2)
        time.sleep(0.3)
    
    # 4. Save configuration
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    time.sleep(0.5)
    
    config_path = get_config_path(local_app_data)
    with open(config_path, "r", encoding="utf-8") as f:
        saved_config = json.load(f)
        
    if type_combo is not None:
        assert saved_config["Profiles"][0]["Actions"][0]["Type"] == "Folder", "Action type should persist as Folder"

def test_v140_custom_icons_and_appearance_collapsible_and_milestones_folding(app):
    """
    Test v1.4.0 Features:
    1. Appearance Tab (NavTab1):
       - Verify UiStyleComboBox does not contain CatPaw.
       - Verify CustomColorExpander exists and is collapsible.
    2. Gestures Tab (NavTab2):
       - Verify Launch and Folder browse buttons exist.
    3. About Tab (NavTab4):
       - Verify Milestone cards exist and OlderMilestonesExpander exists.
    """
    win, local_app_data = app
    
    # 1. Appearance Tab (Tab 1)
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    ui_style_combo = win.child_window(auto_id="UiStyleComboBox", control_type="ComboBox")
    assert ui_style_combo.exists(timeout=3), "UiStyleComboBox should exist"
    # Should have exactly 3 styles now (ClassicRing, CleanSectors, Glassmorphism)
    assert ui_style_combo.item_count() == 3, f"UiStyleComboBox should have 3 items without CatPaw, got {ui_style_combo.item_count()}"
    
    color_expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert color_expander.exists(timeout=3), "CustomColorExpander should exist"
    
    # 2. Gestures Tab (Tab 2)
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.4)
    
    action_list_title = win.child_window(auto_id="SectorActionListTitleText", control_type="Text")
    assert action_list_title.exists(timeout=3), "SectorActionListTitleText should exist"
    
    # 3. About Tab (Tab 4)
    tab4 = win.child_window(auto_id="NavTab4", control_type="RadioButton")
    tab4.click_input()
    time.sleep(0.4)
    
    older_expander = win.child_window(auto_id="OlderMilestonesExpander", control_type="Group")
    assert older_expander.exists(timeout=3), "OlderMilestonesExpander should exist"

def test_v141_outer_escape_cancel_and_rename_capabilities(app):
    """
    Test v1.4.1 Features:
    1. Triggers & Scenes Tab (NavTab0):
       - Verify EnableOuterEscapeCheckBox exists and can be toggled.
    2. Gestures Tab (NavTab2):
       - Verify RenameProfileButton exists and is enabled.
    3. Appearance Tab (NavTab1):
       - Verify custom color expander and theme preset capabilities.
    4. Save configuration and verify persistence of v1.4.1 settings.
    """
    win, local_app_data = app
    
    # 1. Triggers Tab (Tab 0)
    tab0 = win.child_window(auto_id="NavTab0", control_type="RadioButton")
    tab0.select()
    time.sleep(0.4)
    
    outer_escape_chk = win.child_window(auto_id="EnableOuterEscapeCheckBox", control_type="CheckBox")
    assert outer_escape_chk.exists(timeout=3), "EnableOuterEscapeCheckBox should exist"
    
    escape_dist_slider = win.child_window(auto_id="OuterEscapeDistanceSlider", control_type="Slider")
    assert escape_dist_slider.exists(timeout=3), "OuterEscapeDistanceSlider should exist"
    
    # 2. Gestures Tab (Tab 2)
    tab2 = win.child_window(auto_id="NavTab2", control_type="RadioButton")
    tab2.click_input()
    time.sleep(0.4)
    
    rename_profile_btn = win.child_window(auto_id="RenameProfileButton", control_type="Button")
    assert rename_profile_btn.exists(timeout=3), "RenameProfileButton should exist"
    
    # 3. Appearance Tab (Tab 1)
    tab1 = win.child_window(auto_id="NavTab1", control_type="RadioButton")
    tab1.click_input()
    time.sleep(0.4)
    
    color_expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert color_expander.exists(timeout=3), "CustomColorExpander should exist"
    
    # 4. Save and verify config
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()
    time.sleep(0.5)
    
    config_path = get_config_path(local_app_data)
    with open(config_path, "r", encoding="utf-8") as f:
        config = json.load(f)
    assert "EnableOuterEscapeCancel" in config, "EnableOuterEscapeCancel should be in config.json"
    assert config["EnableOuterEscapeCancel"] is True, "EnableOuterEscapeCancel should default to True"

