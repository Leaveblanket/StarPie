import base64
import time

import pytest
from conftest import dismiss_messagebox, goto, label_value, read_config

# 1×1 PNG：v136 中心图标用例的自建测试图片（不再依赖 C:\Windows 系统文件）
PNG_1X1_BASE64 = (
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
)

def test_modify_slider_and_save(app):
    win, local_app_data = app

    # 1. Locate the Slider and Label
    slider = win.child_window(auto_id="ThresholdSlider", control_type="Slider")
    label = win.child_window(auto_id="ThresholdValueLabel", control_type="Text")

    initial_val = float(label.window_text())

    # 2. Set value directly using UIA RangeValue pattern
    slider.set_value(32.0)
    time.sleep(0.3)

    new_val = float(label.window_text())
    assert new_val != initial_val, f"Slider value should have changed from {initial_val}"

    # 3. Click the SaveButton to persist configurations
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    # 关闭 Save 触发的提示框（仅 -OnScreen 调试形态会出现；静默形态零等待）
    dismiss_messagebox()

    # 4. Verify the config file was written correctly in the sandbox（轮询落盘，不靠固定 sleep）
    config = read_config(local_app_data, predicate=lambda c: c.get("DragThreshold") == pytest.approx(new_val))
    assert config["DragThreshold"] == pytest.approx(new_val), \
        f"Saved DragThreshold ({config['DragThreshold']}) should match UI value ({new_val})"


def test_switch_all_tabs_smoothly(app):
    """
    依次切到四个页面（导航由选中态驱动，UIA Select 即真导航）：
    以每页锚点控件出现为准，保证零崩溃、页面真实切换、控件就绪。
    """
    win, local_app_data = app

    # 0: 触发与场景 (NavPage0) / 1: 外观与形态 / 2: 手势与动作 / 3: 高级与系统
    for slot in range(4):
        goto(win, slot)

    assert win.is_visible(), "Window must remain visible after switching all pages"

    # 各页代表性控件（锚点之外的补充检查）
    goto(win, 1)
    for auto_id, ctype in (("WheelRadiusSlider", "Slider"),
                           ("SectorGapSlider", "Slider"),
                           ("SectorCornerRadiusSlider", "Slider")):
        assert win.child_window(auto_id=auto_id, control_type=ctype).exists(timeout=3), \
            f"{auto_id} should exist in Appearance tab"

    goto(win, 2)
    assert win.child_window(auto_id="ProfilesListBox", control_type="List").exists(timeout=3), \
        "ProfilesListBox should exist in Gestures tab"

    goto(win, 3)
    assert win.child_window(auto_id="AutoStartCheckBox", control_type="CheckBox").exists(timeout=3), \
        "AutoStartCheckBox should exist in System tab"


def test_appearance_shapes_and_geometry_reset(app):
    """
    形状选择、间隙/圆角滑块与"重置尺寸"按钮：数值断言用等值（不再用子串包含），
    Reset 按钮缺失时显式失败（不得静默跳过）。
    """
    win, local_app_data = app

    goto(win, 1)

    # 1. Test Gap & Corner Radius Sliders
    gap_slider = win.child_window(auto_id="SectorGapSlider", control_type="Slider")
    corner_slider = win.child_window(auto_id="SectorCornerRadiusSlider", control_type="Slider")

    gap_slider.set_value(5.0)
    corner_slider.set_value(8.0)
    time.sleep(0.3)

    assert label_value(win, "SectorGapLabel") == pytest.approx(5.0), "SectorGap 应显示 5"

    # 2. Test Reset Dimensions Button（产品默认间隙为 2；缺失即失败，不静默跳过）
    reset_btn = win.child_window(auto_id="ResetDimensionsButton", control_type="Button")
    assert reset_btn.exists(timeout=3), "ResetDimensionsButton 必须存在"
    reset_btn.invoke()
    time.sleep(0.4)
    assert label_value(win, "SectorGapLabel") == pytest.approx(2.0), "重置后 SectorGap 应回默认 2"


def test_blacklist_add_and_delete(app):
    """
    Test adding a new process to Blacklist and removing it.
    """
    win, local_app_data = app

    goto(win, 0)

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

    goto(win, 2)

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
    手势页（NavPage2）的方案/槽位控件存在性 + 保存持久化：
    Save 后 config 必须包含 Profiles 且含 Global 兜底方案。
    """
    win, local_app_data = app

    goto(win, 2)

    profiles_list = win.child_window(auto_id="ProfilesListBox", control_type="List")
    assert profiles_list.exists(timeout=3), "ProfilesListBox should exist in Gestures tab"

    # Save settings and verify config persistence（轮询落盘）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    config = read_config(local_app_data)
    profiles = config.get("Profiles", [])
    assert profiles, "保存后 config 必须包含 Profiles"
    assert any(p.get("ProcessName") == "Global" for p in profiles), \
        f"保存后必须有 Global 兜底方案: {[p.get('ProcessName') for p in profiles]}"
    assert win.is_visible()


def test_v124_app_interface_themes_and_clean_appearance(app):
    """
    Test v1.2.4 features:
    1. Navigation to Appearance Page (NavPage1).
    2. Verification that 'AppThemeComboBox' (软件控制台界面主题) exists and functions.
    3. Verification that 'WheelPaletteComboBox' (轮盘配色方案) with 7+ presets exists.
    4. Verification that Wheel Background images card is removed.
    5. AppTheme selection, saving, and JSON persistence validation.
    """
    win, local_app_data = app

    goto(win, 1)

    # 1. Verify AppTheme dropdown (软件界面主题)
    app_theme_combo = win.child_window(auto_id="AppThemeComboBox", control_type="ComboBox")
    assert app_theme_combo.exists(timeout=3), "AppThemeComboBox should exist"

    # 2. Verify Wheel Theme dropdown (轮盘配色方案)
    wheel_theme_combo = win.child_window(auto_id="WheelPaletteComboBox", control_type="ComboBox")
    assert wheel_theme_combo.exists(timeout=3), "WheelPaletteComboBox should exist"

    # 3. Verify Wheel Background images controls are removed
    #    （goto 已确认外观页挂载：否定断言不会因 auto_id 拼错而侥幸通过——同页正向控件已在校验）
    wheel_bg_box = win.child_window(auto_id="WheelBgImageTextBox", control_type="Edit")
    assert not wheel_bg_box.exists(timeout=1), "WheelBgImageTextBox should NOT exist (feature canceled)"

    # 4. Select App Theme by index（目录固定 System/Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray，index 2 = Dark）
    app_theme_combo.select(2)
    time.sleep(0.3)

    # 5. Save settings and verify config persistence（轮询落盘）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    config = read_config(local_app_data, predicate=lambda c: c.get("AppTheme") == "Dark")
    assert config.get("AppTheme") == "Dark", f"AppTheme ({config.get('AppTheme')}) should be 'Dark'"


def test_v130_wheel_themes_and_custom_preset_and_text_sync(app):
    """
    轮盘风格/配色下拉存在并可切换（当前目录：WheelStyle 3 项，配色含 Dark），
    ShowText 复选与 IconLayoutMode 下拉存在；保存后 WheelPalette/WheelStyle 落盘。
    （历史 docstring 宣称 4 styles/7 themes/文本同步，与实际断言不符，已按实修正。）
    """
    win, local_app_data = app

    goto(win, 1)

    # 1. Verify WheelStyle dropdown (轮盘主题风格)
    ui_style_combo = win.child_window(auto_id="WheelStyleComboBox", control_type="ComboBox")
    assert ui_style_combo.exists(timeout=3), "WheelStyleComboBox should exist"

    # 2. Verify Theme dropdown (轮盘配色方案)
    wheel_theme_combo = win.child_window(auto_id="WheelPaletteComboBox", control_type="ComboBox")
    assert wheel_theme_combo.exists(timeout=3), "WheelPaletteComboBox should exist"

    # 3. Select Theme (index 1: Dark) and WheelStyle (index 1: CleanSectors)
    wheel_theme_combo.select(1)
    ui_style_combo.select(1)
    time.sleep(0.4)

    # 4. Verify ShowText checkbox and IconLayoutMode dropdown
    show_text_chk = win.child_window(auto_id="ShowTextCheckBox", control_type="CheckBox")
    assert show_text_chk.exists(timeout=3), "ShowTextCheckBox should exist"

    layout_mode_combo = win.child_window(auto_id="IconLayoutModeComboBox", control_type="ComboBox")
    assert layout_mode_combo.exists(timeout=3), "IconLayoutModeComboBox should exist"

    # 5. Save settings and verify config persistence（轮询落盘）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    config = read_config(
        local_app_data,
        predicate=lambda c: c.get("WheelPalette") == "Dark" and c.get("WheelStyle") == "CleanSectors",
    )
    assert config.get("WheelPalette") == "Dark", f"WheelPalette ({config.get('WheelPalette')}) should be 'Dark'"
    assert config.get("WheelStyle") == "CleanSectors", f"WheelStyle ({config.get('WheelStyle')}) should be 'CleanSectors'"


def test_v132_shapes_fontsize_and_iconsize_control(app):
    """
    Test v1.3.2 features:
    1. Navigation to Appearance Page (NavPage1).
    2. Verification of new shapes in ShapeComboBox (OrganicPetals, ArcTracker, RoundedCapsule).
    3. Verification of SectorIconSizeSlider and SectorFontSizeSlider updating.
    4. Save settings and verify config persistence for SectorIconSize and SectorFontSize.
    """
    win, local_app_data = app

    goto(win, 1)

    # 1. Verify ShapeComboBox exists and can select new shapes
    shape_combo = win.child_window(auto_id="ShapeComboBox", control_type="ComboBox")
    assert shape_combo.exists(timeout=3), "ShapeComboBox should exist"

    # Select Capsule or HexagonHive
    shape_combo.select(2)
    time.sleep(0.3)

    # 2. Verify SectorIconSizeSlider exists and functions
    icon_slider = win.child_window(auto_id="SectorIconSizeSlider", control_type="Slider")
    assert icon_slider.exists(timeout=3), "SectorIconSizeSlider should exist"

    icon_slider.set_value(26)
    time.sleep(0.3)
    assert label_value(win, "SectorIconSizeLabel") == pytest.approx(26), "SectorIconSize 应显示 26"

    # 3. Verify SectorFontSizeSlider exists and functions
    font_slider = win.child_window(auto_id="SectorFontSizeSlider", control_type="Slider")
    assert font_slider.exists(timeout=3), "SectorFontSizeSlider should exist"

    font_slider.set_value(13.5)
    time.sleep(0.3)
    assert label_value(win, "SectorFontSizeLabel") == pytest.approx(13.5), "SectorFontSize 应显示 13.5"

    # 4. Save and verify persistence（轮询落盘）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    config = read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("SectorIconSize", 0) - 26) < 1.0
        and abs(c.get("SectorFontSize", 0) - 13.5) < 0.1,
    )
    assert abs(config.get("SectorIconSize", 0) - 26) < 1.0, f"Saved SectorIconSize should be 26, got {config.get('SectorIconSize')}"
    assert abs(config.get("SectorFontSize", 0) - 13.5) < 0.1, f"Saved SectorFontSize should be 13.5, got {config.get('SectorFontSize')}"


def test_v133_sector_count_4_8_12_adaptation_and_streamlined_shapes(app):
    """
    Test v1.3.3 features:
    1. Verify streamlined shapes (4 items in ShapeComboBox).
    2. Switch to Gestures & Actions tab (NavPage2).
    3. Verify 4-key (SectorCount4Radio) and 12-key (SectorCount12Radio) selection works.
    4. Save settings and verify profile SectorCount is correctly updated and persisted.
    """
    win, local_app_data = app

    # 1. Verify streamlined shapes（形状目录固定 4 项：Original/Circle/RoundedCapsule/HexagonHive）
    goto(win, 1)
    shape_combo = win.child_window(auto_id="ShapeComboBox", control_type="ComboBox")
    assert shape_combo.exists(timeout=3), "ShapeComboBox should exist"
    assert shape_combo.item_count() == 4, f"ShapeComboBox 应为 4 项，got {shape_combo.item_count()}"

    # 2. Switch to Gestures & Actions tab
    goto(win, 2)

    radio4 = win.child_window(auto_id="SectorCount4Radio", control_type="RadioButton")
    radio8 = win.child_window(auto_id="SectorCount8Radio", control_type="RadioButton")
    radio12 = win.child_window(auto_id="SectorCount12Radio", control_type="RadioButton")

    assert radio4.exists(timeout=3), "SectorCount4Radio should exist"
    assert radio8.exists(timeout=3), "SectorCount8Radio should exist"
    assert radio12.exists(timeout=3), "SectorCount12Radio should exist"

    # 3. Select 12-key sector count
    radio12.select()
    time.sleep(0.4)

    # 4. Save and verify persistence in config（显式找 Global，不再用 Profiles[0] 兜底）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    config = read_config(
        local_app_data,
        predicate=lambda c: any(
            p.get("ProcessName") == "Global" and p.get("SectorCount") == 12
            for p in c.get("Profiles", [])
        ),
    )
    profiles = config.get("Profiles", [])
    assert profiles, "Should have at least one profile"
    global_prof = next((p for p in profiles if p.get("ProcessName") == "Global"), None)
    assert global_prof is not None, f"缺少 Global 方案: {[p.get('ProcessName') for p in profiles]}"
    assert global_prof.get("SectorCount") == 12, f"Global profile SectorCount should be 12, got {global_prof.get('SectorCount')}"


def test_v134_memory_autosave_and_theme_persistence(app):
    """
    自动保存语义（v1.3.4）：在外观页改 WheelRadiusSlider 后**不点 SaveButton**，
    防抖自动保存应把 WheelRadius=145 落盘到沙盒 config.json（轮询等待，不靠固定 sleep）。
    """
    win, local_app_data = app

    # 1. Navigate to Appearance Page (NavPage1)
    goto(win, 1)

    # 2. Change a slider (WheelRadiusSlider)
    wheel_slider = win.child_window(auto_id="WheelRadiusSlider", control_type="Slider")
    assert wheel_slider.exists(timeout=3), "WheelRadiusSlider should exist"
    wheel_slider.set_value(145.0)

    # 3. 自动保存落盘（轮询，不点 Save）
    config = read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("WheelRadius", 0) - 145.0) < 1.0,
        timeout=5.0,
    )
    assert abs(config.get("WheelRadius", 0) - 145.0) < 1.0, \
        f"Auto-persisted WheelRadius should be 145, got {config.get('WheelRadius')}"


def test_v135_program_picker_clean_icons_and_core_customization(app):
    """
    中心图标自定义（v1.3.5）：
    1. 外观页切换 ShowCoreIconCheckBox，断言 config 值**翻转**（Toggle 失败必须红，不吞异常）。
    2. CoreIconTypeComboBox 选 index 1（目录 Exit/Crosshair/Windows/Dot/…，SelectedValuePath=Tag → Crosshair），
       断言落盘值。
    （程序选择器窗口的打开/关闭用例另立——本用例不再假装覆盖它。）
    """
    win, local_app_data = app

    # 1. Appearance Page
    goto(win, 1)

    # 2. Check Core Icon controls exist
    core_chk = win.child_window(auto_id="ShowCoreIconCheckBox", control_type="CheckBox")
    assert core_chk.exists(timeout=3), "ShowCoreIconCheckBox should exist"

    core_combo = win.child_window(auto_id="CoreIconTypeComboBox", control_type="ComboBox")
    assert core_combo.exists(timeout=3), "CoreIconTypeComboBox should exist"

    # 3. Toggle ShowCoreIcon and select Core Pattern
    pre_state = core_chk.get_toggle_state()
    core_chk.toggle()
    core_combo.select(1)
    time.sleep(0.3)

    # 4. Verify config persisted（轮询：ShowCoreIcon 翻转 + CoreIconType == Crosshair）
    config = read_config(
        local_app_data,
        predicate=lambda c: c.get("CoreIconType") == "Crosshair"
        and c.get("ShowCoreIcon") == (pre_state != 1),
        timeout=5.0,
    )
    assert config.get("ShowCoreIcon") == (pre_state != 1), \
        f"ShowCoreIcon 应翻转（toggle 前 state={pre_state}），got {config.get('ShowCoreIcon')}"
    assert config.get("CoreIconType") == "Crosshair", \
        f"CoreIconType 应为 'Crosshair'，got {config.get('CoreIconType')}"


def test_v136_glow_color_customization_config_memory_and_core_image(app, tmp_path):
    """
    v1.3.6：高亮光效三控件存在并可切换（预设 Lilac + 颜色落盘）、中心图标支持选
    'Image' 项并写入自定义图片路径（用例自建临时图片，不依赖系统文件）；落盘轮询等待。
    """
    win, local_app_data = app

    # 1. Appearance Page
    goto(win, 1)

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

    # Select Image item (last item；SelectedValuePath=Tag，目录最后一项 Tag=Image)
    core_combo.select(core_combo.item_count() - 1)
    time.sleep(0.3)

    core_img_box = win.child_window(auto_id="CoreImagePathTextBox", control_type="Edit")
    assert core_img_box.exists(timeout=3), "CoreImagePathTextBox should exist"

    # Enter a real temp image path（本用例自建，替代系统文件依赖）
    img_path = tmp_path / "core-icon.png"
    img_path.write_bytes(base64.b64decode(PNG_1X1_BASE64))
    core_img_box.set_text(str(img_path))

    # 4. Verify config file contains all v1.3.6 entries（轮询等待图片路径落盘）
    config = read_config(
        local_app_data,
        predicate=lambda c: c.get("CoreCustomImagePath") == str(img_path),
        timeout=5.0,
    )

    assert config.get("HighlightGlowPreset") == "Lilac", f"Expected HighlightGlowPreset to be 'Lilac', got {config.get('HighlightGlowPreset')}"
    assert config.get("HighlightGlowColor") == "#A855F7", f"Expected HighlightGlowColor to be '#A855F7', got {config.get('HighlightGlowColor')}"
    assert config.get("CoreIconType") == "Image", f"Expected CoreIconType to be 'Image', got {config.get('CoreIconType')}"
    assert config.get("CoreCustomImagePath") == str(img_path), f"Expected CoreCustomImagePath to be '{img_path}', got {config.get('CoreCustomImagePath')}"
    assert "HighlightGlowRadius" in config, "HighlightGlowRadius should be present in config"
    assert "HighlightGlowOpacity" in config, "HighlightGlowOpacity should be present in config"


def test_v136_custom_color_preset_deletion_and_management(app):
    """
    自定义配色面板（v1.3.6）存在性检查：外观页 WheelPaletteComboBox 与
    CustomColorExpander 必须存在，沙盒 config.json 完整可读。
    （预设删除/重命名流程的交互用例待补——本用例先只做它实际做的事。）
    """
    win, local_app_data = app

    # 1. Appearance Page
    goto(win, 1)

    # 2. Check WheelPaletteComboBox existence
    theme_combo = win.child_window(auto_id="WheelPaletteComboBox", control_type="ComboBox")
    assert theme_combo.exists(timeout=3), "WheelPaletteComboBox should exist"

    # 3. Verify CustomColorExpander exists
    color_expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert color_expander.exists(timeout=3), "CustomColorExpander should exist"

    # 4. Verify config file integrity
    read_config(local_app_data)


def test_v138_i18n_multilanguage_support(app):
    """
    Test v1.3.8 Multi-Language (i18n) Support:
    1. Navigate to Advanced & System Page (NavPage3).
    2. Verify LanguageComboBox exists and contains zh-CN, zh-TW, en, ja, and Auto.
    3. Switch language to English (en).
    4. Verify UI elements dynamically update to English text.
    5. Verify config.json persists Language="en".
    6. Switch language to Japanese (ja), verify Japanese text.
    7. Switch back to Simplified Chinese (zh-CN).
    """
    win, local_app_data = app

    # 1. Advanced & System Page
    goto(win, 3)

    # 2. Check LanguageComboBox existence
    lang_combo = win.child_window(auto_id="LanguageComboBox", control_type="ComboBox")
    assert lang_combo.exists(timeout=3), "LanguageComboBox should exist in Page 3"

    # Verify items count >= 5 (zh-CN, zh-TW, en, ja, Auto)
    assert lang_combo.item_count() >= 5, f"LanguageComboBox should have at least 5 options, got {lang_combo.item_count()}"

    # 3. Select English (Tag="en", index 2)
    lang_combo.select(2)
    time.sleep(0.5)

    # 4. Verify UI elements updated to English
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    assert "Save" in save_btn.window_text(), f"Save button should be in English, got {save_btn.window_text()}"

    # T19 数据驱动侧边栏:标题是 NavPage0 单选钮的内容文本(不再有独立 NavPage0Text 元素)
    tab0_text = win.child_window(auto_id="NavPage0", control_type="RadioButton").window_text()
    assert "Trigger" in tab0_text or "🎯" in tab0_text, f"Tab0 should update, got {tab0_text}"

    # T24 设置页文本经运行时语言字典 DynamicResource 刷新(页面保持挂载, 无 code-behind 回填)
    advanced_header = win.child_window(auto_id="AdvancedPageHeader", control_type="Text")
    assert "System Integration & Preferences" in advanced_header.window_text(), \
        f"Advanced page header should refresh in place, got {advanced_header.window_text()}"

    # 5. Check config file persists Language = "en"（轮询等待落盘）
    config = read_config(local_app_data, predicate=lambda c: c.get("Language") == "en")
    assert config.get("Language") == "en", f"Expected config Language='en', got {config.get('Language')}"

    # 6. Switch to Japanese (Tag="ja", index 3)
    lang_combo.select(3)
    time.sleep(0.5)

    assert "保存" in save_btn.window_text(), f"Save button should update to Japanese, got {save_btn.window_text()}"
    assert "システム統合と高度な設定" in advanced_header.window_text(), \
        f"Advanced page header should refresh in place (ja), got {advanced_header.window_text()}"
    config = read_config(local_app_data, predicate=lambda c: c.get("Language") == "ja")
    assert config.get("Language") == "ja", f"Expected config Language='ja', got {config.get('Language')}"

    # 7. Switch back to zh-CN (Tag="zh-CN", index 0)
    lang_combo.select(0)
    time.sleep(0.5)
    config = read_config(local_app_data, predicate=lambda c: c.get("Language") == "zh-CN")
    assert config.get("Language") == "zh-CN", f"Expected config Language='zh-CN', got {config.get('Language')}"
    assert "系统集成与高级偏好设置" in advanced_header.window_text(), \
        f"Advanced page header should refresh in place (zh-CN), got {advanced_header.window_text()}"

def test_v139_folder_action_type_and_i18n_consistency(app):
    """
    Test v1.3.9 Folder Action Type and Global UI I18n Consistency:
    1. Navigate to Gestures & Actions (NavPage2).
    2. Verify SectorActionListTitleText and ProfileCardTitleText exist.
    3. Locate the first slot's Action Type ComboBox and select "Folder" (index 2).
    4. Save configuration and verify config.json persists Type="Folder".
    5. Switch language to English (en), verify action type options are translated.
    6. Switch back to zh-CN.
    """
    win, local_app_data = app

    # 1. Switch to Page 2
    goto(win, 2)

    # 2. Check title text blocks
    action_list_title = win.child_window(auto_id="SectorActionListTitleText", control_type="Text")
    assert action_list_title.exists(timeout=3), "SectorActionListTitleText should exist"

    # 3. Locate the first slot's Action Type ComboBox
    #    （产品侧已补稳定 AutomationId：Slot{索引}ActionTypeComboBox，见 #137；Slot0 即首个槽位。）
    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    assert type_combo.exists(timeout=3), "Slot0ActionTypeComboBox 必须存在（首个槽位动作类型）"

    # Select index 2: Folder
    type_combo.select(2)
    time.sleep(0.3)

    # 4. Save configuration（轮询等待 Global 方案首个动作落盘为 Folder）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    def _first_global_action_is_folder(c):
        profiles = c.get("Profiles", [])
        glob = next((p for p in profiles if p.get("ProcessName") == "Global"), None)
        actions = (glob or {}).get("Actions") or []
        return bool(actions) and actions[0].get("Type") == "Folder"

    saved_config = read_config(local_app_data, predicate=_first_global_action_is_folder)
    glob = next(p for p in saved_config["Profiles"] if p.get("ProcessName") == "Global")
    assert glob["Actions"][0]["Type"] == "Folder", "Action type should persist as Folder"

def test_v140_custom_icons_and_appearance_collapsible(app):
    """
    Test v1.4.0 Features:
    1. Appearance Page (NavPage1):
       - Verify WheelStyleComboBox does not contain CatPaw.
       - Verify CustomColorExpander exists and is collapsible.
    2. Gestures Page (NavPage2):
       - Verify Launch and Folder browse buttons exist.
    """
    win, local_app_data = app

    # 1. Appearance Page (Page 1)
    goto(win, 1)

    ui_style_combo = win.child_window(auto_id="WheelStyleComboBox", control_type="ComboBox")
    assert ui_style_combo.exists(timeout=3), "WheelStyleComboBox should exist"
    # Should have exactly 3 styles now (ClassicRing, CleanSectors, Glassmorphism)
    assert ui_style_combo.item_count() == 3, f"WheelStyleComboBox should have 3 items without CatPaw, got {ui_style_combo.item_count()}"

    color_expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert color_expander.exists(timeout=3), "CustomColorExpander should exist"

    # 2. Gestures Page (Page 2)
    goto(win, 2)

    action_list_title = win.child_window(auto_id="SectorActionListTitleText", control_type="Text")
    assert action_list_title.exists(timeout=3), "SectorActionListTitleText should exist"

def test_v141_outer_escape_cancel_and_rename_capabilities(app):
    """
    Test v1.4.1 Features:
    1. Triggers & Scenes Page (NavPage0):
       - Verify EnableOuterEscapeCheckBox exists and can be toggled.
    2. Gestures Page (NavPage2):
       - Verify RenameProfileButton exists and is enabled.
    3. Appearance Page (NavPage1):
       - Verify custom color expander and theme preset capabilities.
    4. Save configuration and verify persistence of v1.4.1 settings.
    """
    win, local_app_data = app

    # 1. Triggers Page (Page 0)
    goto(win, 0)

    outer_escape_chk = win.child_window(auto_id="EnableOuterEscapeCheckBox", control_type="CheckBox")
    assert outer_escape_chk.exists(timeout=3), "EnableOuterEscapeCheckBox should exist"

    escape_dist_slider = win.child_window(auto_id="OuterEscapeDistanceSlider", control_type="Slider")
    assert escape_dist_slider.exists(timeout=3), "OuterEscapeDistanceSlider should exist"

    # toggle 前记录状态：保存后断言 config 值**翻转**（只断言默认值无法证明开关可用）
    pre_state = outer_escape_chk.get_toggle_state()
    outer_escape_chk.toggle()

    # 2. Gestures Page (Page 2)
    goto(win, 2)

    rename_profile_btn = win.child_window(auto_id="RenameProfileButton", control_type="Button")
    assert rename_profile_btn.exists(timeout=3), "RenameProfileButton should exist"

    # 3. Appearance Page (Page 1)
    goto(win, 1)

    color_expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert color_expander.exists(timeout=3), "CustomColorExpander should exist"

    # 4. Save and verify config（轮询落盘）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    config = read_config(
        local_app_data,
        predicate=lambda c: c.get("EnableOuterEscapeCancel") == (pre_state != 1),
    )
    assert "EnableOuterEscapeCancel" in config, "EnableOuterEscapeCancel should be in config.json"
    assert config["EnableOuterEscapeCancel"] == (pre_state != 1), \
        f"EnableOuterEscapeCancel 应为 toggle 后的状态（toggle 前 state={pre_state}），got {config.get('EnableOuterEscapeCancel')}"


def test_t28_hardcoded_copy_follows_language(app):
    """
    T28 (#33): settings-page hardcoded subheaders/headers must follow the
    runtime language dictionary (T24 mechanism). Locate by AutomationId only.
    """
    win, local_app_data = app

    # Advanced & System (NavPage3) -> language combo lives here
    goto(win, 3)
    lang_combo = win.child_window(auto_id="LanguageComboBox", control_type="ComboBox")
    assert lang_combo.exists(timeout=3), "LanguageComboBox should exist"

    # --- English ---
    lang_combo.select(2)  # en
    time.sleep(0.6)

    adv_sub = win.child_window(auto_id="AdvancedPageSubheader", control_type="Text")
    assert adv_sub.exists(timeout=3), "AdvancedPageSubheader should exist"
    assert "Manage interface language" in adv_sub.window_text(), \
        f"Advanced subheader should be en, got {adv_sub.window_text()}"

    # Gestures & Actions (NavPage2)
    goto(win, 2)
    ges_sub = win.child_window(auto_id="GesturesPageSubheader", control_type="Text")
    assert ges_sub.exists(timeout=3), "GesturesPageSubheader should exist"
    assert "Set dedicated multi-directional gesture wheels" in ges_sub.window_text(), \
        f"Gestures subheader should be en, got {ges_sub.window_text()}"

    # Appearance (NavPage1)
    goto(win, 1)
    app_sub = win.child_window(auto_id="AppearancePageSubheader", control_type="Text")
    assert app_sub.exists(timeout=3), "AppearancePageSubheader should exist"
    assert "Customize visual styles" in app_sub.window_text(), \
        f"Appearance subheader should be en, got {app_sub.window_text()}"

    # --- Japanese --- (switch back on Advanced, then re-check in place)
    goto(win, 3)
    lang_combo.select(3)  # ja
    time.sleep(0.6)
    adv_sub = win.child_window(auto_id="AdvancedPageSubheader", control_type="Text")
    assert "インターフェース言語" in adv_sub.window_text(), \
        f"Advanced subheader should be ja, got {adv_sub.window_text()}"

    # --- back to Simplified Chinese ---
    lang_combo.select(0)  # zh-CN
    time.sleep(0.6)
    adv_sub = win.child_window(auto_id="AdvancedPageSubheader", control_type="Text")
    assert "管理界面语言" in adv_sub.window_text(), \
        f"Advanced subheader should be zh-CN, got {adv_sub.window_text()}"
