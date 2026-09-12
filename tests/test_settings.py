import base64

import pytest
from conftest import (
    ONSCREEN,
    assert_catalog,
    assert_text_contains,
    dismiss_messagebox,
    goto,
    list_item_texts,
    read_config,
    select_option,
    text_of,
    wait_dialog,
    wait_dialog_closed,
    wait_for_label_value,
    wait_until,
)

# 1×1 PNG：v136 中心图标用例的自建测试图片（不再依赖 C:\Windows 系统文件）
PNG_1X1_BASE64 = (
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
)

# 产品下拉目录（Tag 的固定顺序）。UIA 不暴露选项文本、SelectedValuePath 取 Tag，
# 故用例用名字（= 落盘值）而不是魔数索引选项目；select_option 会先断言目录规模，
# 目录变更时显式失败而不是"选错项还侥幸通过"。
APP_THEME_CATALOG = ("System", "Light", "Dark", "MidnightNavy", "RoyalViolet", "TitaniumGray")  # InterfaceThemeSettingsViewModel
WHEEL_STYLE_CATALOG = ("ClassicRing", "CleanSectors", "Glassmorphism")  # AppearanceSettingsPage.xaml
WHEEL_PALETTE_CATALOG = ("System", "Dark", "Light", "MatchaForest", "GlacialIce", "MorandiMuted")  # 固定项；自定义预设追加在后
ICON_LAYOUT_CATALOG = ("IconAndText", "IconOnly", "TextOnly")  # AppearanceSettingsPage.xaml
SHAPE_CATALOG = ("Original", "Circle", "RoundedCapsule", "HexagonHive")  # AppearanceSettingsPage.xaml
CORE_ICON_CATALOG = ("Exit", "Crosshair", "Windows", "Dot", "Home", "Power", "Compass", "CatPaw", "Custom", "Image")  # AppearanceSettingsPage.xaml
GLOW_PRESET_CATALOG = ("Auto", "Lilac", "Blue", "Emerald", "Rose", "Amber", "Red", "White", "Custom")  # AppearanceSettingsPage.xaml
LANGUAGE_CATALOG = ("zh-CN", "zh-TW", "en", "ja", "Auto")  # AdvancedSettingsPage.xaml
SLOT_ACTION_TYPE_CATALOG = ("Hotkey", "Launch", "Folder", "System")  # SlotViewModel.ActionTypes

def test_modify_slider_and_save(app):
    win, local_app_data = app

    # 1. Locate the Slider and Label
    slider = win.child_window(auto_id="ThresholdSlider", control_type="Slider")
    label = win.child_window(auto_id="ThresholdValueLabel", control_type="Text")

    initial_val = float(label.window_text())

    # 2. Set value directly using UIA RangeValue pattern
    slider.set_value(32.0)
    wait_for_label_value(win, "ThresholdValueLabel", 32.0)

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
    wait_for_label_value(win, "SectorGapLabel", 5.0)

    # 2. Test Reset Dimensions Button（产品默认间隙为 2；缺失即失败，不静默跳过）
    reset_btn = win.child_window(auto_id="ResetDimensionsButton", control_type="Button")
    assert reset_btn.exists(timeout=3), "ResetDimensionsButton 必须存在"
    reset_btn.invoke()
    wait_for_label_value(win, "SectorGapLabel", 2.0)


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
    add_btn.invoke()
    
    # Check that item was added to listbox
    wait_until(lambda: "testgame.exe" in list_item_texts(list_box), description="黑名单出现 testgame.exe")
    items = list_item_texts(list_box)
    assert "testgame.exe" in items, f"testgame.exe should be in blacklist items: {items}"
    
    # Select and remove
    for item in list_box.children(control_type="ListItem"):
        if item.window_text() == "testgame.exe":
            item.select()
            del_btn.invoke()
            break

    wait_until(lambda: "testgame.exe" not in list_item_texts(list_box), description="黑名单删除 testgame.exe")
    items_after = list_item_texts(list_box)
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
    改首个槽位动作类型后 Save，断言落盘的是本次改动值（System），
    不再让默认就已存在的 Global 兜底方案充当"保存成功"的证据（#135 P0-3）。
    """
    win, local_app_data = app

    goto(win, 2)

    profiles_list = win.child_window(auto_id="ProfilesListBox", control_type="List")
    assert profiles_list.exists(timeout=3), "ProfilesListBox should exist in Gestures tab"

    # 改首个槽位的动作类型（目录 index 3 = System）。该模板 ComboBox 的 UIA 不暴露选中态
    # （selected_index 恒为 None），故不做选中态回读——"切换是否生效"以 Save 后的落盘值为准。
    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    assert type_combo.exists(timeout=3), "Slot0ActionTypeComboBox 必须存在（首个槽位动作类型）"
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "System", verify_selection=False)

    # Save settings and verify config persistence（谓词轮询等改动值落盘）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    def _first_global_action_is_system(c):
        profiles = c.get("Profiles", [])
        glob = next((p for p in profiles if p.get("ProcessName") == "Global"), None)
        actions = (glob or {}).get("Actions") or []
        return bool(actions) and actions[0].get("Type") == "System"

    config = read_config(local_app_data, predicate=_first_global_action_is_system)
    profiles = config.get("Profiles", [])
    assert profiles, "保存后 config 必须包含 Profiles"
    glob = next((p for p in profiles if p.get("ProcessName") == "Global"), None)
    assert glob is not None, f"保存后必须有 Global 方案: {[p.get('ProcessName') for p in profiles]}"
    actions = glob.get("Actions") or []
    assert actions and actions[0].get("Type") == "System", \
        f"Slot0 动作类型应落盘为 System: {actions[0] if actions else None}"


def test_app_theme_persistence_and_removed_wheel_bg_controls(app):
    """
    外观页（NavPage1）界面主题与轮盘配色控件 + 背景图片控件下线 + AppTheme 落盘：
    1. AppThemeComboBox（软件界面主题）与 WheelPaletteComboBox（轮盘配色方案）存在；
    2. 全量枚举证明 WheelBg* 家族已下线（否定断言可证否）；
    3. 选中 Dark → Save → config.json 落盘 AppTheme=Dark。
    """
    win, local_app_data = app

    goto(win, 1)

    # 1. Verify AppTheme dropdown (软件界面主题)
    app_theme_combo = win.child_window(auto_id="AppThemeComboBox", control_type="ComboBox")
    assert app_theme_combo.exists(timeout=3), "AppThemeComboBox should exist"

    # 2. Verify Wheel Theme dropdown (轮盘配色方案)
    wheel_theme_combo = win.child_window(auto_id="WheelPaletteComboBox", control_type="ComboBox")
    assert wheel_theme_combo.exists(timeout=3), "WheelPaletteComboBox should exist"
    # 固定配色目录（自定义预设按配置追加；沙盒冷启动无自定义预设）
    assert_catalog(wheel_theme_combo, WHEEL_PALETTE_CATALOG)

    # 3. Verify Wheel Background images controls are removed
    #    否定断言可证否：同一次全量枚举既证明枚举面有效（清单里含已知在页控件），
    #    又证明已下线功能的 auto_id 家族整体缺席——单点 child_window(...).exists() 为 False
    #    无法区分"控件真的不在"与"auto_id 写错/枚举失效"（#135 P0-7）。
    auto_ids = {element.element_info.automation_id for element in win.descendants()}
    assert "WheelPaletteComboBox" in auto_ids, \
        f"外观页控件枚举异常，否定断言不可证否: {sorted(auto_ids)}"
    stale_bg_ids = sorted(auto_id for auto_id in auto_ids if auto_id.startswith("WheelBg"))
    assert not stale_bg_ids, f"已下线的轮盘背景图片控件仍存在: {stale_bg_ids}"

    # 4. 选中 Dark（目录常量 = 产品下拉项 Tag 顺序）
    select_option(app_theme_combo, APP_THEME_CATALOG, "Dark")

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

    # 3. Select Theme (Dark) and WheelStyle (CleanSectors)
    select_option(wheel_theme_combo, WHEEL_PALETTE_CATALOG, "Dark")
    select_option(ui_style_combo, WHEEL_STYLE_CATALOG, "CleanSectors")

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


def test_shape_selection_and_icon_font_size_persistence(app):
    """
    扇区形状切换与图标/文字尺寸滑块的取值与落盘：
    1. Navigation to Appearance Page (NavPage1).
    2. ShapeComboBox 四项目录（Original/Circle/RoundedCapsule/HexagonHive）选中 RoundedCapsule。
    3. Verification of SectorIconSizeSlider and SectorFontSizeSlider updating.
    4. Save settings and verify config persistence for Shape, SectorIconSize and SectorFontSize.
    """
    win, local_app_data = app

    goto(win, 1)

    # 1. Verify ShapeComboBox exists and can select new shapes
    shape_combo = win.child_window(auto_id="ShapeComboBox", control_type="ComboBox")
    assert shape_combo.exists(timeout=3), "ShapeComboBox should exist"
    assert_catalog(shape_combo, SHAPE_CATALOG)

    # 选 RoundedCapsule（select_option 断言 4 项目录并等选中态回读）；
    # 落盘值在 Save 后另行断言——不再"点过即算"。
    select_option(shape_combo, SHAPE_CATALOG, "RoundedCapsule")

    # 2. Verify SectorIconSizeSlider exists and functions
    icon_slider = win.child_window(auto_id="SectorIconSizeSlider", control_type="Slider")
    assert icon_slider.exists(timeout=3), "SectorIconSizeSlider should exist"

    icon_slider.set_value(26)
    wait_for_label_value(win, "SectorIconSizeLabel", 26.0)

    # 3. Verify SectorFontSizeSlider exists and functions
    font_slider = win.child_window(auto_id="SectorFontSizeSlider", control_type="Slider")
    assert font_slider.exists(timeout=3), "SectorFontSizeSlider should exist"

    font_slider.set_value(13.5)
    wait_for_label_value(win, "SectorFontSizeLabel", 13.5)

    # 4. Save and verify persistence（轮询落盘）
    save_btn = win.child_window(auto_id="SaveButton", control_type="Button")
    save_btn.invoke()

    dismiss_messagebox()

    config = read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("SectorIconSize", 0) - 26) < 1.0
        and abs(c.get("SectorFontSize", 0) - 13.5) < 0.1
        and c.get("Shape") == "RoundedCapsule",
    )
    assert abs(config.get("SectorIconSize", 0) - 26) < 1.0, f"Saved SectorIconSize should be 26, got {config.get('SectorIconSize')}"
    assert abs(config.get("SectorFontSize", 0) - 13.5) < 0.1, f"Saved SectorFontSize should be 13.5, got {config.get('SectorFontSize')}"
    assert config.get("Shape") == "RoundedCapsule", f"Saved Shape should be 'RoundedCapsule', got {config.get('Shape')}"


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
    assert_catalog(shape_combo, SHAPE_CATALOG)

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
    wait_until(radio12.is_selected, description="12 键单选已选中")

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
    select_option(core_combo, CORE_ICON_CATALOG, "Crosshair")

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


def test_program_picker_opens_and_cancels_cleanly(app):
    """
    程序选择器（真实模态对话框）打开/关闭交互：
    AddProfileButton 打开对话框，CancelButton 干净关闭；关闭后主窗口仍可用、配置无新增方案。
    后台形态下对话框必须离屏（静默运行承诺，见 #134/#135）。
    """
    win, local_app_data = app

    goto(win, 2)

    profiles_before = [p.get("ProcessName") for p in read_config(local_app_data).get("Profiles", [])]

    add_btn = win.child_window(auto_id="AddProfileButton", control_type="Button")
    assert add_btn.exists(timeout=3), "AddProfileButton 必须存在"
    add_btn.invoke()

    picker = wait_dialog("选择程序 - StarPie")
    if not ONSCREEN:
        rect = picker.element_info.rectangle
        assert rect.left <= -1000 and rect.top <= -1000, \
            f"后台形态下程序选择器必须离屏，got {rect}"

    cancel_btn = picker.child_window(auto_id="CancelButton", control_type="Button")
    assert cancel_btn.exists(timeout=5), "程序选择器 CancelButton 必须存在"
    cancel_btn.invoke()
    wait_dialog_closed(picker)

    # 主窗口仍可用：切页往返，锚点断言
    goto(win, 3)
    goto(win, 2)

    profiles_after = [p.get("ProcessName") for p in read_config(local_app_data).get("Profiles", [])]
    assert profiles_after == profiles_before, f"取消选择不应新增方案: {profiles_before} -> {profiles_after}"


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
    select_option(glow_preset_combo, GLOW_PRESET_CATALOG, "Lilac")

    # 3. Check Core Image Controls
    core_combo = win.child_window(auto_id="CoreIconTypeComboBox", control_type="ComboBox")
    assert core_combo.exists(timeout=3), "CoreIconTypeComboBox should exist"

    # Select Image item（SelectedValuePath=Tag，目录内 Tag=Image）
    select_option(core_combo, CORE_ICON_CATALOG, "Image")

    core_img_box = win.child_window(auto_id="CoreImagePathTextBox", control_type="Edit")
    assert core_img_box.exists(timeout=3), "CoreImagePathTextBox should exist"

    # Enter a real temp image path（本用例自建，替代系统文件依赖）
    img_path = tmp_path / "core-icon.png"
    img_path.write_bytes(base64.b64decode(PNG_1X1_BASE64))
    core_img_box.set_text(str(img_path))

    # 4. Verify config file contains all v1.3.6 entries（一次等齐本次改动，避免读到中间快照）
    config = read_config(
        local_app_data,
        predicate=lambda c: c.get("CoreCustomImagePath") == str(img_path)
        and c.get("CoreIconType") == "Image"
        and c.get("HighlightGlowPreset") == "Lilac"
        and c.get("HighlightGlowColor") == "#A855F7",
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

    # 语言目录固定 5 项（zh-CN, zh-TW, en, ja, Auto）
    assert_catalog(lang_combo, LANGUAGE_CATALOG)

    # 3. Select English（目录常量 = 产品下拉项 Tag 顺序）
    select_option(lang_combo, LANGUAGE_CATALOG, "en")

    # 4. Verify UI elements updated to English（文本刷新用轮询，不靠固定等待）
    assert_text_contains(win, "SaveButton", "Button", "Save")

    # T19 数据驱动侧边栏:标题是 NavPage0 单选钮的内容文本(不再有独立 NavPage0Text 元素)
    def _tab0_title():
        return text_of(win, "NavPage0", "RadioButton", timeout=0.3)

    wait_until(lambda: "Trigger" in _tab0_title() or "🎯" in _tab0_title(), description="侧边栏标题切英文")

    # T24 设置页文本经运行时语言字典 DynamicResource 刷新(页面保持挂载, 无 code-behind 回填)
    assert_text_contains(win, "AdvancedPageHeader", "Text", "System Integration & Preferences")

    # 5. Check config file persists Language = "en"（轮询等待落盘）
    config = read_config(local_app_data, predicate=lambda c: c.get("Language") == "en")
    assert config.get("Language") == "en", f"Expected config Language='en', got {config.get('Language')}"

    # 6. Switch to Japanese
    select_option(lang_combo, LANGUAGE_CATALOG, "ja")
    assert_text_contains(win, "SaveButton", "Button", "保存")
    assert_text_contains(win, "AdvancedPageHeader", "Text", "システム統合と高度な設定")
    config = read_config(local_app_data, predicate=lambda c: c.get("Language") == "ja")
    assert config.get("Language") == "ja", f"Expected config Language='ja', got {config.get('Language')}"

    # 7. Switch back to zh-CN
    select_option(lang_combo, LANGUAGE_CATALOG, "zh-CN")
    config = read_config(local_app_data, predicate=lambda c: c.get("Language") == "zh-CN")
    assert config.get("Language") == "zh-CN", f"Expected config Language='zh-CN', got {config.get('Language')}"
    assert_text_contains(win, "AdvancedPageHeader", "Text", "系统集成与高级偏好设置")

def test_v139_folder_action_type_and_i18n_consistency(app):
    """
    v1.3.9 文件夹动作类型落盘：手势页首个槽位动作类型选 Folder，Save 后断言 Global
    方案首个动作 Type 落盘为 Folder。（动作类型下拉的本地化文本不在此用例覆盖。）
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

    # 该 ComboBox（Slot0 动作类型）的 UIA 不暴露选中态，落盘值断言即门。
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "Folder", verify_selection=False)

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
    v1.4.0 目录与容器（外观页 + 手势页）：
    1. 外观页 WheelStyleComboBox 固定 3 项（ClassicRing/CleanSectors/Glassmorphism，无 CatPaw）；
    2. CustomColorExpander 存在；
    3. 手势页动作列表标题存在。
    """
    win, local_app_data = app

    # 1. Appearance Page (Page 1)
    goto(win, 1)

    ui_style_combo = win.child_window(auto_id="WheelStyleComboBox", control_type="ComboBox")
    assert ui_style_combo.exists(timeout=3), "WheelStyleComboBox should exist"
    # 轮盘风格目录固定 3 项（ClassicRing/CleanSectors/Glassmorphism，无 CatPaw）
    assert_catalog(ui_style_combo, WHEEL_STYLE_CATALOG)

    color_expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert color_expander.exists(timeout=3), "CustomColorExpander should exist"

    # 2. Gestures Page (Page 2)
    goto(win, 2)

    action_list_title = win.child_window(auto_id="SectorActionListTitleText", control_type="Text")
    assert action_list_title.exists(timeout=3), "SectorActionListTitleText should exist"

def test_v141_outer_escape_cancel_and_rename_capabilities(app):
    """
    v1.4.1 开关与控件存在性：
    1. 触发页 EnableOuterEscapeCheckBox 可切换（保存后断言 config 值随 toggle 翻转）；
    2. 手势页 RenameProfileButton 存在；
    3. 外观页 CustomColorExpander 存在。
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
    select_option(lang_combo, LANGUAGE_CATALOG, "en")
    assert_text_contains(win, "AdvancedPageSubheader", "Text", "Manage interface language")

    # Gestures & Actions (NavPage2)
    goto(win, 2)
    assert_text_contains(win, "GesturesPageSubheader", "Text", "Set dedicated multi-directional gesture wheels")

    # Appearance (NavPage1)
    goto(win, 1)
    assert_text_contains(win, "AppearancePageSubheader", "Text", "Customize visual styles")

    # --- Japanese --- (switch back on Advanced, then re-check in place)
    goto(win, 3)
    select_option(lang_combo, LANGUAGE_CATALOG, "ja")
    assert_text_contains(win, "AdvancedPageSubheader", "Text", "インターフェース言語")

    # --- back to Simplified Chinese ---
    select_option(lang_combo, LANGUAGE_CATALOG, "zh-CN")
    assert_text_contains(win, "AdvancedPageSubheader", "Text", "管理界面语言")
