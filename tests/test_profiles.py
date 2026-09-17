"""配置方案 CRUD 与程序选择器确认路径 e2e。

覆盖真实的模态对话框链路（xUnit 只锁 VM 编排）：输入框（新建/重命名）、确认框（删除）、
程序选择器（选中条目 → 确认 → 写回参数/新增方案 → 落盘）。
"""

from conftest import (
    PROBE_PROGRAM_NAME,
    click_and_answer,
    click_and_confirm_yes,
    goto,
    list_item_texts,
    read_config,
    save_settings,
    select_option,
    text_of,
    wait_dialog,
    wait_dialog_closed,
    wait_until,
)

SLOT_ACTION_TYPE_CATALOG = ("Hotkey", "Launch", "Folder", "System")  # SlotViewModel.ActionTypes

CUSTOM_PROFILE_NAME = "e2e 自定义方案"
RENAMED_PROFILE_NAME = "e2e-renamed.exe"
PICKER_TITLE = "选择程序 - StarPie"


def _profiles_list(win):
    return win.child_window(auto_id="ProfilesListBox", control_type="List")


def _profile_names(win) -> list:
    return list_item_texts(_profiles_list(win))


def _select_profile(win, name: str) -> None:
    """在方案列表里按展示名选中一行（列表已选中态是后续按钮可用的前提）。"""
    list_box = _profiles_list(win)
    for item in list_box.children(control_type="ListItem"):
        if item.window_text() == name:
            item.select()
            return
    raise AssertionError(f"方案列表中没有 {name!r}：{_profile_names(win)}")


def _fill_input_dialog(title: str, text: str) -> None:
    """填写输入框对话框并确认（确认后窗口关闭；校验拒绝时窗口留在原地，用例会显式失败）。"""
    dialog = wait_dialog(title)
    edit = dialog.child_window(auto_id="InputTextBox", control_type="Edit")
    assert edit.exists(timeout=5), f"输入框对话框缺少 InputTextBox：{title}"
    edit.set_edit_text(text)
    dialog.child_window(auto_id="OkButton", control_type="Button").invoke()
    wait_dialog_closed(dialog)


def _confirm_picker_selection(picker, filter_text: str) -> None:
    """在程序选择器里过滤、选中目标条目并确认（确认后对话框关闭）。"""
    search = picker.child_window(auto_id="SearchTextBox", control_type="Edit")
    assert search.exists(timeout=5), "程序选择器搜索框必须存在"
    search.set_edit_text(filter_text)

    programs = picker.child_window(auto_id="ProgramsListView", control_type="List")
    wait_until(
        lambda: any(filter_text in text for text in list_item_texts(programs)),
        timeout=30.0,
        description=f"程序选择器列出 {filter_text}",
    )
    for item in programs.children(control_type="ListItem"):
        if filter_text in item.window_text():
            item.select()
            break
    else:
        raise AssertionError(f"程序选择器没有可选的 {filter_text} 条目")

    picker.child_window(auto_id="OkButton", control_type="Button").invoke()
    wait_dialog_closed(picker)


def test_add_custom_profile_via_input_dialog(app):
    """新建自定义方案：输入框确认后列表出现新条目且落盘。"""
    win, local_app_data = app
    goto(win, 2)

    before = _profile_names(win)
    win.child_window(auto_id="AddCustomProfileButton", control_type="Button").invoke()
    _fill_input_dialog("新建自定义配置", CUSTOM_PROFILE_NAME)

    wait_until(
        lambda: CUSTOM_PROFILE_NAME in _profile_names(win),
        timeout=5.0,
        description=f"方案列表出现 {CUSTOM_PROFILE_NAME}",
    )
    config = read_config(
        local_app_data,
        predicate=lambda c: any(
            p.get("ProcessName") == CUSTOM_PROFILE_NAME for p in c.get("Profiles", [])
        ),
    )
    names = [p.get("ProcessName") for p in config["Profiles"]]
    assert CUSTOM_PROFILE_NAME in names, f"新建方案必须落盘: {before} -> {names}"


def test_rename_profile_via_input_dialog(app):
    """重命名方案：输入框确认后列表与配置同步换名。"""
    win, local_app_data = app
    goto(win, 2)

    _select_profile(win, "chrome.exe")
    win.child_window(auto_id="RenameProfileButton", control_type="Button").invoke()
    _fill_input_dialog("重命名配置方案", RENAMED_PROFILE_NAME)

    wait_until(
        lambda: RENAMED_PROFILE_NAME in _profile_names(win),
        timeout=5.0,
        description=f"方案列表出现 {RENAMED_PROFILE_NAME}",
    )
    config = read_config(
        local_app_data,
        predicate=lambda c: any(
            p.get("ProcessName") == RENAMED_PROFILE_NAME for p in c.get("Profiles", [])
        ),
    )
    names = [p.get("ProcessName") for p in config["Profiles"]]
    assert RENAMED_PROFILE_NAME in names, f"重命名必须落盘: {names}"
    assert "chrome.exe" not in names, f"旧名不得残留: {names}"


def test_delete_profile_confirm_and_global_protection(app):
    """删除确认真实应答；Global 删除被提示框拦下且方案保留。"""
    win, local_app_data = app
    goto(win, 2)

    # 删除非全局方案：确认框按「是」
    _select_profile(win, "code.exe")
    click_and_confirm_yes(win, "DeleteProfileButton")
    wait_until(
        lambda: "code.exe" not in _profile_names(win),
        timeout=5.0,
        description="code.exe 方案从列表移除",
    )
    config = read_config(
        local_app_data,
        predicate=lambda c: all(
            p.get("ProcessName") != "code.exe" for p in c.get("Profiles", [])
        ),
    )
    assert all(p.get("ProcessName") != "code.exe" for p in config["Profiles"]), "删除必须落盘"

    # Global 保护：提示框（确定）拦下删除，方案仍在
    _select_profile(win, "Global")
    click_and_answer(win, "DeleteProfileButton", button_id="1")
    assert "Global" in _profile_names(win), "Global 方案不得被删除"


def test_add_profile_from_picker_creates_profile_for_program(app, probe_program):
    """选择程序新建方案：新方案以程序进程名为名，且写入配置。"""
    win, local_app_data = app
    goto(win, 2)

    win.child_window(auto_id="AddProfileButton", control_type="Button").invoke()
    picker = wait_dialog(PICKER_TITLE)
    _confirm_picker_selection(picker, PROBE_PROGRAM_NAME)

    expected = f"{PROBE_PROGRAM_NAME}.exe"
    wait_until(
        lambda: any(expected in name for name in _profile_names(win)),
        timeout=5.0,
        description=f"方案列表出现 {expected}",
    )
    read_config(
        local_app_data,
        predicate=lambda c: any(
            p.get("ProcessName", "").lower() == expected for p in c.get("Profiles", [])
        ),
    )


def test_program_picker_confirm_writes_launch_action(app, probe_program):
    """槽位动作经程序选择器写回：参数落盘为所选程序路径，名称回填。"""
    win, local_app_data = app
    goto(win, 2)

    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    assert type_combo.exists(timeout=3), "Slot0ActionTypeComboBox 必须存在"
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "Launch", verify_selection=False)

    win.child_window(auto_id="Slot0BrowseProgramButton", control_type="Button").invoke()
    picker = wait_dialog(PICKER_TITLE)
    _confirm_picker_selection(picker, PROBE_PROGRAM_NAME)

    path_box = win.child_window(auto_id="Slot0ProgramPathTextBox", control_type="Edit")
    assert path_box.exists(timeout=5), "选择程序后槽位程序路径框必须存在（Launch 参数行可见）"
    wait_until(
        lambda: PROBE_PROGRAM_NAME in path_box.window_text(),
        timeout=5.0,
        description="槽位参数回填所选程序路径",
    )
    assert text_of(win, "Slot0NameTextBox", "Edit", timeout=3).strip(), "动作名称必须回填"

    save_settings(win)
    config = read_config(
        local_app_data,
        predicate=lambda c: any(
            p.get("ProcessName") == "Global"
            and (p.get("Actions") or [{}])[0].get("Type") == "Launch"
            and PROBE_PROGRAM_NAME in ((p.get("Actions") or [{}])[0].get("Parameter") or "")
            for p in c.get("Profiles", [])
        ),
    )
    glob = next(p for p in config["Profiles"] if p.get("ProcessName") == "Global")
    assert PROBE_PROGRAM_NAME in glob["Actions"][0]["Parameter"], (
        f"Launch 参数应落盘为探针路径: {glob['Actions'][0]}"
    )
