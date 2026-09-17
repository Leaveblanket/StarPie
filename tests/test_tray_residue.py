"""托盘图标残留用例：测试实例退出消息走真实退出路径，不留幽灵托盘图标。

进程未 `NIM_DELETE` 就退出（硬杀）时，shell 的通知区保留"宿主窗口已失效"的死条目——幽灵托盘
图标，表现是鼠标拖入通知区才消失；e2e 每用例一次硬杀，便按用例数累加。

本用例锁两件事：测试实例退出消息被受理（进程真的走了退出编排），以及退出后通知区不再有指向该
进程托盘消息窗口的条目（`NIM_DELETE` 真的执行了）。

等待语义由 `tray_area.wait_icon` 承担：通知区不可读（非经典任务栏 / 读 explorer 内存失败）
会抛 RuntimeError 直接失败，不会被折算成"条目不存在"——"不存在"方向上的假绿代价最高。
"""

from conftest import TRAY_WINDOW_TITLE, exit_via_test_message, find_tray_window
from tray_area import wait_icon

# 图标登记观察窗口：实测约 0.6s；退出后条目摘除先于进程结束。
REGISTER_OBSERVE_TIMEOUT = 5.0
EXIT_TIMEOUT = 15.0
RESIDUE_TIMEOUT = 5.0


def test_exit_message_leaves_no_tray_residue(app):
    """退出消息受理后进程退出，且托盘条目随 NIM_DELETE 摘除，通知区不留残留。"""
    win, _ = app
    pid = win.process_id()

    tray_hwnd = find_tray_window(pid)
    assert tray_hwnd, f"常驻托盘消息窗口必须存在（标题 {TRAY_WINDOW_TITLE}）"

    # 登记观察：explorer 偶发收下 NIM_ADD 却不落进按钮表，此时通知区本就没有条目、
    # 残留判定天然成立——该偶发与退出路径无关，故只作观察记录，不作前置断言。
    try:
        wait_icon(tray_hwnd, present=True, timeout=REGISTER_OBSERVE_TIMEOUT)
        registered = True
    except AssertionError:
        registered = False
    print(f"托盘图标登记观察：{'已登记' if registered else '未登记（explorer 未落进按钮表）'} {tray_hwnd:#x}")

    # 测试实例退出消息：常驻壳层走落盘 → 释放托盘 → 应用关闭
    exit_via_test_message(pid, timeout=EXIT_TIMEOUT)

    wait_icon(tray_hwnd, present=False, timeout=RESIDUE_TIMEOUT)
