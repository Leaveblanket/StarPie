"""槽位动作编辑与试运行 e2e：热键录制、参数编辑、图标选取，以及"试运行"真的执行动作。

动作执行器（ActionExecutorService）在真实进程里的执行面只能在这里验：
试运行按钮走的是与手势松开时同一条 `Execute` 路径，落地证据是探针 exe 的进程出现。

断言分层：录制框/编辑框这类可回读的写回走"控件回读 + 切页往返保持"（View 重建后仍成立，
是运行态已接受的强证据）；图标键没有 UI 观察面，保留落盘断言；动作执行以进程证据为准。
"""

import pytest
from catalogs import ICON_PICKER_TITLE, SLOT_ACTION_TYPE_CATALOG
from conftest import (
    find_process_by_executable,
    goto,
    kill_processes,
    probe_exe_from_config,
    read_config,
    save_settings,
    select_option,
    wait_dialog,
    wait_dialog_closed,
    wait_process_started,
    wait_until,
)

ICON_KEY = "Lock"


def test_hotkey_recorder_records(app):
    """热键录制框：真实敲 Ctrl+A → 控件即时回读，切页往返后保持（运行态已接受）。"""
    win, _ = app
    goto(win, 2)

    recorder = win.child_window(auto_id="Slot0HotkeyRecorder", control_type="Edit")
    assert recorder.exists(timeout=5), "热键录制框必须可寻址（自定义控件需自动化对等体）"

    recorder.click_input()  # 聚焦即进入录制态
    recorder.type_keys("^a")
    wait_until(
        lambda: recorder.window_text().replace(" ", "") == "Ctrl+A",
        timeout=5.0,
        description="录制到 Ctrl + A",
    )

    goto(win, 1)
    goto(win, 2)
    recorder = win.child_window(auto_id="Slot0HotkeyRecorder", control_type="Edit")
    wait_until(
        lambda: recorder.window_text().replace(" ", "") == "Ctrl+A",
        timeout=5.0,
        description="切页往返后录制值保持（运行态已接受）",
    )


def test_folder_action_parameter_edits(app, tmp_path):
    """动作类型切 Folder 后出现路径编辑行，编辑值切页往返后保持（运行态已接受）。"""
    win, _ = app
    goto(win, 2)

    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "Folder", verify_selection=False)

    folder_box = win.child_window(auto_id="Slot0FolderPathTextBox", control_type="Edit")
    assert folder_box.exists(timeout=3), "切到 Folder 后应出现文件夹路径编辑框"
    target = str(tmp_path)
    folder_box.set_edit_text(target)

    goto(win, 1)
    goto(win, 2)
    folder_box = win.child_window(auto_id="Slot0FolderPathTextBox", control_type="Edit")
    wait_until(
        lambda: folder_box.exists(timeout=0.3) and folder_box.window_text() == target,
        timeout=5.0,
        description="切页往返后 Folder 路径保持（运行态已接受）",
    )


def test_slot_icon_picker_writes_icon_key(app):
    """槽位图标选择器：过滤选中 → 确认 → 图标键写入配置（图标键无 UI 观察面）。"""
    win, local_app_data = app
    goto(win, 2)

    win.child_window(auto_id="Slot0IconButton", control_type="Button").invoke()
    picker = wait_dialog(ICON_PICKER_TITLE)
    search = picker.child_window(auto_id="SearchTextBox", control_type="Edit")
    assert search.exists(timeout=5), "图标选择器搜索框必须存在"
    search.set_edit_text(ICON_KEY)

    card = picker.child_window(title=ICON_KEY, control_type="Text", found_index=0)
    wait_until(lambda: card.exists(timeout=0.3), timeout=5.0, description=f"过滤后出现 {ICON_KEY} 图标卡片")
    card.click_input()
    picker.child_window(auto_id="ConfirmButton", control_type="Button").invoke()
    wait_dialog_closed(picker)

    # 槽位图标键的写回不触发防抖落盘（实测 5s 内不落盘）：这里必须显式 Save 才能读盘
    save_settings(win)
    read_config(
        local_app_data,
        predicate=lambda c: any(
            p.get("ProcessName") == "Global"
            and ((p.get("Actions") or [{}])[0].get("IconKey") or "") == ICON_KEY
            for p in c.get("Profiles", [])
        ),
        message=f"图标键应落盘为 {ICON_KEY}（图标键无 UI 观察面）",
    )


@pytest.mark.parametrize("sandbox_seed", ["gesture-probe"], indirect=True)
def test_slot_test_button_executes_action(app):
    """「试运行」按钮：真实执行槽位动作（预置的 Launch 探针），进程出现即证据。"""
    win, local_app_data = app
    goto(win, 2)

    probe_exe = probe_exe_from_config(local_app_data)
    assert probe_exe, "预置配置里槽位 0 必须是探针 Launch 动作"
    assert find_process_by_executable(probe_exe) == [], "试运行前探针不得已在运行"

    win.child_window(auto_id="Slot0TestButton", control_type="Button").invoke()
    pids = wait_process_started(probe_exe, timeout=10.0)
    print(f"试运行已执行动作：探针进程 {pids}（exe={probe_exe}）")
    kill_processes(pids)
