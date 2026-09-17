"""对话框交互 e2e：图标选择器、颜色选择器与自定义配色预设的增删。

xUnit 锁的是对话框 VM 的编排逻辑；这里锁真实窗口交互：打开、过滤、选中、确认写回、
取消不改动，以及预设保存/删除链路上的真实提示框与确认框应答。
"""

import pytest
from conftest import (
    answer_messagebox,
    click_and_confirm_yes,
    goto,
    read_config,
    save_settings,
    select_option,
    text_of,
    wait_dialog,
    wait_dialog_closed,
    wait_until,
)

ICON_PICKER_TITLE = "选择动作矢量图标 - StarPie"
COLOR_PICKER_TITLE = "色彩选择器与屏幕吸管 (Color Picker) - StarPie"
SAVE_PRESET_TITLE = "保存配色预设"

CORE_ICON_CATALOG = ("Exit", "Crosshair", "Windows", "Dot", "Home", "Power", "Compass", "CatPaw", "Custom", "Image")

PRESET_NAME = "e2e 配色预设"
CUSTOM_COLOR = "#80123456"


def expand_custom_colors(win) -> None:
    """展开"自定义高级配色"面板（折叠态下内容不在 UIA 树里，须先展开才可寻址）。"""
    box = win.child_window(auto_id="CustomSectorBgTextBox", control_type="Edit")
    if box.exists(timeout=1.0):
        return
    expander = win.child_window(auto_id="CustomColorExpander", control_type="Group")
    assert expander.exists(timeout=3), "CustomColorExpander 必须存在"
    expander.click_input()
    wait_until(
        lambda: box.exists(timeout=0.3),
        timeout=5.0,
        description="自定义配色面板展开（色值编辑框出现）",
    )


def test_icon_picker_confirm_writes_core_icon(app):
    """中心图标：选自定义类型 → 图标选择器过滤选中 → 确认写回并落盘。"""
    win, local_app_data = app
    goto(win, 1)

    core_combo = win.child_window(auto_id="CoreIconTypeComboBox", control_type="ComboBox")
    assert core_combo.exists(timeout=3), "CoreIconTypeComboBox 必须存在"
    select_option(core_combo, CORE_ICON_CATALOG, "Custom")

    pick = win.child_window(auto_id="PickCoreIconButton", control_type="Button")
    assert pick.exists(timeout=3), "选 Custom 后应出现「选择图标」按钮"
    pick.invoke()

    picker = wait_dialog(ICON_PICKER_TITLE)
    search = picker.child_window(auto_id="SearchTextBox", control_type="Edit")
    assert search.exists(timeout=5), "图标选择器搜索框必须存在"
    search.set_edit_text("Copy")

    card = picker.child_window(title="Copy", control_type="Text", found_index=0)
    wait_until(
        lambda: card.exists(timeout=0.3),
        timeout=5.0,
        description="过滤后出现 Copy 图标卡片",
    )
    card.click_input()

    picker.child_window(auto_id="ConfirmButton", control_type="Button").invoke()
    wait_dialog_closed(picker)

    config = read_config(local_app_data, predicate=lambda c: c.get("CoreCustomIconKey") == "Copy")
    assert config.get("CoreCustomIconKey") == "Copy", f"选中图标应落盘: {config.get('CoreCustomIconKey')!r}"


def _rgb_channels(hex_text: str) -> tuple:
    """#AARRGGBB / #RRGGBB → (R, G, B)（颜色选择器结果含 alpha 通道）。"""
    digits = (hex_text or "").strip().lstrip("#")
    if len(digits) == 8:
        digits = digits[2:]
    assert len(digits) == 6, f"色值应为 6/8 位十六进制: {hex_text!r}"
    return tuple(int(digits[i : i + 2], 16) for i in (0, 2, 4))


def test_color_picker_confirm_writes_custom_color(app):
    """色值取色：颜色选择器写十六进制 → 确认后回填到编辑框，保存后与配置一致。"""
    win, local_app_data = app
    goto(win, 1)
    expand_custom_colors(win)

    win.child_window(auto_id="PickColor_CustomSectorBg", control_type="Button").invoke()
    dialog = wait_dialog(COLOR_PICKER_TITLE)

    hex_box = dialog.child_window(auto_id="HexInputBox", control_type="Edit")
    assert hex_box.exists(timeout=5), "颜色选择器十六进制输入框必须存在"
    hex_box.set_edit_text("#123456")
    dialog.child_window(auto_id="OkButton", control_type="Button").invoke()
    wait_dialog_closed(dialog)

    text = text_of(win, "CustomSectorBgTextBox", "Edit", timeout=3)
    # 色轮/HSV 往返会让通道有 ±1 级舍入，故按通道容差断言（不是"任意值都算过"）
    got = _rgb_channels(text)
    for index, (actual, expected) in enumerate(zip(got, (0x12, 0x34, 0x56))):
        assert abs(actual - expected) <= 4, f"通道 {index} 应≈{expected:#04x}，实际 {actual:#04x}（色值 {text!r}）"

    # 自定义配色键入只写运行态配置、落盘留给既有落盘时机 → 点 Save 后再验磁盘
    save_settings(win)
    config = read_config(
        local_app_data,
        predicate=lambda c: (c.get("CustomSectorBg") or "").upper() == text.upper(),
    )
    assert (config.get("CustomSectorBg") or "").upper() == text.upper(), "落盘色值应与界面一致"


def test_color_picker_cancel_keeps_value(app):
    """颜色选择器取消：色值不变（取消无业务语义，不动配置）。"""
    win, local_app_data = app
    goto(win, 1)
    expand_custom_colors(win)

    before = text_of(win, "CustomSectorBgTextBox", "Edit", timeout=3)

    win.child_window(auto_id="PickColor_CustomSectorBg", control_type="Button").invoke()
    dialog = wait_dialog(COLOR_PICKER_TITLE)
    dialog.child_window(auto_id="HexInputBox", control_type="Edit").set_edit_text("#00FF00")
    dialog.child_window(auto_id="CancelButton", control_type="Button").invoke()
    wait_dialog_closed(dialog)

    after = text_of(win, "CustomSectorBgTextBox", "Edit", timeout=3)
    assert after == before, f"取消后色值不得变化: {before!r} -> {after!r}"
    config = read_config(local_app_data)
    assert (config.get("CustomSectorBg") or "").upper() == before.upper()


def test_custom_color_preset_save_and_delete(app):
    """自定义配色预设：命名保存（输入框 + 成功提示）→ 删除（确认框 + 成功提示）→ 落盘一致。"""
    win, local_app_data = app
    pid = win.process_id()
    goto(win, 1)
    expand_custom_colors(win)

    # 先写一个可辨识的色值（保存的预设要带上它）；键入只写运行态配置，落盘留到保存预设时
    color_box = win.child_window(auto_id="CustomSectorBgTextBox", control_type="Edit")
    color_box.set_edit_text(CUSTOM_COLOR)
    assert text_of(win, "CustomSectorBgTextBox", "Edit", timeout=3).upper() == CUSTOM_COLOR.upper()

    win.child_window(auto_id="SaveCustomColorPresetButton", control_type="Button").invoke()
    dialog = wait_dialog(SAVE_PRESET_TITLE)
    edit = dialog.child_window(auto_id="InputTextBox", control_type="Edit")
    assert edit.exists(timeout=5), "保存预设对话框缺少输入框"
    edit.set_edit_text(PRESET_NAME)
    dialog.child_window(auto_id="OkButton", control_type="Button").invoke()
    wait_dialog_closed(dialog)
    answer_messagebox(pid, button_id="1")  # 保存成功提示

    config = read_config(
        local_app_data,
        predicate=lambda c: any(
            (p or {}).get("Name") == PRESET_NAME for p in (c.get("CustomColorPresets") or [])
        ),
    )
    preset = next(p for p in config["CustomColorPresets"] if p.get("Name") == PRESET_NAME)
    assert (preset.get("SectorBg") or "").upper() == CUSTOM_COLOR.upper(), f"预设应带上保存时的色值: {preset}"
    assert (config.get("CustomSectorBg") or "").upper() == CUSTOM_COLOR.upper(), (
        f"保存预设触发的落盘应带上色值: {config.get('CustomSectorBg')!r}"
    )

    # 保存后新预设即被选中 → 面板里的删除入口可见
    delete_btn = win.child_window(auto_id="DeletePresetInPanelButton", control_type="Button")
    wait_until(
        lambda: delete_btn.exists(timeout=0.3),
        timeout=5.0,
        description="保存后删除预设入口出现（新预设已选中）",
    )
    click_and_confirm_yes(win, "DeletePresetInPanelButton")
    answer_messagebox(pid, button_id="1")  # 删除成功提示

    read_config(
        local_app_data,
        predicate=lambda c: all(
            (p or {}).get("Name") != PRESET_NAME for p in (c.get("CustomColorPresets") or [])
        ),
    )
