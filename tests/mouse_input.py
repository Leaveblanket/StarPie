"""合成鼠标输入：驱动全局鼠标钩子的注入层（真实轮盘手势链路用）。

产品输入栈捕获侧不做 `LLMHF_INJECTED` 过滤——注入事件与物理鼠标走同一条回调路径，
因此本模块是"在 e2e 里真实走一遍轮盘手势链路"的唯一入口。起点用 `SetCursorPos` 固定，
拖动用相对位移，避免多显示器/DPI 下的绝对坐标换算。

触发键可注入右键（默认）或侧键：侧键未被产品抑制时不弹出上下文菜单，
观测轮盘手势链路不必先收菜单再判定，故首次轮盘手势/时序类用例优先用侧键
（产品侧经 `--trigger-button=4` 把触发键换成后侧键）。
"""

import math
import time

import win32api
import win32con

_MOVE = win32con.MOUSEEVENTF_MOVE
_LEFT_DOWN = win32con.MOUSEEVENTF_LEFTDOWN
_LEFT_UP = win32con.MOUSEEVENTF_LEFTUP
_RIGHT_DOWN = win32con.MOUSEEVENTF_RIGHTDOWN
_RIGHT_UP = win32con.MOUSEEVENTF_RIGHTUP
_XDOWN = win32con.MOUSEEVENTF_XDOWN
_XUP = win32con.MOUSEEVENTF_XUP

# 侧键标识（winuser.h 的 XBUTTON1/XBUTTON2，写在 mouse_event 的 dwData 里）。
# 与产品侧触发键的对应关系（SharpHook MouseButton ⟷ libuiohook ⟷ Windows）：
#   Button4 ⟷ MOUSE_BUTTON4 ⟷ XBUTTON1（后侧键）
#   Button5 ⟷ MOUSE_BUTTON5 ⟷ XBUTTON2（前侧键）
XBUTTON1 = 0x0001
XBUTTON2 = 0x0002

# 侧键注入的默认选择：后侧键（产品侧 --trigger-button=4）。
SIDE_BUTTON = XBUTTON1


def move_to(x: int, y: int) -> None:
    """把光标移到屏幕坐标（注入层与钩子都以屏幕坐标记账）。"""
    win32api.SetCursorPos((int(x), int(y)))


def move_by(dx: int, dy: int) -> None:
    """相对移动光标（产生 WM_MOUSEMOVE 链）。"""
    win32api.mouse_event(_MOVE, int(dx), int(dy), 0, 0)


def left_click_at(x: int, y: int, settle: float = 0.15) -> None:
    """在指定屏幕坐标左键单击（不拖动）：把前台焦点交给光标下的窗口（如桌面窗口）。

    用单击而非拖动：桌面场景下拖动可能拖拽起点处的图标；单击最轻（空白处无副作用）。
    """
    move_to(x, y)
    time.sleep(settle)
    win32api.mouse_event(_LEFT_DOWN, 0, 0, 0, 0)
    time.sleep(0.05)
    win32api.mouse_event(_LEFT_UP, 0, 0, 0, 0)
    time.sleep(settle)


def right_down() -> None:
    win32api.mouse_event(_RIGHT_DOWN, 0, 0, 0, 0)


def right_up() -> None:
    win32api.mouse_event(_RIGHT_UP, 0, 0, 0, 0)


def press_right_at(x: int, y: int, settle: float = 0.15) -> None:
    """在指定屏幕坐标按下右键并留出钩子处理时间。"""
    move_to(x, y)
    time.sleep(settle)
    right_down()
    time.sleep(settle)


def drag_right(distance: int, steps: int = 8, step_delay: float = 0.04, hold: float = 0.15) -> None:
    """按住右键向右分段拖动 distance 像素（分段让钩子看到多次移动，逼近真实轮盘手势）。

    调用前需已按下右键；起点即按下点。
    """
    time.sleep(hold)
    base, remainder = divmod(int(distance), steps)
    for index in range(steps):
        move_by(base + (remainder if index == steps - 1 else 0), 0)
        time.sleep(step_delay)


def release_right(settle: float = 0.2) -> None:
    right_up()
    time.sleep(settle)


def side_down(xbutton: int = SIDE_BUTTON) -> None:
    """按下鼠标侧键（xbutton 取 XBUTTON1/XBUTTON2）。"""
    win32api.mouse_event(_XDOWN, 0, 0, xbutton, 0)


def side_up(xbutton: int = SIDE_BUTTON) -> None:
    """抬起鼠标侧键。"""
    win32api.mouse_event(_XUP, 0, 0, xbutton, 0)


def press_side_at(x: int, y: int, settle: float = 0.15, xbutton: int = SIDE_BUTTON) -> None:
    """在指定屏幕坐标按下侧键并留出钩子处理时间。"""
    move_to(x, y)
    time.sleep(settle)
    side_down(xbutton)
    time.sleep(settle)


def release_side(settle: float = 0.2, xbutton: int = SIDE_BUTTON) -> None:
    side_up(xbutton)
    time.sleep(settle)


def drag_circle(center, radius: float, steps: int, interval: float = 0.001, revolutions: float = 2.5) -> None:
    """按住状态下沿圆周分步扫掠（约 1/interval 的事件率，逼近高回报率鼠标的密集拖动）。

    以忙等定拍：Windows 上 time.sleep 的最小间隔受系统时钟粒度限制（常见 15.6ms），
    达不到"高回报率鼠标"的事件率，而密集事件正是拖动路径的负载形态。
    步进角按 revolutions 摊平，结束角 = revolutions × 360°（默认 2.5 圈即正左，
    避开预置配置里绑定了动作的正右扇区）。调用前需已按下触发键；起点即圆心。
    """
    step_angle = (revolutions * 2 * math.pi) / steps
    next_at = time.perf_counter()
    for index in range(steps):
        angle = (index + 1) * step_angle
        x = center[0] + radius * math.cos(angle)
        y = center[1] + radius * math.sin(angle)
        cur = win32api.GetCursorPos()
        next_at += interval
        while time.perf_counter() < next_at:
            pass
        win32api.mouse_event(_MOVE, int(x - cur[0]), int(y - cur[1]), 0, 0)
