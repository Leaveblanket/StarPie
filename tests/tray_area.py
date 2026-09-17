"""通知区（系统托盘）图标明细读取。

通知区是 explorer 的工具栏控件：主区 `Shell_TrayWnd/TrayNotifyWnd/SysPager/ToolbarWindow32`、
溢出区 `NotifyIconOverflowWindow/ToolbarWindow32`。每个按钮的 `dwData` 指向 explorer 堆里的
TRAYDATA，**首字段是图标的宿主 HWND**（布局无歧义，其余字段不参与判定）。

宿主 HWND 是"图标还在不在"的唯一凭据：进程未 `NIM_DELETE` 就退出（硬杀）时，shell 会保留该条目
直到通知区收到鼠标输入——即幽灵托盘图标（宿主窗口已失效的死条目）。
"""

import ctypes
import time
from ctypes import wintypes

import win32api
import win32con
import win32gui
import win32process

kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

TB_BUTTONCOUNT = win32con.WM_USER + 24
TB_GETBUTTON = win32con.WM_USER + 23

_PROCESS_VM_READ = 0x0010
_PROCESS_VM_OPERATION = 0x0008
_PROCESS_QUERY_INFORMATION = 0x0400
_MEM_COMMIT = 0x1000
_MEM_RELEASE = 0x8000
_PAGE_READWRITE = 0x04

# 通知区两级工具栏的窗口链：主区在每个任务栏（多显示器各有副任务栏）的 SysPager 下，
# 溢出区是独立顶层窗。链尾是承载托盘按钮的 ToolbarWindow32。
_MAIN_ROOTS = ("Shell_TrayWnd", "Shell_SecondaryTrayWnd")
_MAIN_TAIL = ("TrayNotifyWnd", "SysPager", "ToolbarWindow32")
_OVERFLOW_ROOT = "NotifyIconOverflowWindow"


class TBBUTTON(ctypes.Structure):
    _fields_ = [
        ("iBitmap", ctypes.c_int),
        ("idCommand", ctypes.c_int),
        ("fsState", ctypes.c_ubyte),
        ("fsStyle", ctypes.c_ubyte),
        ("bReserved", ctypes.c_ubyte * 6),
        ("dwData", ctypes.c_void_p),
        ("iString", ctypes.c_void_p),
    ]


class TRAYDATA(ctypes.Structure):
    """explorer 堆里每个托盘按钮的私有结构（只取前两字段：宿主 HWND 与图标 id）。

    后续字段（回调消息 / uFlags / hIcon / szTip）的布局随 shell 版本变过，不参与判定。
    """

    _fields_ = [
        ("hwnd", ctypes.c_void_p),
        ("uID", wintypes.UINT),
    ]


def _top_level_windows(class_name: str):
    """全部指定类名的顶层窗口（同类窗口可能不止一个：多显示器任务栏、shell 重建的溢出窗）。"""
    found = []
    hwnd = win32gui.FindWindowEx(0, 0, class_name, None)
    while hwnd:
        found.append(hwnd)
        hwnd = win32gui.FindWindowEx(0, hwnd, class_name, None)
    return found


def _toolbars():
    """(区域名, toolbar HWND) 列表——主区与溢出区的**全部**实例取并集。

    只取每类窗口的第一个匹配会漏图标：shell 重建通知区时会短暂并存新旧工具栏窗口，
    按第一个命中读到的是旧实例的按钮表（表现为"新登记的图标不存在"）。
    """
    found = []
    for area, roots, tail in (
        ("main", _MAIN_ROOTS, _MAIN_TAIL),
        ("overflow", (_OVERFLOW_ROOT,), ("ToolbarWindow32",)),
    ):
        for root_class in roots:
            for root in _top_level_windows(root_class):
                hwnd = root
                for class_name in tail:
                    hwnd = win32gui.FindWindowEx(hwnd, 0, class_name, None) if hwnd else 0
                if hwnd and all(hwnd != existing for _area, existing in found):
                    found.append((area, hwnd))
    return found


def icons():
    """通知区全部图标的明细：`[{"area", "hwnd", "uID", "alive"}]`（主区 + 溢出区）。

    通知区不可读（非经典任务栏 / 无法读 explorer 内存）时抛异常而非返回空列表——
    空列表会让"没有残留条目"之类的断言静默通过。
    """
    toolbars = _toolbars()
    if not toolbars:
        raise RuntimeError("未找到通知区工具栏（Shell_TrayWnd/TrayNotifyOverflowWindow 窗口链）")
    explorer_pid = win32process.GetWindowThreadProcessId(toolbars[0][1])[1]
    process = kernel32.OpenProcess(
        _PROCESS_VM_READ | _PROCESS_VM_OPERATION | _PROCESS_QUERY_INFORMATION, False, explorer_pid
    )
    if not process:
        raise RuntimeError(f"无法读取 explorer（pid={explorer_pid}）内存：错误码 {ctypes.get_last_error()}")

    rows = []
    try:
        buffer = kernel32.VirtualAllocEx(
            process, None, ctypes.sizeof(TBBUTTON), _MEM_COMMIT, _PAGE_READWRITE
        )
        if not buffer:
            raise RuntimeError(f"explorer 进程内分配读缓冲失败：错误码 {ctypes.get_last_error()}")
        try:
            for area, toolbar in toolbars:
                for index in range(win32api.SendMessage(toolbar, TB_BUTTONCOUNT, 0, 0)):
                    button = TBBUTTON()
                    if not win32api.SendMessage(toolbar, TB_GETBUTTON, index, buffer):
                        # 不静默跳过：读不到就报（跳过会让"某条目不存在"的断言在自己读失败时假绿）。
                        raise RuntimeError(f"读取通知区按钮失败（{area} 区第 {index} 个）")
                    _read(process, buffer, button, "按钮")
                    tray = TRAYDATA()
                    _read(process, button.dwData, tray, "TRAYDATA")
                    host = int(tray.hwnd or 0)
                    rows.append(
                        {
                            "area": area,
                            "hwnd": host,
                            "uID": tray.uID,
                            "alive": bool(host) and bool(win32gui.IsWindow(host)),
                        }
                    )
        finally:
            kernel32.VirtualFreeEx(process, buffer, 0, _MEM_RELEASE)
    finally:
        kernel32.CloseHandle(process)
    return rows


def _read(process, address, target, what: str) -> None:
    """跨进程读一块内存；失败即抛（读失败留下的零值会被误读成"没有这个条目"）。"""
    if not kernel32.ReadProcessMemory(
        process, ctypes.c_void_p(address), ctypes.byref(target), ctypes.sizeof(target), None
    ):
        raise RuntimeError(f"读取 explorer 内存失败（{what}）：错误码 {ctypes.get_last_error()}")


def find_icon(host_hwnd: int):
    """通知区里宿主窗口为 `host_hwnd` 的图标条目；不存在时为 None。"""
    return next((icon for icon in icons() if icon["hwnd"] == host_hwnd), None)


def wait_icon(host_hwnd: int, present: bool, timeout: float = 5.0):
    """轮询通知区直到 `host_hwnd` 的条目出现（present=True）/摘除（present=False）；超时抛断言。

    读取失败（icons() 抛 RuntimeError，通知区不可读）直接向上抛、不折算成"条目不存在"——
    "不存在"方向上的假绿代价最高（残留判定天然成立会掩盖 NIM_DELETE 未执行）。
    本模块不依赖 pytest 夹具模块，故等待循环自带（语义与 conftest.wait_until 一致：异常即失败）。
    """
    deadline = time.time() + timeout
    last = None
    while True:
        last = find_icon(host_hwnd)
        if (last is not None) == present:
            return last
        if time.time() >= deadline:
            state = "出现" if present else "摘除"
            raise AssertionError(
                f"通知区条目未在 {timeout}s 内{state}（host={host_hwnd:#x}，最后取值: {last}）"
            )
        time.sleep(0.1)
