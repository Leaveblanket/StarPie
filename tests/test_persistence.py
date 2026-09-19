"""配置持久化 e2e：防抖落盘不丢改动、同沙盒重启回读、损坏/缺键降级、导入导出真实对话框。

xUnit 锁的是配置服务的读写边界；这里锁的是真实进程生命周期上的持久化语义——
"改了就关/就退"不能丢数据，重启后必须读回，坏配置不能让应用起不来。

本文件是落盘链路的专验所在（显式 Save / 防抖自动保存 / 关窗冲刷 / 退出冲刷 / 重启回读 /
导入导出 / 落盘失败），其余交互用例的落盘断言按分层口径收敛到这些路径上。
"""

import os
import time

import pytest
from catalogs import APP_THEME_CATALOG, EXPORT_DIALOG_TITLE, IMPORT_DIALOG_TITLE
from conftest import (
    answer_messagebox,
    close_console,
    exit_via_test_message,
    goto,
    label_value,
    read_config,
    save_settings,
    select_option,
    start_app,
    stop_app,
    wait_dialog,
    wait_dialog_closed,
    wait_for_label_value,
    wait_until,
)


def _wait_file_dialog_edit(dialog, timeout: float = 10.0):
    """等系统文件对话框的"文件名"编辑框就位并返回它。

    对话框内容在窗口出现后才异步建齐，故必须轮询等待；命中顺序为 Vista 版 id
    `1001` → 旧版 `1148` → `FileNameControlHost` 里的编辑框。**绝不回退到"首个 Edit"**：
    文件列表里的列编辑框是重命名入口，往里写路径会触发系统"文件名不能包含下列任何字符"报错。
    """
    makers = (
        lambda: dialog.child_window(auto_id="1001", control_type="Edit"),
        lambda: dialog.child_window(auto_id="1148", control_type="Edit"),
        lambda: dialog.child_window(auto_id="FileNameControlHost", control_type="ComboBox").child_window(
            control_type="Edit", found_index=0
        ),
    )
    deadline = time.time() + timeout
    while True:
        for make in makers:
            try:
                edit = make()
                if edit.exists(timeout=1.0):
                    return edit
            except Exception:
                continue
        if time.time() >= deadline:
            raise AssertionError(f"系统文件对话框的文件名编辑框未在时限内出现（{timeout}s）")
        time.sleep(0.2)


def _answer_file_dialog(dialog, path: str) -> None:
    """在现代文件对话框里填完整路径并点主按钮（保存/打开；主按钮 id 恒为 1）。"""
    _wait_file_dialog_edit(dialog).set_edit_text(path)

    button = dialog.child_window(auto_id="1", control_type="Button")
    assert button.exists(timeout=5.0), "文件对话框缺少主按钮（保存/打开）"
    button.invoke()
    wait_dialog_closed(dialog)


def test_pending_save_flushed_when_console_closes(app):
    """改动后立刻关闭设置台：挂起的防抖保存被冲刷落盘（不点 Save 也不丢）。"""
    win, local_app_data = app
    goto(win, 0)

    slider = win.child_window(auto_id="ThresholdSlider", control_type="Slider")
    slider.set_value(33.0)
    wait_for_label_value(win, "ThresholdValueLabel", 33.0)

    # 不等防抖窗口，直接关窗：关闭序列的 FlushPendingSave 步骤必须把改动写盘
    close_console(win)
    read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("DragThreshold", 0) - 33) < 0.01,
        message="关窗冲刷应把 DragThreshold=33 落盘",
    )


def test_pending_save_flushed_when_process_exits(app):
    """改动后立刻退出进程：退出编排的兜底落盘把改动写盘。"""
    win, local_app_data = app
    goto(win, 0)

    slider = win.child_window(auto_id="ThresholdSlider", control_type="Slider")
    slider.set_value(44.0)
    wait_for_label_value(win, "ThresholdValueLabel", 44.0)

    # 退出走测试实例退出消息（真实退出路径：落盘 → 释托盘 → 关应用）；不点 Save、不等防抖
    exit_via_test_message(win.process_id())
    read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("DragThreshold", 0) - 44) < 0.01,
        message="退出冲刷应把 DragThreshold=44 落盘",
    )


def test_settings_survive_restart(sandbox_env):
    """同一沙盒内重启：改动落盘后新进程读回（真实启动加载路径）。"""
    env, local_app_data = sandbox_env

    proc, win = start_app(env)
    try:
        goto(win, 0)
        win.child_window(auto_id="ThresholdSlider", control_type="Slider").set_value(41.0)
        wait_for_label_value(win, "ThresholdValueLabel", 41.0)

        goto(win, 1)
        app_theme = win.child_window(auto_id="AppThemeComboBox", control_type="ComboBox")
        select_option(app_theme, APP_THEME_CATALOG, "Dark")
        show_text = win.child_window(auto_id="ShowTextCheckBox", control_type="CheckBox")
        show_text.toggle()  # 预置默认 true → 关掉，作为布尔项的重启回读凭据
        save_settings(win)
        read_config(
            local_app_data,
            predicate=lambda c: c.get("AppTheme") == "Dark" and c.get("ShowText") is False,
            message="重启前 AppTheme=Dark、ShowText=false 应已落盘",
        )
    finally:
        stop_app(proc)

    proc2, win2 = start_app(env)
    try:
        goto(win2, 0)
        wait_for_label_value(win2, "ThresholdValueLabel", 41.0)

        goto(win2, 1)
        # 布尔项的回读走 UIA toggle 状态（下拉的选中态在冷启动窗口上不保证被 UIA 暴露）
        show_text2 = win2.child_window(auto_id="ShowTextCheckBox", control_type="CheckBox")
        wait_until(
            lambda: show_text2.get_toggle_state() == 0,
            timeout=5.0,
            description="重启后 ShowText 回读为关闭",
        )
    finally:
        stop_app(proc2)


@pytest.mark.parametrize("sandbox_seed", ["corrupt-config"], indirect=True)
def test_corrupt_config_falls_back_to_defaults(app):
    """损坏的 config.json：应用照常可用（回退默认值），且不覆盖损坏文件（不触碰语义）。"""
    win, local_app_data = app

    goto(win, 0)
    wait_for_label_value(win, "ThresholdValueLabel", 25.0)

    # 导航一次即证设置台可用（回退不是"起不来"）
    goto(win, 3)
    goto(win, 0)

    config_path = os.path.join(str(local_app_data), "StarPie", "config.json")
    with open(config_path, "r", encoding="utf-8") as f:
        assert "这不是合法 JSON" in f.read(), "加载失败不得覆盖损坏文件"


@pytest.mark.parametrize("sandbox_seed", ["partial-config"], indirect=True)
def test_partial_config_keeps_model_defaults(app):
    """旧配置缺键：写了的键生效，没写的键取模型默认值。"""
    win, _ = app

    goto(win, 0)
    wait_for_label_value(win, "ThresholdValueLabel", 33.0)  # 预置的 DragThreshold

    goto(win, 1)
    assert abs(label_value(win, "WheelRadiusLabel") - 138.0) < 0.01, "缺键必须回落到模型默认 WheelRadius"


@pytest.mark.parametrize("sandbox_seed", ["partial-config"], indirect=True)
def test_trigger_button_survives_restart(sandbox_env, sandbox_seed):
    """触发键（触发页录制卡）：旧配置缺键回显默认右键；切换落盘后同沙盒重启读回侧键 1。"""
    env, local_app_data = sandbox_env

    proc, win = start_app(env)
    try:
        goto(win, 0)
        option_right = win.child_window(auto_id="TriggerButtonOptionRight", control_type="RadioButton")
        option_side1 = win.child_window(auto_id="TriggerButtonOptionSide1", control_type="RadioButton")
        # 旧配置无 TriggerButton 键：单选回显默认右键（缺键回退）。
        wait_until(
            lambda: option_right.exists(timeout=1.0) and option_right.is_selected(),
            timeout=5.0,
            description="缺键时右键单选回显",
        )

        option_side1.select()
        wait_until(
            lambda: option_side1.is_selected(),
            timeout=3.0,
            description="切到侧键 1 单选回显",
        )
        read_config(
            local_app_data,
            predicate=lambda c: c.get("TriggerButton") == "XButton1",
            message="切换触发键应已落盘（TriggerButton=XButton1）",
        )
    finally:
        stop_app(proc)

    proc2, win2 = start_app(env)
    try:
        goto(win2, 0)
        option_side1_2 = win2.child_window(auto_id="TriggerButtonOptionSide1", control_type="RadioButton")
        wait_until(
            lambda: option_side1_2.exists(timeout=1.0) and option_side1_2.is_selected(),
            timeout=5.0,
            description="重启后触发键回读为侧键 1",
        )
    finally:
        stop_app(proc2)


def test_export_and_import_dialog_roundtrip(app, tmp_path):
    """导出走真实保存对话框落文件；改设置后经真实打开对话框导入，配置回到导出时的值。"""
    win, local_app_data = app
    goto(win, 3)

    export_path = str(tmp_path / "starpie-e2e-backup.json")
    win.child_window(auto_id="ExportConfigButton", control_type="Button").invoke()
    export_dialog = wait_dialog(EXPORT_DIALOG_TITLE)
    _answer_file_dialog(export_dialog, export_path)
    # 导出成功提示（模态 MessageBox，须应答）
    answer_messagebox(win.process_id(), button_id="1")
    wait_until(lambda: os.path.exists(export_path), timeout=5.0, description="导出文件落盘")
    with open(export_path, "r", encoding="utf-8") as f:
        exported = f.read()
    assert "Profiles" in exported, "导出文件必须是完整配置 JSON"

    # 导出后再改一个设置，确保导入有可观察的回退
    goto(win, 0)
    win.child_window(auto_id="ThresholdSlider", control_type="Slider").set_value(52.0)
    wait_for_label_value(win, "ThresholdValueLabel", 52.0)
    save_settings(win)
    read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("DragThreshold", 0) - 52) < 0.01,
        message="导入前 DragThreshold=52 应已落盘",
    )

    goto(win, 3)  # 回到高级页：导入入口在此页
    win.child_window(auto_id="ImportConfigButton", control_type="Button").invoke()
    import_dialog = wait_dialog(IMPORT_DIALOG_TITLE)
    _answer_file_dialog(import_dialog, export_path)
    answer_messagebox(win.process_id(), button_id="1")

    # 导入成功后各页重挂：切回触发页读阈值，应回到导出时的 25
    goto(win, 0)
    wait_for_label_value(win, "ThresholdValueLabel", 25.0)
    read_config(
        local_app_data,
        predicate=lambda c: abs(c.get("DragThreshold", 0) - 25) < 0.01,
        message="导入应把 DragThreshold 回退到导出时的 25",
    )
