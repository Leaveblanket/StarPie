"""托盘图标残留用例：测试实例退出消息走真实退出路径，不留幽灵托盘图标。

进程未 `NIM_DELETE` 就退出（硬杀）时，shell 的通知区保留"宿主窗口已失效"的死条目——幽灵托盘
图标，表现是鼠标拖入通知区才消失；e2e 每用例一次硬杀，便按用例数累加。

本用例锁两件事：测试实例退出消息被受理（进程真的走了退出编排），以及退出后通知区不再有指向该
进程托盘消息窗口的条目（`NIM_DELETE` 真的执行了）。

两处等待都自行轮询而不复用 `wait_until`：该助手把 predicate 抛出的异常当作"已成立"返回，
读通知区失败会被吞成通过——本用例的断言方向是"某条目不存在"，假绿代价尤其高。
"""

import time

import win32gui
import win32process

from conftest import TRAY_WINDOW_TITLE, TEST_INSTANCE_EXIT_MESSAGE, find_tray_window
from tray_area import find_icon

# 图标登记观察窗口：实测约 0.6s；退出后条目摘除先于进程结束。
REGISTER_OBSERVE_TIMEOUT = 5.0
EXIT_TIMEOUT = 15.0
RESIDUE_TIMEOUT = 5.0


def _observe_registered(tray_hwnd: int, timeout: float) -> bool:
    """观察通知区是否出现指向 tray_hwnd 的条目（到点即返回，不作断言）。

    explorer 偶发收下 `NIM_ADD`（返回 True）却不把图标落进工具栏按钮表：此时通知区本就没有条目，
    残留判定天然成立。该偶发与退出路径无关，故只作观察记录，不作前置断言。
    """
    deadline = time.time() + timeout
    while time.time() < deadline:
        if find_icon(tray_hwnd) is not None:
            return True
        time.sleep(0.1)
    return False


def _wait_until_true(condition, timeout: float, message: str) -> None:
    """轮询 condition 直到为真；超时抛断言。condition 抛出的异常照抛（不折算成"未成立"）。"""
    deadline = time.time() + timeout
    while True:
        if condition():
            return
        if time.time() >= deadline:
            raise AssertionError(message)
        time.sleep(0.1)


def _wait_until_absent(producer, timeout: float, message) -> None:
    """轮询 producer 直到返回 None（条目已摘除）；超时用最后一个取值拼出断言。"""
    deadline = time.time() + timeout
    while True:
        value = producer()
        if value is None:
            return
        if time.time() >= deadline:
            raise AssertionError(message(value))
        time.sleep(0.1)


def test_exit_message_leaves_no_tray_residue(app):
    """退出消息受理后进程退出，且托盘条目随 NIM_DELETE 摘除，通知区不留残留。"""
    win, _ = app
    pid = win.process_id()

    tray_hwnd = find_tray_window(pid)
    assert tray_hwnd, f"常驻托盘消息窗口必须存在（标题 {TRAY_WINDOW_TITLE}）"

    registered = _observe_registered(tray_hwnd, REGISTER_OBSERVE_TIMEOUT)
    print(f"托盘图标登记观察：{'已登记' if registered else '未登记（explorer 未落进按钮表）'} {tray_hwnd:#x}")

    # 测试实例退出消息：常驻壳层走落盘 → 释放托盘 → 应用关闭
    message = win32gui.RegisterWindowMessage(TEST_INSTANCE_EXIT_MESSAGE)
    win32gui.PostMessage(tray_hwnd, message, 0, 0)

    _wait_until_true(
        lambda: pid not in win32process.EnumProcesses(),
        EXIT_TIMEOUT,
        f"退出消息未被受理：被测应用 {EXIT_TIMEOUT}s 内未退出（pid={pid}）",
    )
    _wait_until_absent(
        lambda: find_icon(tray_hwnd),
        RESIDUE_TIMEOUT,
        lambda residue: f"通知区残留指向 {tray_hwnd:#x} 的条目（NIM_DELETE 未执行）：{residue}",
    )
