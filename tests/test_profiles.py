"""配置方案 CRUD 与程序选择器确认路径 e2e。

覆盖真实的模态对话框链路（xUnit 只锁 VM 编排）：输入框（新建/重命名）、确认框（删除）、
程序选择器（选中条目 → 确认 → 写回参数/新增方案）。

断言分层：方案列表的增删改以"列表即时变化"为主（live-apply，运行态即观察面）；
"新方案/槽位参数写入配置"这类没有 UI 观察面的链路由落盘断言覆盖（各一处，
不点 Save——防抖自动落盘 + 轮询等待，省掉模态框往返）。
"""

from catalogs import PROGRAM_PICKER_TITLE, SLOT_ACTION_TYPE_CATALOG
from conftest import (
    PROBE_PROGRAM_NAME,
    click_and_answer,
    click_and_confirm_yes,
    goto,
    list_item_texts,
    pick_program,
    read_config,
    save_settings,
    select_option,
    text_of,
    wait_dialog,
    wait_dialog_closed,
    wait_until,
)

CUSTOM_PROFILE_NAME = "e2e 自定义方案"
RENAMED_PROFILE_NAME = "e2e-renamed.exe"


def _profiles_list(win):
    return win.child_window(auto_id="ProfilesListBox", control_type="List")


def _profile_names(win) -> list:
    return list_item_texts(_profiles_list(win))


def _select_profile(win, name: str) -> None:
    """在方案列表里按展示名选中一行（列表已选中态是后续按钮可用的前提）。"""
    for item in _profiles_list(win).children(control_type="ListItem"):
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


def test_add_custom_profile_via_input_dialog(app):
    """新建自定义方案：输入框确认后列表出现新条目，且新方案写入配置。"""
    win, local_app_data = app
    goto(win, 2)

    win.child_window(auto_id="AddCustomProfileButton", control_type="Button").invoke()
    _fill_input_dialog("新建自定义配置", CUSTOM_PROFILE_NAME)

    wait_until(
        lambda: CUSTOM_PROFILE_NAME in _profile_names(win),
        timeout=5.0,
        description=f"方案列表出现 {CUSTOM_PROFILE_NAME}",
    )
    read_config(
        local_app_data,
        predicate=lambda c: any(p.get("ProcessName") == CUSTOM_PROFILE_NAME for p in c.get("Profiles", [])),
        message=f"新建方案 {CUSTOM_PROFILE_NAME} 应写入配置",
    )


def test_rename_profile_via_input_dialog(app):
    """重命名方案：输入框确认后列表即时换名，配置里旧名不残留。"""
    win, local_app_data = app
    goto(win, 2)

    _select_profile(win, "chrome.exe")
    win.child_window(auto_id="RenameProfileButton", control_type="Button").invoke()
    _fill_input_dialog("重命名配置方案", RENAMED_PROFILE_NAME)

    wait_until(
        lambda: RENAMED_PROFILE_NAME in _profile_names(win) and "chrome.exe" not in _profile_names(win),
        timeout=5.0,
        description=f"方案列表换名为 {RENAMED_PROFILE_NAME}（旧名离场）",
    )
    read_config(
        local_app_data,
        predicate=lambda c: any(p.get("ProcessName") == RENAMED_PROFILE_NAME for p in c.get("Profiles", []))
        and all(p.get("ProcessName") != "chrome.exe" for p in c.get("Profiles", [])),
        message="重命名应写入配置且旧名不残留",
    )


def test_delete_profile_confirm_and_global_protection(app):
    """删除确认真实应答；Global 删除被提示框拦下且方案保留（列表即观察面）。"""
    win, _ = app
    goto(win, 2)

    # 删除非全局方案：确认框按「是」
    _select_profile(win, "code.exe")
    click_and_confirm_yes(win, "DeleteProfileButton")
    wait_until(
        lambda: "code.exe" not in _profile_names(win),
        timeout=5.0,
        description="code.exe 方案从列表移除",
    )

    # Global 保护：提示框（确定）拦下删除，方案仍在
    _select_profile(win, "Global")
    click_and_answer(win, "DeleteProfileButton", button_id="1")
    assert "Global" in _profile_names(win), "Global 方案不得被删除"


def test_add_profile_from_picker_creates_profile_for_program(app, probe_program):
    """选择程序新建方案：列表即时出现以程序进程名为名的新方案。"""
    win, _ = app
    goto(win, 2)

    win.child_window(auto_id="AddProfileButton", control_type="Button").invoke()
    picker = wait_dialog(PROGRAM_PICKER_TITLE)
    pick_program(picker, PROBE_PROGRAM_NAME)

    expected = f"{PROBE_PROGRAM_NAME}.exe"
    wait_until(
        lambda: any(expected in name for name in _profile_names(win)),
        timeout=5.0,
        description=f"方案列表出现 {expected}",
    )


def test_program_picker_confirm_writes_launch_action(app, probe_program):
    """槽位动作经程序选择器写回：路径/名称即时回填，参数与类型写入配置。

    Slot0 动作类型下拉的 UIA 不暴露选中态，参数是否真的写回配置只能读盘——保留此一处落盘断言。
    """
    win, local_app_data = app
    goto(win, 2)

    type_combo = win.child_window(auto_id="Slot0ActionTypeComboBox", control_type="ComboBox")
    select_option(type_combo, SLOT_ACTION_TYPE_CATALOG, "Launch", verify_selection=False)

    win.child_window(auto_id="Slot0BrowseProgramButton", control_type="Button").invoke()
    picker = wait_dialog(PROGRAM_PICKER_TITLE)
    pick_program(picker, PROBE_PROGRAM_NAME)

    path_box = win.child_window(auto_id="Slot0ProgramPathTextBox", control_type="Edit")
    assert path_box.exists(timeout=5), "选择程序后槽位程序路径框必须存在（Launch 参数行可见）"
    wait_until(
        lambda: PROBE_PROGRAM_NAME in path_box.window_text(),
        timeout=5.0,
        description="槽位参数回填所选程序路径",
    )
    assert text_of(win, "Slot0NameTextBox", "Edit", timeout=3).strip(), "动作名称必须回填"

    # 槽位参数的写回不触发防抖落盘（实测 5s 内不落盘）：这里必须显式 Save 才能读盘
    save_settings(win)
    read_config(
        local_app_data,
        predicate=lambda c: any(
            p.get("ProcessName") == "Global"
            and (p.get("Actions") or [{}])[0].get("Type") == "Launch"
            and PROBE_PROGRAM_NAME in ((p.get("Actions") or [{}])[0].get("Parameter") or "")
            for p in c.get("Profiles", [])
        ),
        message="选程序后槽位动作应写入配置（Type=Launch、Parameter=探针路径）",
    )
