"""托盘交互 e2e：右键菜单（暂停/恢复、导航项、退出）与双击直达。

系统托盘图标不是 UIA 元素，菜单也不在设置台窗口树里；这里按产品发布的定位面驱动——
向常驻托盘消息窗口投递回调消息（等价于真实右键/双击），菜单按标题 `StarPieTrayMenu`
定位，条目按稳定 AutomationId 定位。菜单行是 Border+TextBlock 组合（无 Invoke 模式），
故用真实鼠标点击（click_input）。

运行期间请勿操作键鼠（全局钩子在跑）。
"""

import win32gui
import win32process
from conftest import (
    PAGE_ANCHORS,
    TRAY_CALLBACK_MESSAGE,
    TRAY_MENU_WINDOW_TITLE,
    TRAY_WINDOW_TITLE,
    WM_LBUTTONDBLCLK,
    WM_RBUTTONUP,
    close_console,
    find_tray_window,
    wait_dialog,
    wait_dialog_closed,
    wait_until,
)
from tray_area import wait_icon

# 菜单条目 id 与产品发布面一致（StarPie.Ui/ShellHost.cs）
PAUSE_ITEM = "TrayMenuPause"
NAV_ITEMS = {
    "TrayMenuPreferences": 0,  # 触发与场景
    "TrayMenuAppearance": 1,  # 外观与形态
    "TrayMenuWheelActions": 2,  # 轮盘与动作
}
EXIT_ITEM = "TrayMenuExit"


def _post_tray_command(pid: int, lparam: int) -> None:
    """向常驻托盘消息窗口投递托盘回调消息（等价于在图标上按/双击鼠标）。

    只吃 pid：设置台关闭即销毁，绑定旧窗口的 WindowSpecification 再取 pid 会抛 COMError，
    而托盘消息窗口是常驻 HWND，按 pid 定位不受设置台开关影响。
    """
    tray = find_tray_window(pid)
    assert tray, f"常驻托盘消息窗口必须存在（标题 {TRAY_WINDOW_TITLE}）"
    win32gui.PostMessage(tray, TRAY_CALLBACK_MESSAGE, 0, lparam)


def open_tray_menu(pid: int):
    """打开托盘右键菜单并返回菜单窗口（按标题定位；菜单每次新建窗口）。"""
    _post_tray_command(pid, WM_RBUTTONUP)
    return wait_dialog(TRAY_MENU_WINDOW_TITLE)


def menu_item(menu, auto_id: str):
    item = menu.child_window(auto_id=auto_id, control_type="Text")
    assert item.exists(timeout=5.0), f"托盘菜单缺少条目 {auto_id}"
    return item


def click_menu_item(menu, auto_id: str) -> None:
    """点击菜单条目：菜单行非 Control（无 Invoke 模式），走真实鼠标点击。"""
    menu_item(menu, auto_id).click_input()
    wait_dialog_closed(menu)


def test_tray_menu_items_and_pause_roundtrip(app):
    """菜单列出内置条目；暂停 ⇄ 恢复按当前态刷新标签（暂停是运行态，无落盘）。"""
    win, _ = app
    pid = win.process_id()

    menu = open_tray_menu(pid)
    pause = menu_item(menu, PAUSE_ITEM)
    assert "暂停" in pause.window_text(), f"初始应为「暂停」: {pause.window_text()!r}"

    click_menu_item(menu, PAUSE_ITEM)

    menu = open_tray_menu(pid)
    assert "恢复" in menu_item(menu, PAUSE_ITEM).window_text(), "暂停后菜单应显示「恢复」"

    click_menu_item(menu, PAUSE_ITEM)

    menu = open_tray_menu(pid)
    assert "暂停" in menu_item(menu, PAUSE_ITEM).window_text(), "恢复后菜单应回到「暂停」"
    # 收摊：关掉菜单（失焦也会自动关，这里显式关以便断言干净）
    menu.type_keys("{ESC}")
    wait_dialog_closed(menu)


def test_tray_menu_navigates_to_pages(app):
    """菜单导航项直达对应页面（设置台显示并激活 + 目标页锚点就位）。"""
    win, _ = app
    pid = win.process_id()

    for auto_id, slot in NAV_ITEMS.items():
        menu = open_tray_menu(pid)
        click_menu_item(menu, auto_id)

        anchor_id, anchor_type = PAGE_ANCHORS[slot]
        wait_until(
            lambda: win.child_window(auto_id=anchor_id, control_type=anchor_type).exists(timeout=0.3),
            timeout=5.0,
            description=f"{auto_id} 导航到 NavPage{slot}",
        )
        radio = win.child_window(auto_id=f"NavPage{slot}", control_type="RadioButton")
        assert radio.is_selected(), f"{auto_id} 后 NavPage{slot} 应处于选中态"


def test_tray_double_click_reopens_console(app):
    """双击托盘图标重建设置台并停在触发页（关闭即销毁 → 双击直达重开）。"""
    win, _ = app
    pid = win.process_id()

    handle = win.handle
    close_console(win)

    _post_tray_command(pid, WM_LBUTTONDBLCLK)

    from conftest import Desktop, find_main_window

    built = []

    def _rebuilt():
        try:
            candidate = find_main_window(pid, timeout=0.5)
        except AssertionError:
            return False
        if candidate != handle:
            built.append(candidate)
            return True
        return False

    wait_until(_rebuilt, timeout=10.0, description="双击后设置台重建（新窗口句柄）")
    win2 = Desktop(backend="uia").window(handle=built[-1])
    win2.wait("visible", timeout=5.0)

    radio0 = win2.child_window(auto_id="NavPage0", control_type="RadioButton")
    wait_until(radio0.is_selected, timeout=5.0, description="双击直达触发页")
    assert win2.child_window(
        auto_id="EnableOuterEscapeCheckBox", control_type="CheckBox"
    ).exists(timeout=5.0), "双击直达后触发页锚点必须就位"


def test_tray_menu_exit_terminates_without_residue(app):
    """菜单「退出」走真实退出编排：进程退出且通知区不留幽灵托盘图标。"""
    win, _ = app
    pid = win.process_id()

    tray_hwnd = find_tray_window(pid)
    assert tray_hwnd, f"常驻托盘消息窗口必须存在（标题 {TRAY_WINDOW_TITLE}）"

    # 观察图标是否真的落进通知区（explorer 偶发不落条目，此时残留判定天然成立）
    try:
        wait_icon(tray_hwnd, present=True, timeout=5.0)
        registered = True
    except AssertionError:
        registered = False
    print(f"托盘图标登记观察：{'已登记' if registered else '未登记（explorer 未落进按钮表）'}")

    menu = open_tray_menu(pid)
    click_menu_item(menu, EXIT_ITEM)

    wait_until(
        lambda: pid not in win32process.EnumProcesses(),
        timeout=15.0,
        description="菜单退出后进程结束（真实退出编排）",
    )

    wait_icon(tray_hwnd, present=False, timeout=5.0)
