"""合成鼠标输入：驱动全局鼠标钩子的注入层（真实手势链路用）。

产品输入栈捕获侧不做 `LLMHF_INJECTED` 过滤——注入事件与物理鼠标走同一条回调路径，
因此本模块是"在 e2e 里真实走一遍手势链路"的唯一入口。起点用 `SetCursorPos` 固定，
拖动用相对位移，避免多显示器/DPI 下的绝对坐标换算。
"""

import time

import win32api
import win32con

_MOVE = win32con.MOUSEEVENTF_MOVE
_RIGHT_DOWN = win32con.MOUSEEVENTF_RIGHTDOWN
_RIGHT_UP = win32con.MOUSEEVENTF_RIGHTUP


def move_to(x: int, y: int) -> None:
    """把光标移到屏幕坐标（注入层与钩子都以屏幕坐标记账）。"""
    win32api.SetCursorPos((int(x), int(y)))


def move_by(dx: int, dy: int) -> None:
    """相对移动光标（产生 WM_MOUSEMOVE 链）。"""
    win32api.mouse_event(_MOVE, int(dx), int(dy), 0, 0)


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
    """按住右键向右分段拖动 distance 像素（分段让钩子看到多次移动，逼近真实手势）。

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
