"""测试侧 Win32 探针窗口：观察注入输入是否真的落到前台窗口。

"阈值下的触发键补发"没有任何产品 UI 可观察——补发的点击投递给光标下的窗口。
本模块给出一个只记录触发键消息的置顶窗口，作为补发落点的可观察面：
右键（默认触发键）记 `WM_RBUTTONUP`，侧键（XBUTTON1/XBUTTON2）记 `WM_XBUTTONUP`
（按键身份在 `HIWORD(wparam)`）。
"""

import threading
import time

import win32api
import win32con
import win32gui

WM_RBUTTON_UP = win32con.WM_RBUTTONUP
WM_RBUTTON_DOWN = win32con.WM_RBUTTONDOWN
# pywin32 的 win32con 未导出侧键消息（winuser.h 有），按头文件取值自备。
WM_XBUTTON_DOWN = 0x020B
WM_XBUTTON_UP = 0x020C

# 侧键身份（winuser.h 的 XBUTTON1/XBUTTON2，出现在 HIWORD(wParam) 里）；
# 与产品侧触发键的对应关系见 tests/mouse_input.py。
XBUTTON1 = 0x0001
XBUTTON2 = 0x0002

# 属主实例路由表（HWND → 实例）与进程级窗口过程：
# 窗口类在进程内**只注册一次**（RegisterClass 对同类名第二次即抛错），类上绑定的 lpfnWndProc
# 因此必须是不绑定任何实例的分发器——按 HWND 找到当前属主，交它处理。
# 若像早期实现那样把首个实例的绑定方法注册为窗口过程，同进程内**第二个**探针窗口的消息
# 会被派发到第一个实例（它可能已 stop，于是消息石沉大海）：用例顺序一变就静默失效。
_INSTANCES: dict = {}


def _dispatch(hwnd, msg, wparam, lparam):
    """窗口类分发器：按 HWND 找属主实例；未知 HWND（如 CreateWindowEx 期的 WM_CREATE）走默认处理。"""
    instance = _INSTANCES.get(hwnd)
    if instance is None:
        return win32gui.DefWindowProc(hwnd, msg, wparam, lparam)
    return instance._wndproc(hwnd, msg, wparam, lparam)


class RightClickProbeWindow:
    """置顶的普通窗口（无边框），把收到的触发键消息记进
    `right_events`（右键）与 `xbutton_events`（侧键，元素为 (msg, xbutton)）。"""

    CLASS_NAME = "StarPieE2EProbeWindow"
    WINDOW_NAME = "StarPieE2EProbeWindow"

    def __init__(self, x: int = 40, y: int = 40, width: int = 320, height: int = 220):
        self._rect = (x, y, x + width, y + height)
        self.right_events: list = []
        self.xbutton_events: list = []
        self._hwnd = 0
        self._error = ""
        self._ready = threading.Event()
        self._thread: threading.Thread | None = None

    @property
    def hwnd(self) -> int:
        return self._hwnd

    @property
    def center(self) -> tuple:
        left, top, right, bottom = self._rect
        return ((left + right) // 2, (top + bottom) // 2)

    def start(self, timeout: float = 5.0) -> "RightClickProbeWindow":
        self._thread = threading.Thread(target=self._run, daemon=True, name="e2e-probe-window")
        self._thread.start()
        assert self._ready.wait(timeout), "探针窗口未在时限内就绪"
        assert self._hwnd, f"探针窗口创建失败：{self._error}"
        return self

    def stop(self, timeout: float = 5.0) -> None:
        if not self._hwnd:
            return
        try:
            win32gui.PostMessage(self._hwnd, win32con.WM_CLOSE, 0, 0)
        except Exception:
            pass
        if self._thread:
            self._thread.join(timeout)
        self._hwnd = 0

    def wait_right_button_up(self, timeout: float = 3.0) -> bool:
        """等待窗口收到一次右键抬起（补发点击的落点证据）。"""
        deadline = time.time() + timeout
        while time.time() < deadline:
            if WM_RBUTTON_UP in self.right_events:
                return True
            time.sleep(0.05)
        return False

    def wait_xbutton_up(self, xbutton: int = XBUTTON1, timeout: float = 3.0) -> bool:
        """等待窗口收到一次指定侧键的抬起（侧键补发的落点证据）。"""
        deadline = time.time() + timeout
        while time.time() < deadline:
            if (WM_XBUTTON_UP, xbutton) in self.xbutton_events:
                return True
            time.sleep(0.05)
        return False

    def __enter__(self) -> "RightClickProbeWindow":
        return self.start()

    def __exit__(self, *exc) -> None:
        self.stop()

    # --- 窗口线程 ---

    def _run(self) -> None:
        hwnd = 0
        try:
            wc = win32gui.WNDCLASS()
            wc.lpszClassName = self.CLASS_NAME
            wc.hInstance = win32api.GetModuleHandle(None)
            # 类上绑定的是不持有实例的分发器（见 _dispatch）：同进程多实例各按 HWND 路由。
            wc.lpfnWndProc = _dispatch
            try:
                win32gui.RegisterClass(wc)
            except win32gui.error:
                # 同类名已注册（同进程多次实例化）：沿用既有注册即可——分发器已经装好。
                pass

            left, top, right, bottom = self._rect
            hwnd = win32gui.CreateWindowEx(
                win32con.WS_EX_TOPMOST | win32con.WS_EX_TOOLWINDOW,
                self.CLASS_NAME,
                self.WINDOW_NAME,
                win32con.WS_POPUP | win32con.WS_VISIBLE,
                left,
                top,
                right - left,
                bottom - top,
                0,
                0,
                wc.hInstance,
                None,
            )
            # 登记属主后才置就绪：此后到达的消息都路由到本实例。
            self._hwnd = hwnd
            _INSTANCES[hwnd] = self
            # 窗口就绪即置位；消息循环是阻塞的（PumpMessages 直到 WM_QUIT 才返回）。
            self._ready.set()
            win32gui.PumpMessages()
        except Exception as ex:
            self._error = f"{type(ex).__name__}: {ex}"
            self._ready.set()
        finally:
            # 摘除属主登记：用局部 hwnd（stop() 已把 self._hwnd 归零）。
            _INSTANCES.pop(hwnd, None)

    def _wndproc(self, hwnd, msg, wparam, lparam):
        if msg in (WM_RBUTTON_DOWN, WM_RBUTTON_UP):
            self.right_events.append(msg)
            return 0
        if msg in (WM_XBUTTON_DOWN, WM_XBUTTON_UP):
            # 侧键身份在 HIWORD(wParam)（XBUTTON1/XBUTTON2）。
            self.xbutton_events.append((msg, (wparam >> 16) & 0xFFFF))
            return 0
        if msg == win32con.WM_CLOSE:
            win32gui.DestroyWindow(hwnd)
            return 0
        if msg == win32con.WM_DESTROY:
            win32gui.PostQuitMessage(0)
            return 0
        return win32gui.DefWindowProc(hwnd, msg, wparam, lparam)
