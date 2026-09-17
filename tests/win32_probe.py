"""测试侧 Win32 探针窗口：观察注入输入是否真的落到前台窗口。

"阈值下的右键补发"没有任何产品 UI 可观察——补发的点击投递给光标下的窗口。
本模块给出一个只记录右键消息的置顶窗口，作为补发落点的可观察面。
"""

import threading
import time

import win32api
import win32con
import win32gui

WM_RBUTTON_UP = win32con.WM_RBUTTONUP
WM_RBUTTON_DOWN = win32con.WM_RBUTTONDOWN


class RightClickProbeWindow:
    """置顶的普通窗口（无边框），把收到的右键消息记进 `right_events`。"""

    CLASS_NAME = "StarPieE2EProbeWindow"
    WINDOW_NAME = "StarPieE2EProbeWindow"

    def __init__(self, x: int = 40, y: int = 40, width: int = 320, height: int = 220):
        self._rect = (x, y, x + width, y + height)
        self.right_events: list = []
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

    def __enter__(self) -> "RightClickProbeWindow":
        return self.start()

    def __exit__(self, *exc) -> None:
        self.stop()

    # --- 窗口线程 ---

    def _run(self) -> None:
        try:
            wc = win32gui.WNDCLASS()
            wc.lpszClassName = self.CLASS_NAME
            wc.hInstance = win32api.GetModuleHandle(None)
            wc.lpfnWndProc = self._wndproc
            try:
                win32gui.RegisterClass(wc)
            except win32gui.error:
                # 同类名已注册（同进程多次实例化）：沿用既有注册即可。
                pass

            left, top, right, bottom = self._rect
            self._hwnd = win32gui.CreateWindowEx(
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
            # 窗口就绪即置位；消息循环是阻塞的（PumpMessages 直到 WM_QUIT 才返回）。
            self._ready.set()
            win32gui.PumpMessages()
        except Exception as ex:
            self._error = f"{type(ex).__name__}: {ex}"
            self._ready.set()

    def _wndproc(self, hwnd, msg, wparam, lparam):
        if msg in (WM_RBUTTON_DOWN, WM_RBUTTON_UP):
            self.right_events.append(msg)
            return 0
        if msg == win32con.WM_CLOSE:
            win32gui.DestroyWindow(hwnd)
            return 0
        if msg == win32con.WM_DESTROY:
            win32gui.PostQuitMessage(0)
            return 0
        return win32gui.DefWindowProc(hwnd, msg, wparam, lparam)
