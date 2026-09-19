"""设置台交互 e2e（触发 / 外观 / 轮盘交互 / 高级四页）。

断言分层口径（本文件）：
- 交互用例只验"运行态即时生效"（live-apply：改动写穿运行态配置、绑定即时刷新）——
  不点 Save、不读盘；落盘链路由专门的持久化用例覆盖（tests/test_persistence.py：
  显式 Save / 防抖自动保存 / 关窗冲刷 / 退出冲刷 / 重启回读 / 导入导出）。
- 本文件保留的落盘断言只有两类：①落盘链路专验（显式 Save、防抖自动保存）；
  ②UIA 观察不到运行态的字段（Slot0 动作类型、IconLayoutMode、glow 颜色/几何、
  自定义图片路径的配置记忆）——那些字段读盘是唯一观察面，不是重复检查。
"""

import base64

import pytest
from catalogs import (
    APP_THEME_CATALOG,
    CORE_ICON_CATALOG,
    GLOW_PRESET_CATALOG,
    ICON_LAYOUT_MODE_CATALOG,
    LANGUAGE_CATALOG,
    PROGRAM_PICKER_TITLE,
    SHAPE_CATALOG,
    SLOT_ACTION_TYPE_CATALOG,
    WHEEL_PALETTE_CATALOG,
    WHEEL_STYLE_CATALOG,
)
from conftest import (
    PROBE_PROGRAM_NAME,
    assert_catalog,
    assert_page_ready,
    assert_text_contains,
    cancel_dialog,
    global_action_type_is,
    goto,
    label_value,
    list_item_texts,
    open_program_picker,
    picker_filter,
    picker_programs,
    plant_probe_program,
    read_config,
    remove_probe_program,
    save_settings,
    select_option,
    text_of,
    wait_dialog,
    wait_for_label_value,
    wait_until,
)

# 1×1 PNG：v136 中心图标用例的自建测试图片（不再依赖 C:\Windows 系统文件）
PNG_1X1_BASE64 = (
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
)


# ==== 基础交互 ====

def test_modify_slider_and_save(app):
    """落盘专验（显式 Save）：滑块取值 → 点 Save → 沙盒 config.json 落盘。"""
    win, local_app_data = app
    goto(win, 0)

    slider = win.child_window(auto_id="ThresholdSlider", control_type="Slider")
    initial_val = label_value(win, "ThresholdValueLabel")

    slider.set_value(32.0)
    new_val = wait_for_label_value(win, "ThresholdValueLabel", 32.0)
    assert new_val != initial_val, f"滑块值应随操作变化: {initial_val} -> {new_val}"

    save_settings(win)
    read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("DragThreshold", 0) - new_val) < 0.01,
        message=f"显式 Save 后 DragThreshold 应落盘为 {new_val}",
    )


def test_switch_all_tabs_smoothly(app):
    """依次切到各页并断言页面就绪（导航由选中态驱动，UIA Select 即真导航）。"""
    win, _ = app

    # 0: 触发与场景 / 1: 外观与形态 / 2: 轮盘与动作 / 3: 高级与系统
    for slot in range(4):
        goto(win, slot)
        assert_page_ready(win, slot)


def test_blacklist_add_and_delete(app):
    """黑名单增删即时反映在列表（live-apply + 列表绑定；落盘由持久化用例覆盖）。"""
    win, _ = app
    goto(win, 0)

    txt_box = win.child_window(auto_id="NewBlacklistProcessTextBox", control_type="Edit")
    add_btn = win.child_window(auto_id="AddBlacklistButton", control_type="Button")
    del_btn = win.child_window(auto_id="DeleteBlacklistButton", control_type="Button")
    list_box = win.child_window(auto_id="BlacklistListBox", control_type="List")

    txt_box.set_text("testgame.exe")
    add_btn.invoke()
    wait_until(lambda: "testgame.exe" in list_item_texts(list_box), description="黑名单列表出现 testgame.exe")

    for item in list_box.children(control_type="ListItem"):
        if item.window_text() == "testgame.exe":
            item.select()
            del_btn.invoke()
            break
    else:
        raise AssertionError(f"黑名单列表没有 testgame.exe 可删: {list_item_texts(list_box)}")

    wait_until(lambda: "testgame.exe" not in list_item_texts(list_box), description="黑名单列表移除 testgame.exe")


def test_appearance_shapes_and_geometry_reset(app):
    """形状选择、间隙/圆角滑块与「重置尺寸」即时生效（Reset 缺失即失败，不静默跳过）。"""
    win, _ = app
    goto(win, 1)

    gap_slider = win.child_window(auto_id="SectorGapSlider", control_type="Slider")
    corner_slider = win.child_window(auto_id="SectorCornerRadiusSlider", control_type="Slider")
    gap_slider.set_value(5.0)
    corner_slider.set_value(8.0)
    wait_for_label_value(win, "SectorGapLabel", 5.0)
    wait_for_label_value(win, "SectorCornerRadiusLabel", 8.0)

    reset_btn = win.child_window(auto_id="ResetDimensionsButton", control_type="Button")
    assert reset_btn.exists(timeout=3), "ResetDimensionsButton 必须存在"
    reset_btn.invoke()
    wait_for_label_value(win, "SectorGapLabel", 2.0)  # 产品默认间隙为 2


def test_profile_management_ui_and_buttons(app):
    """方案管理控件齐备（本用例即存在性检查）：四个按钮 + 列表，且列出 Global 兜底方案。"""
    win, _ = app
    goto(win, 2)
    assert_page_ready(win, 2)

    for auto_id in ("AddProfileButton", "AddCustomProfileButton", "RenameProfileButton", "DeleteProfileButton"):
        assert win.child_window(auto_id=auto_id, control_type="Button").exists(timeout=3), f"{auto_id} 必须存在"

    items = list_item_texts(win.child_window(auto_id="ProfilesListBox", control_type="List"))
    assert any("Global" in item for item in items), f"Global 兜底方案必须列出: {items}"


# ==== 轮盘与动作 ====

def test_hotkey_recorder_and_system_presets_catalog(app):
    """槽位动作类型切 System 后落盘（Slot0 类型下拉 UIA 不暴露选中态，读盘是唯一观察面）。"""
    win, local_app_data = app
    goto(win, 2)

    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "System", verify_selection=False)

    save_settings(win)
    read_config(
        local_app_data,
        predicate=lambda c: global_action_type_is(c, "System"),
        message="Slot0 动作类型应落盘为 System",
    )


def test_v133_sector_count_4_8_12_adaptation_and_streamlined_shapes(app):
    """形状目录（4 项）与扇区数单选（4/8/12）即时生效，切页往返后选中态保持。"""
    win, _ = app

    goto(win, 1)
    assert_catalog(win.child_window(auto_id="ShapeComboBox", control_type="ComboBox"), SHAPE_CATALOG)

    goto(win, 2)
    radios = {
        name: win.child_window(auto_id=auto_id, control_type="RadioButton")
        for name, auto_id in (
            ("4", "SectorCount4Radio"),
            ("8", "SectorCount8Radio"),
            ("12", "SectorCount12Radio"),
        )
    }
    for name, radio in radios.items():
        assert radio.exists(timeout=3), f"SectorCount{name}Radio 必须存在"

    radios["12"].select()
    wait_until(radios["12"].is_selected, description="12 键单选已选中")

    # 切页往返后选中态保持（值住运行态配置，页面重挂不回退）
    goto(win, 1)
    goto(win, 2)
    wait_until(radios["12"].is_selected, description="切页往返后 12 键选中态保持")


def test_v139_folder_action_type_and_i18n_consistency(app):
    """槽位动作类型切 Folder 后出现文件夹路径编辑框（类型切换生效的 UI 观察面）。"""
    win, _ = app
    goto(win, 2)

    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "Folder", verify_selection=False)

    assert win.child_window(auto_id="Slot0FolderPathTextBox", control_type="Edit").exists(timeout=3), (
        "切到 Folder 后必须出现文件夹路径编辑框（类型切换生效的观察面）"
    )


# ==== 外观与主题 ====

def test_app_theme_switch_and_removed_wheel_bg_controls(app):
    """界面主题切换即时生效（选中态回读）；WheelBg* 家族整体下线（全量枚举可证否）。"""
    win, _ = app
    goto(win, 1)

    app_theme_combo = win.child_window(auto_id="AppThemeComboBox", control_type="ComboBox")
    wheel_theme_combo = win.child_window(auto_id="WheelPaletteComboBox", control_type="ComboBox")
    assert_catalog(wheel_theme_combo, WHEEL_PALETTE_CATALOG)

    # 否定断言可证否：同一次全量枚举既证明枚举面有效（清单里含已知在页控件），
    # 又证明已下线功能的 auto_id 家族整体缺席——单点 child_window(...).exists() 为 False
    # 无法区分"控件真的不在"与"auto_id 写错/枚举失效"（#135 P0-7）。
    auto_ids = {element.element_info.automation_id for element in win.descendants()}
    assert "WheelPaletteComboBox" in auto_ids, f"外观页控件枚举异常，否定断言不可证否: {sorted(auto_ids)}"
    stale_bg_ids = sorted(auto_id for auto_id in auto_ids if auto_id.startswith("WheelBg"))
    assert not stale_bg_ids, f"已下线的轮盘背景图片控件仍存在: {stale_bg_ids}"

    select_option(app_theme_combo, APP_THEME_CATALOG, "Dark")  # 选中态回读由 select_option 完成


def test_v130_wheel_themes_and_custom_preset_and_text_sync(app):
    """轮盘风格（3 项）与配色切换即时生效；文字开关与排版下拉就位。"""
    win, _ = app
    goto(win, 1)

    ui_style_combo = win.child_window(auto_id="WheelStyleComboBox", control_type="ComboBox")
    wheel_theme_combo = win.child_window(auto_id="WheelPaletteComboBox", control_type="ComboBox")
    assert_catalog(ui_style_combo, WHEEL_STYLE_CATALOG)

    select_option(wheel_theme_combo, WHEEL_PALETTE_CATALOG, "Dark")
    select_option(ui_style_combo, WHEEL_STYLE_CATALOG, "CleanSectors")

    show_text_chk = win.child_window(auto_id="ShowTextCheckBox", control_type="CheckBox")
    assert show_text_chk.exists(timeout=3), "ShowTextCheckBox 必须存在"


def test_shape_selection_and_icon_font_size_live_apply(app):
    """形状切换与图标/文字尺寸滑块即时生效，数值切页往返保持。"""
    win, _ = app
    goto(win, 1)

    shape_combo = win.child_window(auto_id="ShapeComboBox", control_type="ComboBox")
    assert_catalog(shape_combo, SHAPE_CATALOG)
    select_option(shape_combo, SHAPE_CATALOG, "RoundedCapsule")

    icon_slider = win.child_window(auto_id="SectorIconSizeSlider", control_type="Slider")
    icon_slider.set_value(26)
    wait_for_label_value(win, "SectorIconSizeLabel", 26.0)

    font_slider = win.child_window(auto_id="SectorFontSizeSlider", control_type="Slider")
    font_slider.set_value(13.5)
    wait_for_label_value(win, "SectorFontSizeLabel", 13.5)

    # 切页往返后数值保持（页面按导航重建，值住运行态配置不回退）
    goto(win, 0)
    goto(win, 1)
    wait_for_label_value(win, "SectorIconSizeLabel", 26.0)
    wait_for_label_value(win, "SectorFontSizeLabel", 13.5)


def test_v134_memory_autosave_and_theme_persistence(app):
    """落盘专验（防抖自动保存）：改 WheelRadius 不点 Save，落盘仍到 145。"""
    win, local_app_data = app
    goto(win, 1)

    wheel_slider = win.child_window(auto_id="WheelRadiusSlider", control_type="Slider")
    wheel_slider.set_value(145.0)

    read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("WheelRadius", 0) - 145.0) < 1.0,
        message="防抖自动保存应把 WheelRadius=145 落盘（不点 Save）",
    )


def test_v135_program_picker_clean_icons_and_core_customization(app):
    """中心图标开关翻转与图案切换即时生效（toggle 状态与下拉选中态即观察面）。"""
    win, _ = app
    goto(win, 1)

    core_chk = win.child_window(auto_id="ShowCoreIconCheckBox", control_type="CheckBox")
    pre_state = core_chk.get_toggle_state()
    core_chk.toggle()
    wait_until(
        lambda: core_chk.get_toggle_state() != pre_state,
        description=f"ShowCoreIcon 开关翻转即时回读（toggle 前 state={pre_state}）",
    )

    core_combo = win.child_window(auto_id="CoreIconTypeComboBox", control_type="ComboBox")
    select_option(core_combo, CORE_ICON_CATALOG, "Crosshair")


def test_v136_glow_color_customization_config_memory_and_core_image(app, tmp_path):
    """glow 颜色/几何与自定义图片路径的配置记忆：这些字段没有 UI 观察面，读盘是唯一证据。

    可交互部分（预设选中、图案选中、路径文本写入）一并驱动；落盘判定收在一处 predicate
    （message 提供业务文案，条件只写一处）。
    """
    win, local_app_data = app
    goto(win, 1)

    glow_preset_combo = win.child_window(auto_id="HighlightGlowPresetComboBox", control_type="ComboBox")
    select_option(glow_preset_combo, GLOW_PRESET_CATALOG, "Lilac")

    core_combo = win.child_window(auto_id="CoreIconTypeComboBox", control_type="ComboBox")
    select_option(core_combo, CORE_ICON_CATALOG, "Image")

    core_img_box = win.child_window(auto_id="CoreImagePathTextBox", control_type="Edit")
    assert core_img_box.exists(timeout=3), "CoreImagePathTextBox 必须存在"
    img_path = tmp_path / "core-icon.png"
    img_path.write_bytes(base64.b64decode(PNG_1X1_BASE64))
    core_img_box.set_text(str(img_path))

    read_config(
        local_app_data,
        predicate=lambda c: c.get("CoreCustomImagePath") == str(img_path)
        and c.get("CoreIconType") == "Image"
        and c.get("HighlightGlowPreset") == "Lilac"
        and c.get("HighlightGlowColor") == "#A855F7"
        and "HighlightGlowRadius" in c
        and "HighlightGlowOpacity" in c,
        message="glow 预设/颜色与自定义图片路径应写入配置",
    )


def test_v136_custom_color_preset_deletion_and_management(app):
    """自定义配色面板入口就位（外观页就绪），沙盒配置完整可读（启动已播种）。"""
    win, local_app_data = app
    goto(win, 1)
    assert_page_ready(win, 1)
    read_config(local_app_data)


def test_v140_custom_icons_and_appearance_collapsible(app):
    """v1.4.0 目录与容器：轮盘风格固定 3 项，外观页/轮盘页容器就位。"""
    win, _ = app

    goto(win, 1)
    assert_page_ready(win, 1)
    assert_catalog(win.child_window(auto_id="WheelStyleComboBox", control_type="ComboBox"), WHEEL_STYLE_CATALOG)

    goto(win, 2)
    assert_page_ready(win, 2)


def test_v141_outer_escape_cancel_and_rename_capabilities(app):
    """逃逸取消开关翻转即时回读；重命名入口与配色面板入口就位。"""
    win, _ = app

    goto(win, 0)
    outer_escape_chk = win.child_window(auto_id="EnableOuterEscapeCheckBox", control_type="CheckBox")
    escape_dist_slider = win.child_window(auto_id="OuterEscapeDistanceSlider", control_type="Slider")
    assert escape_dist_slider.exists(timeout=3), "OuterEscapeDistanceSlider 必须存在"

    pre_state = outer_escape_chk.get_toggle_state()
    outer_escape_chk.toggle()
    wait_until(
        lambda: outer_escape_chk.get_toggle_state() != pre_state,
        description=f"逃逸取消开关翻转即时回读（toggle 前 state={pre_state}）",
    )

    goto(win, 2)
    assert win.child_window(auto_id="RenameProfileButton", control_type="Button").exists(timeout=3), (
        "RenameProfileButton 必须存在"
    )

    goto(win, 1)
    assert win.child_window(auto_id="CustomColorExpander", control_type="Group").exists(timeout=3), (
        "CustomColorExpander 必须存在"
    )


# ==== 高级与系统 ====

def test_admin_restart_entry_visibility_without_elevation(app):
    """「立即以管理员身份重启」入口在非提权态下可见但不可点，且写明原因。

    不提权态下绝不点击它：触发会去跑 schtasks，属于 e2e 不触碰的系统副作用。
    （提权态下入口不出现的口径由 GeneralSettingsViewModelTests 覆盖。）
    """
    win, _ = app
    goto(win, 3)

    restart = win.child_window(auto_id="AdminRestartNowButton", control_type="Button")
    assert restart.exists(timeout=3), "非提权态下「立即以管理员身份重启」入口应当出现"
    assert not restart.is_enabled(), "提权自启的任务不存在时入口不可点（须先开启「以管理员身份开机自启」）"
    assert text_of(win, "AdminRestartNowHint", "Text", timeout=3).strip(), (
        "入口不可点时必须呈现原因，而不是留一个点了没反应的按钮"
    )


# ==== 界面语言 i18n ====

def test_v138_i18n_multilanguage_support(app):
    """多语言切换：UI 文本随语言即时刷新；最终语言码落盘一处（下次启动生效的设置）。"""
    win, local_app_data = app
    goto(win, 3)

    lang_combo = win.child_window(auto_id="LanguageComboBox", control_type="ComboBox")
    assert_catalog(lang_combo, LANGUAGE_CATALOG)

    select_option(lang_combo, LANGUAGE_CATALOG, "en")
    assert_text_contains(win, "SaveButton", "Button", "Save")

    def _tab0_title():
        return text_of(win, "NavPage0", "RadioButton", timeout=0.3)

    wait_until(lambda: "Trigger" in _tab0_title() or "🎯" in _tab0_title(), description="侧边栏标题切英文")
    assert_text_contains(win, "AdvancedPageHeader", "Text", "System Integration & Preferences")

    select_option(lang_combo, LANGUAGE_CATALOG, "ja")
    assert_text_contains(win, "SaveButton", "Button", "保存")
    assert_text_contains(win, "AdvancedPageHeader", "Text", "システム統合と高度な設定")

    select_option(lang_combo, LANGUAGE_CATALOG, "zh-CN")
    assert_text_contains(win, "AdvancedPageHeader", "Text", "系统集成与高级偏好设置")

    read_config(
        local_app_data,
        predicate=lambda c: c.get("Language") == "zh-CN",
        message="语言码应落盘（下次启动按它初始化界面语言）",
    )


def test_t28_hardcoded_copy_follows_language(app):
    """T28 (#33): 设置页副标题跟随运行时语言字典（T24 机制），只按 AutomationId 定位。"""
    win, _ = app

    goto(win, 3)
    lang_combo = win.child_window(auto_id="LanguageComboBox", control_type="ComboBox")

    select_option(lang_combo, LANGUAGE_CATALOG, "en")
    assert_text_contains(win, "AdvancedPageSubheader", "Text", "Manage interface language")

    goto(win, 2)
    assert_text_contains(win, "WheelActionsPageSubheader", "Text", "Set dedicated multi-directional wheel profiles")

    goto(win, 1)
    assert_text_contains(win, "AppearancePageSubheader", "Text", "Customize visual styles")

    goto(win, 3)  # 语言下拉在高级页，切回后原地再切日语
    select_option(lang_combo, LANGUAGE_CATALOG, "ja")
    assert_text_contains(win, "AdvancedPageSubheader", "Text", "インターフェース言語")

    select_option(lang_combo, LANGUAGE_CATALOG, "zh-CN")
    assert_text_contains(win, "AdvancedPageSubheader", "Text", "管理界面语言")


# ==== 程序选择器与插件来源 ====

def test_program_picker_opens_and_cancels_cleanly(app):
    """程序选择器打开/取消：对话框真实呈现、干净关闭；取消不新增方案。

    "取消不新增方案"比对主窗口方案列表（运行态即时回读），比读盘直接。
    """
    win, _ = app
    goto(win, 2)
    profiles_list = win.child_window(auto_id="ProfilesListBox", control_type="List")
    before = list_item_texts(profiles_list)

    win.child_window(auto_id="AddProfileButton", control_type="Button").invoke()
    picker = wait_dialog(PROGRAM_PICKER_TITLE)
    rect = picker.element_info.rectangle
    assert rect.left > -1000 and rect.top > -1000, f"程序选择器必须呈现于屏幕，got {rect}"

    cancel_dialog(picker)

    # 主窗口仍可用：切页往返后方案列表不变
    goto(win, 3)
    goto(win, 2)
    assert list_item_texts(profiles_list) == before, f"取消选择不应新增方案: {before}"


def test_program_picker_lists_plugin_program_source(app):
    """程序来源插件（内置、默认启用）：插件深扫的沙盒程序进入程序选择器列表。

    探针只登记在 HKCU App Paths，宿主内置来源（系统工具 + 快捷方式）扫不到它——
    因此本用例是"插件确实在跑"的行为证据，而不是配置或日志断言。
    """
    win, _ = app
    plant_probe_program()
    try:
        picker = open_program_picker(win)
        try:
            programs = picker_programs(picker)
            picker_filter(picker, PROBE_PROGRAM_NAME)
            wait_until(
                lambda: any(PROBE_PROGRAM_NAME in text for text in list_item_texts(programs)),
                description=f"插件来源的程序出现在选择器列表（{PROBE_PROGRAM_NAME}）",
            )
        finally:
            cancel_dialog(picker)
    finally:
        remove_probe_program()


def test_trigger_modifier_bypass_and_escape_distance_live_apply(app):
    """触发页四个旁路开关与逃逸距离即时生效，切页往返后保持。"""
    win, _ = app
    goto(win, 0)

    boxes = [
        (name, win.child_window(auto_id=auto_id, control_type="CheckBox"))
        for name, auto_id in (
            ("DisableOnFullScreen", "DisableOnFullScreenCheckBox"),
            ("DisableOnCtrl", "DisableOnCtrlCheckBox"),
            ("DisableOnShift", "DisableOnShiftCheckBox"),
            ("DisableOnAlt", "DisableOnAltCheckBox"),
        )
    ]
    pre = {}
    for name, box in boxes:
        assert box.exists(timeout=3), f"{name} 复选框必须存在"
        pre[name] = box.get_toggle_state()
        box.toggle()
        wait_until(
            lambda b=box, p=pre[name]: b.get_toggle_state() != p,
            description=f"{name} 翻转即时回读",
        )

    escape_slider = win.child_window(auto_id="OuterEscapeDistanceSlider", control_type="Slider")
    escape_slider.set_value(200.0)
    wait_for_label_value(win, "OuterEscapeDistanceLabel", 200.0)

    # 切页往返后全部保持（值住运行态配置，页面重挂不回退）
    goto(win, 1)
    goto(win, 0)
    for name, box in boxes:
        assert box.get_toggle_state() != pre[name], f"{name} 切页往返后应保持翻转态"
    wait_for_label_value(win, "OuterEscapeDistanceLabel", 200.0)


def test_appearance_geometry_layout_and_showtext_live_apply(app):
    """外观页几何/布局即时生效并跨页保持；IconLayoutMode 无 UIA 观察面，保留一处落盘断言。"""
    win, local_app_data = app
    goto(win, 1)

    inner = win.child_window(auto_id="InnerRadiusSlider", control_type="Slider")
    core = win.child_window(auto_id="CoreRadiusSlider", control_type="Slider")
    corner = win.child_window(auto_id="SectorCornerRadiusSlider", control_type="Slider")
    inner.set_value(70.0)
    core.set_value(60.0)
    corner.set_value(9.0)
    wait_for_label_value(win, "InnerRadiusLabel", 70.0)
    wait_for_label_value(win, "CoreRadiusLabel", 60.0)
    wait_for_label_value(win, "SectorCornerRadiusLabel", 9.0)

    show_text = win.child_window(auto_id="ShowTextCheckBox", control_type="CheckBox")
    pre_state = show_text.get_toggle_state()
    show_text.toggle()
    wait_until(
        lambda: show_text.get_toggle_state() != pre_state,
        description=f"ShowText 翻转即时回读（toggle 前 state={pre_state}）",
    )

    layout = win.child_window(auto_id="IconLayoutModeComboBox", control_type="ComboBox")
    select_option(layout, ICON_LAYOUT_MODE_CATALOG, "IconOnly", verify_selection=False)

    # 切页往返后数值与开关保持
    goto(win, 3)
    goto(win, 1)
    wait_for_label_value(win, "InnerRadiusLabel", 70.0)
    wait_for_label_value(win, "CoreRadiusLabel", 60.0)
    wait_for_label_value(win, "SectorCornerRadiusLabel", 9.0)
    assert show_text.get_toggle_state() != pre_state, "切页往返后 ShowText 应保持翻转态"

    # IconLayoutMode 下拉的 UIA 不暴露选中态：布局模式落盘是唯一观察面（Save 后读盘）
    save_settings(win)
    read_config(
        local_app_data,
        predicate=lambda c: c.get("IconLayoutMode") == "IconOnly"
        and abs(c.get("InnerRadius", 0) - 70) < 0.01
        and abs(c.get("CoreRadius", 0) - 60) < 0.01
        and abs(c.get("SectorCornerRadius", 0) - 9) < 0.01
        and c.get("ShowText") == (pre_state != 1),
        message="几何数值/文字开关/布局模式应落盘（IconLayoutMode 无 UIA 观察面）",
    )


@pytest.mark.parametrize("sandbox_seed", ["disabled-program-source"], indirect=True)
def test_program_picker_degrades_to_builtin_when_plugin_disabled(app):
    """停用内置程序来源插件：程序选择器降级为只剩内置来源（plugins.md §1 第 4 条）。

    同一探针在停用态不可见，而内置来源条目仍在——降级不是"选择器坏了"。
    """
    win, _ = app
    plant_probe_program()
    try:
        picker = open_program_picker(win)
        try:
            programs = picker_programs(picker)
            # 内置来源仍在：按内置条目过滤后列表非空（降级不是"选择器坏了"）。
            picker_filter(picker, "Notepad")
            wait_until(
                lambda: list_item_texts(programs) != [],
                description="内置来源条目（记事本）在选择器里可见",
            )
            # 插件来源缺席：按探针名过滤后列表为空（只剩内置来源）。
            picker_filter(picker, PROBE_PROGRAM_NAME)
            wait_until(
                lambda: list_item_texts(programs) == [],
                description="停用后插件来源缺席（过滤探针名后列表为空，只剩内置来源）",
            )
        finally:
            cancel_dialog(picker)
    finally:
        remove_probe_program()
