"""轮盘手势链路 e2e：注射鼠标输入 → 全局钩子 → 轮盘手势引擎 → 轮盘窗口 → 动作执行。

这是 xUnit 覆盖不到的集成面：轮盘窗口真的弹出/收起、扇区动作真的被执行
（Launch 探针 exe 的进程出现）、阈值下的触发键补发真的落到光标下的窗口。
产品钩子不做 LLMHF_INJECTED 过滤，注入事件与物理操作走同一条回调路径
（注入层见 tests/mouse_input.py），因此"没有产品内测试通道"不妨碍真实链路被驱动。

配置由 sandbox_seed="wheel-probe" 预置：Global 4 扇区、DragThreshold=25、
OuterEscapeDistance=186，扇区 0（正右）是探针 exe 的 Launch 动作，其余扇区空动作。

触发键默认右键；**侧键**用例经 `trigger_button` fixture 传 4（后侧键 XBUTTON1，
对应 SharpHook Button4）。侧键未被抑制时不弹上下文菜单——首次轮盘手势/启动时序一类用例
优先用它，观测不必先收菜单（见下方 side-button 组）。

运行期间请勿操作键鼠（全局钩子在跑，注入输入与真实鼠标共用同一个游标）。
"""

import time
import warnings

import pytest
import win32api
import win32con
import win32gui
from conftest import (
    find_process_by_executable,
    find_wheel_window,
    kill_processes,
    probe_exe_from_config,
    wait_process_started,
    wait_until,
)
from mouse_input import (
    XBUTTON1,
    drag_circle,
    drag_right,
    left_click_at,
    move_by,
    move_to,
    press_right_at,
    press_side_at,
    release_right,
    release_side,
)
from win32_probe import WM_XBUTTON_UP, RightClickProbeWindow

# 轮盘手势起点：默认设置台窗口覆盖区内的固定点（远离任务栏/托盘，避免注入点击命中系统 UI）。
START = (600, 400)

# 越过 DragThreshold(25)、远小于 OuterEscapeDistance(186) 的拖动距离 → 命中正右扇区 0。
SECTOR_DRAG = 110
# 越过 OuterEscapeDistance(186) 的外甩距离 → 触发外圈逃逸取消。
ESCAPE_DRAG = 240
# 中心死区（阈值 25 × 0.6 = 15px）以内即取消选中；这里先外拖再拖回起点。
CENTER_RETURN_DRAG = 110

# 密集拖动（≈1000Hz 鼠标节奏）的注入规模：足以让"逐事件全量重绘高亮"的实现
# 在 UI 线程上积压出数百毫秒的待办工作。
DENSE_SWEEP_STEPS = 600
DENSE_SWEEP_INTERVAL = 0.001
DENSE_SWEEP_RADIUS = 110


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
def test_wheel_below_threshold_replays_right_click(app):
    """阈值内的按下/松开不弹轮盘：补发的右键点击落在光标下的窗口上。"""
    win, _ = app
    pid = win.process_id()

    with RightClickProbeWindow() as probe:
        move_to(*probe.center)
        time.sleep(0.2)
        press_right_at(*probe.center)
        move_by(3, 0)  # 微小移动：远在 DragThreshold(25) 之内
        time.sleep(0.05)
        release_right()

        assert probe.wait_right_button_up(3.0), (
            "阈值内的松手必须补发一次右键点击（光标下的探针窗口应收到 WM_RBUTTONUP）"
        )
        assert probe.right_events, f"探针窗口未收到任何右键消息: {probe.right_events}"
        assert find_wheel_window(pid) == 0, "阈值内的拖动不得弹出轮盘"


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
def test_wheel_drag_pops_wheel_and_executes_sector_action(app):
    """过阈拖动弹出轮盘、松手收起，并执行正右扇区 0 绑定的 Launch 动作。"""
    win, local_app_data = app
    pid = win.process_id()
    probe_exe = probe_exe_from_config(local_app_data)

    press_right_at(*START)
    drag_right(20, steps=2)  # 未越阈值：此时轮盘不该出现

    # 观察面：热路径时延 = 越过阈值的那次移动 → 窗口出现（不含拖动注入本身）
    threshold_cross = time.time()
    move_by(45, 0)  # 单次移动越过 DragThreshold(25)
    hwnd = wait_until(
        lambda: find_wheel_window(pid),
        timeout=3.0,
        interval=0.02,
        description="越过阈值后轮盘窗口弹出",
    )
    popup_ms = (time.time() - threshold_cross) * 1000
    if popup_ms > 300:
        warnings.warn(f"轮盘弹出时延偏高：{popup_ms:.0f}ms（越阈值→窗口出现）", stacklevel=2)
    else:
        warnings.warn(f"轮盘弹出时延 {popup_ms:.0f}ms（越阈值→窗口出现，观测值）", stacklevel=2)
    assert hwnd, "轮盘窗口句柄必须有效"
    # 热路径应该"按下即出"：含调度开销仍应在 2s 内（预热失效/首帧卡死会被这条抓住）
    assert popup_ms < 2000, f"轮盘弹出时延过长（{popup_ms:.0f}ms），检查预热与首帧路径"

    drag_right(45, steps=2)  # 继续右拖，最终停在正右扇区 0
    release_right()
    wait_until(
        lambda: find_wheel_window(pid) == 0,
        timeout=3.0,
        description="松手后轮盘窗口收起",
    )

    pids = wait_process_started(probe_exe, timeout=10.0)
    print(f"扇区动作已执行：探针进程 {pids}（exe={probe_exe}）")
    kill_processes(pids)


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
def test_wheel_dense_drag_keeps_close_prompt(app):
    """密集拖动后松手：轮盘收起时延不随拖动事件数增长（拖动工作不在 UI 线程积压）。

    观察面取「松手 → 轮盘窗口销毁」：轮盘收起排在已排队的拖动工作项之后，因此这条
    时延即 UI 线程上的积压深度。高亮更新若逐事件全量重绘，数百次密集拖动会积压出
    数百毫秒、收起明显滞后；拖动工作合并为「最新态覆盖」后队列里至多一个待应用项，
    收起时延回落到与拖动事件数无关的基线。
    """
    win, _ = app
    pid = win.process_id()

    press_right_at(*START)
    move_by(45, 0)  # 单次移动越过 DragThreshold(25)，弹出轮盘
    wait_until(
        lambda: find_wheel_window(pid),
        timeout=3.0,
        interval=0.02,
        description="越过阈值后轮盘窗口弹出",
    )

    drag_circle(START, DENSE_SWEEP_RADIUS, DENSE_SWEEP_STEPS, DENSE_SWEEP_INTERVAL)

    release_t0 = time.perf_counter()
    release_right(settle=0.0)
    wait_until(
        lambda: find_wheel_window(pid) == 0,
        timeout=3.0,
        interval=0.005,
        description="松手后轮盘窗口收起",
    )
    close_ms = (time.perf_counter() - release_t0) * 1000

    if close_ms > 60:
        warnings.warn(f"密集拖动后轮盘收起偏慢：{close_ms:.0f}ms（松手→窗口销毁）", stacklevel=2)
    else:
        warnings.warn(f"密集拖动后轮盘收起 {close_ms:.0f}ms（松手→窗口销毁，观测值）", stacklevel=2)
    assert close_ms < 400, (
        f"密集拖动后轮盘收起过长（{close_ms:.0f}ms）：拖动事件的工作项在 UI 线程上积压，"
        "检查高亮更新是否仍在逐事件全量重绘"
    )


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
def test_wheel_outer_escape_cancels_without_executing(app):
    """外甩越过逃逸距离：松手按取消处理，不执行任何动作。"""
    win, local_app_data = app
    pid = win.process_id()
    probe_exe = probe_exe_from_config(local_app_data)

    press_right_at(*START)
    drag_right(ESCAPE_DRAG)
    wait_until(
        lambda: find_wheel_window(pid),
        timeout=3.0,
        description="拖动过阈后轮盘窗口弹出",
    )

    release_right()
    wait_until(
        lambda: find_wheel_window(pid) == 0,
        timeout=3.0,
        description="松手后轮盘窗口收起",
    )
    time.sleep(0.5)
    assert find_process_by_executable(probe_exe) == [], "外甩取消不得执行扇区动作"


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
def test_wheel_return_to_center_cancels_without_executing(app):
    """弹出后拖回中心死区：取消选中，松手不执行动作。"""
    win, local_app_data = app
    pid = win.process_id()
    probe_exe = probe_exe_from_config(local_app_data)

    press_right_at(*START)
    drag_right(CENTER_RETURN_DRAG)
    wait_until(
        lambda: find_wheel_window(pid),
        timeout=3.0,
        description="拖动过阈后轮盘窗口弹出",
    )

    # 拖回按下点（距离 ~0，落在 0.6 × 阈值 的中心死区内）→ 选中被清空
    move_by(-CENTER_RETURN_DRAG, 0)
    time.sleep(0.15)
    release_right()

    wait_until(
        lambda: find_wheel_window(pid) == 0,
        timeout=3.0,
        description="松手后轮盘窗口收起",
    )
    time.sleep(0.5)
    assert find_process_by_executable(probe_exe) == [], "中心死区取消不得执行扇区动作"


# --- 侧键触发键（触发键在栈内参数化的端到端证明） -------------------------------
# 产品侧触发键可经 --trigger-button=<n> 换（测试实例专有入口，见 TestInstanceSwitches）：
# 4 = 后侧键（SharpHook Button4 ⟷ libuiohook MOUSE_BUTTON4 ⟷ Windows XBUTTON1）。
# 侧键未被抑制时不弹上下文菜单，这两条用例因此不受"死区里漏出的菜单"干扰。


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
@pytest.mark.parametrize("trigger_button", [4], indirect=True)
def test_wheel_side_button_below_threshold_replays_side_click(app):
    """侧键作触发键：阈值内按下/松开不弹轮盘，且补发的是**同一个侧键**的点击。

    换键若只改了捕获侧而没改注入侧，补发会退回右键——探针窗口按侧键消息断言即抓住。
    """
    win, _ = app
    pid = win.process_id()

    with RightClickProbeWindow() as probe:
        move_to(*probe.center)
        time.sleep(0.2)
        press_side_at(*probe.center)
        move_by(3, 0)  # 微小移动：远在 DragThreshold(25) 之内
        time.sleep(0.05)
        release_side()

        assert probe.wait_xbutton_up(XBUTTON1, 3.0), (
            "阈值内的松手必须补发一次侧键点击（光标下的探针窗口应收到 WM_XBUTTONUP/"
            f"XBUTTON1）；实收侧键消息={probe.xbutton_events}、右键消息={probe.right_events}"
        )
        assert (WM_XBUTTON_UP, XBUTTON1) in probe.xbutton_events
        assert find_wheel_window(pid) == 0, "阈值内的拖动不得弹出轮盘"


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
@pytest.mark.parametrize("trigger_button", [4], indirect=True)
def test_wheel_side_button_drag_pops_wheel_and_executes_sector_action(app):
    """侧键作触发键：过阈拖动照常弹出轮盘、松手收起并执行扇区动作。

    证明触发键参数化贯通「捕获 → 抑制 → 引擎 → 轮盘 → 动作」全链路，
    而非只在捕获侧过滤按键。
    """
    win, local_app_data = app
    pid = win.process_id()
    probe_exe = probe_exe_from_config(local_app_data)

    press_side_at(*START)
    drag_right(20, steps=2)  # 未越阈值：此时轮盘不该出现
    assert find_wheel_window(pid) == 0, "未越阈值时不得弹出轮盘"

    move_by(45, 0)  # 单次移动越过 DragThreshold(25)
    hwnd = wait_until(
        lambda: find_wheel_window(pid),
        timeout=3.0,
        interval=0.02,
        description="侧键过阈拖动后轮盘窗口弹出",
    )
    assert hwnd, "轮盘窗口句柄必须有效"

    drag_right(45, steps=2)  # 继续右拖，最终停在正右扇区 0
    release_side()
    wait_until(
        lambda: find_wheel_window(pid) == 0,
        timeout=3.0,
        description="松手后轮盘窗口收起",
    )

    pids = wait_process_started(probe_exe, timeout=10.0)
    print(f"侧键轮盘手势的扇区动作已执行：探针进程 {pids}（exe={probe_exe}）")
    kill_processes(pids)


# --- 桌面窗口场景（全屏误判回归） ----------------------------------------------
# Win11 上桌面图标区（SHELLDLL_DefView）常挂在一个覆盖整屏的 WorkerW 下；前台为桌面时
# IsForegroundFullScreen 的排除清单若只含 Progman（GetShellWindow）/窗口站桌面（GetDesktopWindow），
# 会把桌面窗口误判成"全屏应用"，轮盘手势被 DisableOnFullScreen 隔离、右键直通系统原生。
# 本用例把前台焦点切到桌面后再做轮盘手势：修复前轮盘不弹（红），修复后照常弹出（绿）。

def _is_desktop_host(hwnd) -> bool:
    """窗口是否是桌面宿主：Progman（shell 窗口），或承载 SHELLDLL_DefView 的 WorkerW。

    与产品侧隔离判定的修复同构：这样"点击后前台是桌面宿主"的断言既排除无 DefView 的
    WorkerW（如壁纸层，不属于桌面），也防止用例在错误的前台上给出假红/假绿。
    """
    if not hwnd:
        return False
    cls = win32gui.GetClassName(hwnd)
    if cls == "Progman":
        return True
    return cls == "WorkerW" and bool(win32gui.FindWindowEx(hwnd, 0, "SHELLDLL_DefView", None))


def _desktop_point() -> tuple:
    """找一个"点击后激活桌面宿主"的注入点：右下优先（图标默认自左上排布）。

    桌面点上 WindowFromPoint 命中的通常是 SysListView32（图标列表，铺满整屏）等子窗口，
    故取命中窗口的顶层祖先（点击会激活的前台窗口）判定是否为桌面宿主；
    候选点被任务栏/其它窗口覆盖时继续找，全部落空时抛断言——wait_until 调用时按"尚未成立"轮询。
    """
    width, height = win32api.GetSystemMetrics(0), win32api.GetSystemMetrics(1)
    for fx, fy in ((0.76, 0.72), (0.68, 0.78), (0.84, 0.62), (0.58, 0.8), (0.44, 0.76)):
        x, y = int(width * fx), int(height * fy)
        hit = win32gui.WindowFromPoint((x, y))
        if hit and _is_desktop_host(win32gui.GetAncestor(hit, win32con.GA_ROOT)):
            return x, y
    raise AssertionError("找不到落在桌面宿主上的注入点（候选点均被任务栏或其它窗口覆盖）")


def _toggle_show_desktop(settle: float = 0.4) -> None:
    """Win+D：显示桌面 / 再按一次还原（用例先腾出桌面、结束后恢复原窗口布局）。"""
    win32api.keybd_event(win32con.VK_LWIN, 0, 0, 0)
    win32api.keybd_event(ord("D"), 0, 0, 0)
    win32api.keybd_event(ord("D"), 0, win32con.KEYEVENTF_KEYUP, 0)
    win32api.keybd_event(win32con.VK_LWIN, 0, win32con.KEYEVENTF_KEYUP, 0)
    time.sleep(settle)


def _press_escape() -> None:
    """注入一次 Esc：收拾失败路径上未被抑制的右键可能弹出的桌面原生菜单。"""
    win32api.keybd_event(win32con.VK_ESCAPE, 0, 0, 0)
    win32api.keybd_event(win32con.VK_ESCAPE, 0, win32con.KEYEVENTF_KEYUP, 0)


@pytest.mark.parametrize("sandbox_seed", ["wheel-probe"], indirect=True)
def test_wheel_on_desktop_pops_wheel(app):
    """桌面宿主窗口上的右键拖动照常弹出轮盘（全屏误判回归）。

    前置：显示桌面后单击桌面点，把前台焦点切到桌面宿主（Win11 上常为承载
    SHELLDLL_DefView 的 WorkerW）。该窗口覆盖整屏但不是全屏应用，隔离判定必须排除它——
    修复前轮盘不弹（红），修复后照常弹出（绿）。
    用例先按 Win+D 腾出桌面（用户桌面可能被最大化窗口盖住），收尾再按一次还原；
    点位假设：桌面右下为空白区域（图标自左上排布）；e2e 运行期间请勿操作键鼠。
    """
    win, _ = app
    pid = win.process_id()
    win.minimize()  # 让出桌面区域，注入点落位更可控
    _toggle_show_desktop()  # 隐藏其它窗口，保证桌面点可见

    try:
        point = wait_until(
            _desktop_point,
            timeout=5.0,
            description="显示桌面后出现可注入的桌面宿主点",
        )
        left_click_at(*point)
        wait_until(
            lambda: _is_desktop_host(win32gui.GetForegroundWindow()),
            timeout=3.0,
            description="单击桌面点后前台焦点变为桌面宿主（Progman / 带 DefView 的 WorkerW）",
        )

        press_right_at(*point)
        move_by(0, -45)  # 单次移动越过 DragThreshold(25)；向上落空扇区，避免命中探针动作
        try:
            wait_until(
                lambda: find_wheel_window(pid),
                timeout=3.0,
                interval=0.02,
                description="桌面上的右键拖动越过阈值后轮盘窗口弹出",
            )
        finally:
            # 拖回按下点（中心死区内）→ 取消选中，松开不执行动作；失败路径同样收尾。
            move_by(0, 45)
            release_right(settle=0.0)
            time.sleep(0.2)
            _press_escape()
    finally:
        _toggle_show_desktop()  # 还原 Win+D 之前的最小化状态

    wait_until(
        lambda: find_wheel_window(pid) == 0,
        timeout=3.0,
        description="松手后轮盘窗口收起",
    )
