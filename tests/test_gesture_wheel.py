"""手势链路 e2e：注射鼠标输入 → 全局钩子 → 手势引擎 → 轮盘窗口 → 动作执行。

这是 xUnit 覆盖不到的集成面：轮盘窗口真的弹出/收起、扇区动作真的被执行
（Launch 探针 exe 的进程出现）、阈值下的右键补发真的落到光标下的窗口。
产品钩子不做 LLMHF_INJECTED 过滤，注入事件与物理操作走同一条回调路径
（注入层见 tests/mouse_input.py），因此"没有产品内测试通道"不妨碍真实链路被驱动。

配置由 sandbox_seed="gesture-probe" 预置：Global 4 扇区、DragThreshold=25、
OuterEscapeDistance=186，扇区 0（正右）是探针 exe 的 Launch 动作，其余扇区空动作。

运行期间请勿操作键鼠（全局钩子在跑，注入输入与真实鼠标共用同一个游标）。
"""

import time
import warnings

import pytest
from conftest import (
    find_process_by_executable,
    find_wheel_window,
    kill_processes,
    probe_exe_from_config,
    wait_process_started,
    wait_until,
)
from mouse_input import drag_right, move_by, move_to, press_right_at, release_right
from win32_probe import RightClickProbeWindow

# 手势起点：默认设置台窗口覆盖区内的固定点（远离任务栏/托盘，避免注入点击命中系统 UI）。
START = (600, 400)

# 越过 DragThreshold(25)、远小于 OuterEscapeDistance(186) 的拖动距离 → 命中正右扇区 0。
SECTOR_DRAG = 110
# 越过 OuterEscapeDistance(186) 的外甩距离 → 触发外圈逃逸取消。
ESCAPE_DRAG = 240
# 中心死区（阈值 25 × 0.6 = 15px）以内即取消选中；这里先外拖再拖回起点。
CENTER_RETURN_DRAG = 110


@pytest.mark.parametrize("sandbox_seed", ["gesture-probe"], indirect=True)
def test_gesture_below_threshold_replays_right_click(app):
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


@pytest.mark.parametrize("sandbox_seed", ["gesture-probe"], indirect=True)
def test_gesture_drag_pops_wheel_and_executes_sector_action(app):
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


@pytest.mark.parametrize("sandbox_seed", ["gesture-probe"], indirect=True)
def test_gesture_outer_escape_cancels_without_executing(app):
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


@pytest.mark.parametrize("sandbox_seed", ["gesture-probe"], indirect=True)
def test_gesture_return_to_center_cancels_without_executing(app):
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
