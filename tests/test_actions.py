"""槽位动作编辑与试运行 e2e：热键录制、参数编辑、图标选取，以及"试运行"真的执行动作。

动作执行器（ActionExecutorService）在真实进程里的执行面只能在这里验：
试运行按钮走的是与手势松开时同一条 `Execute` 路径，落地证据是探针 exe 的进程出现。
"""

import pytest
from conftest import (
    find_process_by_executable,
    goto,
    kill_processes,
    read_config,
    save_settings,
    select_option,
    wait_dialog,
    wait_dialog_closed,
    wait_process_started,
    wait_until,
)

SLOT_ACTION_TYPE_CATALOG = ("Hotkey", "Launch", "Folder", "System")  # SlotViewModel.ActionTypes
ICON_PICKER_TITLE = "选择动作矢量图标 - StarPie"


def _global_first_action(config) -> dict:
    glob = next((p for p in config.get("Profiles", []) if p.get("ProcessName") == "Global"), None)
    assert glob is not None, f"配置里必须有 Global 方案: {[p.get('ProcessName') for p in config.get('Profiles', [])]}"
    actions = glob.get("Actions") or []
    assert actions, "Global 方案必须有动作"
    return actions[0]


def test_hotkey_recorder_records_and_persists(app):
    """热键录制框：真实敲 Ctrl+A → 控件回读 → 保存后落盘。"""
    win, local_app_data = app
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

    save_settings(win)
    config = read_config(
        local_app_data,
        predicate=lambda c: _global_first_action(c).get("Parameter", "").replace(" ", "") == "Ctrl+A",
    )
    action = _global_first_action(config)
    assert action.get("Type") == "Hotkey", f"槽位动作类型应为 Hotkey: {action}"
    assert action.get("Parameter", "").replace(" ", "") == "Ctrl+A", f"热键应落盘: {action}"


def test_folder_action_parameter_persists(app, tmp_path):
    """动作类型切 Folder 后直接编辑路径：保存后类型与路径一起落盘。"""
    win, local_app_data = app
    goto(win, 2)

    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    assert type_combo.exists(timeout=3), "Slot0ActionTypeComboBox 必须存在"
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "Folder", verify_selection=False)

    folder_box = win.child_window(auto_id="Slot0FolderPathTextBox", control_type="Edit")
    assert folder_box.exists(timeout=3), "切到 Folder 后应出现文件夹路径编辑框"
    target = str(tmp_path)
    folder_box.set_edit_text(target)

    save_settings(win)
    config = read_config(
        local_app_data,
        predicate=lambda c: _global_first_action(c).get("Type") == "Folder"
        and _global_first_action(c).get("Parameter") == target,
    )
    action = _global_first_action(config)
    assert action.get("Type") == "Folder" and action.get("Parameter") == target, f"落盘不符: {action}"


def test_slot_icon_picker_writes_icon_key(app):
    """槽位图标选择器：过滤选中 → 确认 → 保存后图标键落盘。"""
    win, local_app_data = app
    goto(win, 2)

    win.child_window(auto_id="Slot0IconButton", control_type="Button").invoke()
    picker = wait_dialog(ICON_PICKER_TITLE)
    search = picker.child_window(auto_id="SearchTextBox", control_type="Edit")
    assert search.exists(timeout=5), "图标选择器搜索框必须存在"
    search.set_edit_text("Lock")

    card = picker.child_window(title="Lock", control_type="Text", found_index=0)
    wait_until(lambda: card.exists(timeout=0.3), timeout=5.0, description="过滤后出现 Lock 图标卡片")
    card.click_input()
    picker.child_window(auto_id="ConfirmButton", control_type="Button").invoke()
    wait_dialog_closed(picker)

    save_settings(win)
    config = read_config(local_app_data, predicate=lambda c: _global_first_action(c).get("IconKey") == "Lock")
    assert _global_first_action(config).get("IconKey") == "Lock", "图标键应落盘为 Lock"


@pytest.mark.parametrize("sandbox_seed", ["gesture-probe"], indirect=True)
def test_slot_test_button_executes_action(app):
    """「试运行」按钮：真实执行槽位动作（预置的 Launch 探针），进程出现即证据。"""
    win, local_app_data = app
    goto(win, 2)

    probe_exe = _global_first_action(read_config(local_app_data)).get("Parameter")
    assert probe_exe, "预置配置里槽位 0 必须是探针 Launch 动作"
    assert find_process_by_executable(probe_exe) == [], "试运行前探针不得已在运行"

    win.child_window(auto_id="Slot0TestButton", control_type="Button").invoke()
    pids = wait_process_started(probe_exe, timeout=10.0)
    print(f"试运行已执行动作：探针进程 {pids}（exe={probe_exe}）")
    kill_processes(pids)
