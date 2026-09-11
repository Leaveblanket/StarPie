import json
import os
import re
import subprocess
import time
import warnings
import win32gui
import pytest
from pywinauto import Application, Desktop

# -OnScreen 调试形态（窗口可见、Save 的系统提示框真实弹出）。
# 默认（静默后台形态）被测应用离屏且提示框不呈现（ADR-0031），e2e 不应等待任何弹窗。
ONSCREEN = os.environ.get("STARPIE_E2E_ONSCREEN") == "1"

# 失败截图依赖 PIL：缺件时 pytest header 常显 + warnings.warn（#136），
# 后台（-NoWait）形态下不再只有无人可见的 stdout。
try:
    import PIL  # noqa: F401

    PIL_AVAILABLE = True
except ImportError:
    PIL_AVAILABLE = False

_screenshot_warned = False


def warn_screenshot_unavailable(reason: str) -> None:
    """显式暴露失败截图缺口（warning 级；环境缺件不升格为测试失败）。"""
    global _screenshot_warned
    if _screenshot_warned:
        return
    _screenshot_warned = True
    warnings.warn(
        f"失败截图不可用：{reason}（pip install -r tests/requirements.txt 恢复，见 #136）",
        stacklevel=2,
    )


def pytest_report_header(config):
    """PIL 缺失时在每次运行的 header 常显缺口（-Status 另经 screenshotAvailable 汇总）。"""
    if not PIL_AVAILABLE:
        return "失败截图不可用：PIL 未安装（-Status 会显示 screenshotAvailable=false；见 #136）"
    return None


def dismiss_messagebox(timeout: float = 3.0) -> None:
    """关闭 Save 触发的系统提示框（#32770）。

    仅 -OnScreen 调试形态会有提示框；静默后台形态下 DialogService 直接不呈现，
    立即返回，避免每个用例白等 timeout（ADR-0031）。
    """
    if not ONSCREEN:
        return
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=timeout):
            dialog.child_window(control_type="Button").invoke()
    except Exception:
        pass


# 每个页面的"锚点控件"：页面 View 按导航重建（旧页卸载、新页挂载），锚点出现即证明真的切到了该页。
PAGE_ANCHORS = {
    0: ("EnableOuterEscapeCheckBox", "CheckBox"),
    1: ("AppearancePageSubheader", "Text"),
    2: ("GesturesPageSubheader", "Text"),
    3: ("AdvancedPageSubheader", "Text"),
}


def goto(win, slot: int, timeout: float = 5.0):
    """选中侧边栏导航项并等待目标页锚点出现。

    导航失败在这里显式失败，而不是靠后续 `is_visible()` 之类的弱断言侥幸通过。
    """
    radio = win.child_window(auto_id=f"NavPage{slot}", control_type="RadioButton")
    assert radio.exists(timeout=timeout), f"NavPage{slot} 必须存在"
    radio.select()
    anchor_id, anchor_type = PAGE_ANCHORS[slot]
    anchor = win.child_window(auto_id=anchor_id, control_type=anchor_type)
    assert anchor.exists(timeout=timeout), f"导航到 NavPage{slot} 后未出现锚点控件 {anchor_id}"
    return anchor


def read_config(local_app_data, predicate=None, timeout: float = 5.0):
    """轮询读取沙盒 config.json：等文件出现、可选等 predicate 成立；超时抛带诊断信息的断言。

    替代"固定 sleep 后直接 open/json.load"——落盘稍慢时不再读到旧值或抛 FileNotFoundError。
    """
    path = os.path.join(str(local_app_data), "StarPie", "config.json")
    deadline = time.time() + timeout
    last = None
    last_err = ""
    while True:
        try:
            with open(path, "r", encoding="utf-8") as f:
                last = json.load(f)
            if predicate is None or predicate(last):
                return last
        except FileNotFoundError:
            last_err = f"config.json 不存在: {path}"
        except json.JSONDecodeError as ex:
            last_err = f"config.json 解析失败: {ex}"
        if time.time() >= deadline:
            break
        time.sleep(0.1)
    raise AssertionError(f"等待 config.json 超时（{timeout}s）。{last_err} 最后内容: {last}")


def label_value(win, auto_id: str, timeout: float = 3.0) -> float:
    """读取数值标签的浮点值（容忍前后缀文案，取第一段数字），替代 `"26" in text` 式包含断言。"""
    label = win.child_window(auto_id=auto_id, control_type="Text")
    assert label.exists(timeout=timeout), f"{auto_id} 数值标签必须存在"
    text = label.window_text()
    match = re.search(r"-?\d+(?:\.\d+)?", text)
    assert match, f"{auto_id} 标签不含数值: {text!r}"
    return float(match.group())


def wait_dialog(title: str, timeout: float = 10.0):
    """等待并返回指定标题的顶层对话框（win32 按 HWND 查找 + UIA 包装为 WindowSpecification）。

    后台形态下对话框离屏且为 owned 窗口，pywinauto 的进程/桌面枚举不一定包含它，
    但 win32 枚举可见；拿到 HWND 后经 handle 绑定，child_window/invoke 与常规 WindowSpecification 一致。
    """
    deadline = time.time() + timeout
    while True:
        hwnd = win32gui.FindWindow(None, title)
        if hwnd:
            return Desktop(backend="uia").window(handle=hwnd)
        if time.time() >= deadline:
            raise AssertionError(f"等待对话框超时（{timeout}s）：{title!r}")
        time.sleep(0.1)


def wait_dialog_closed(dialog, timeout: float = 5.0) -> None:
    """等待对话框关闭；句柄失效（销毁后 UIA 解析抛 COMError）等价于已关闭。

    pywinauto 的 exists() 命中即返回、不等待关闭；窗口销毁瞬间句柄查询会抛
    COMError，故此轮询同时处理"仍存在"与"已不可解析"两种状态。
    """
    deadline = time.time() + timeout
    while True:
        try:
            if not dialog.exists(timeout=0.2):
                return
        except Exception:
            return
        if time.time() >= deadline:
            raise AssertionError(f"等待对话框关闭超时（{timeout}s）")
        time.sleep(0.1)


@pytest.fixture(scope="function")
def sandbox_env(tmp_path):
    """
    Sets up isolated AppData folder structures to prevent config pollution.
    """
    env = os.environ.copy()
    local_app_data = tmp_path / "AppData" / "Local"
    app_data = tmp_path / "AppData" / "Roaming"
    temp_dir = tmp_path / "Temp"
    
    for path in (local_app_data, app_data, temp_dir):
        path.mkdir(parents=True, exist_ok=True)
        
    env["LOCALAPPDATA"] = str(local_app_data)
    env["APPDATA"] = str(app_data)
    env["TEMP"] = str(temp_dir)
    env["TMP"] = str(temp_dir)
    
    return env, local_app_data

@pytest.fixture(scope="function")
def app(sandbox_env, request):
    env, local_app_data = sandbox_env

    if not PIL_AVAILABLE:
        warn_screenshot_unavailable("PIL 未安装")

    # Locate the executable
    project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    candidates = [
        os.path.join(project_root, "StarPie", "bin", config, tfm, "StarPie.exe")
        for config in ("Release", "Debug")
        for tfm in ("net10.0-windows10.0.19041.0", "net10.0-windows")
    ]
    app_path = next((c for c in candidates if os.path.exists(c)), None)
    if not app_path:
        pytest.fail(f"Executable not found in {candidates}. Please build the project first.")
        
    # Start the process with sandboxed environment variables.
    # 默认静默后台形态（--background：离屏 + 不可激活 + 不进任务栏/托盘，不打扰同机用户）；
    # ONSCREEN（STARPIE_E2E_ONSCREEN=1，scripts/run-e2e.ps1 -OnScreen）时窗口正常显示，供调试。
    flags = ["--allow-multiple"]
    if not ONSCREEN:
        flags.append("--background")
    proc = subprocess.Popen([app_path, *flags], env=env)
    
    # Connect pywinauto using PID; poll for readiness instead of a fixed sleep
    try:
        pw_app = Application(backend="uia").connect(process=proc.pid, timeout=15)
        win = pw_app.window(title_re="StarPie.*")
        win.wait("visible", timeout=15)
    except Exception as ex:
        proc.terminate()
        pytest.fail(f"Failed to launch or connect to application window: {ex}")
        
    yield win, local_app_data
    
    # Screenshot on failure
    if getattr(getattr(request.node, "rep_call", None), "failed", False):
        # 诊断：dump 本进程全部顶层窗口（定位 ElementAmbiguousError 之类的"第二个同名窗口"）
        try:
            import win32gui
            import win32process

            rows = []

            def _collect(hwnd, _):
                try:
                    if win32process.GetWindowThreadProcessId(hwnd)[1] == proc.pid:
                        rows.append({
                            "cls": win32gui.GetClassName(hwnd)[:44],
                            "title": win32gui.GetWindowText(hwnd)[:40],
                            "rect": list(win32gui.GetWindowRect(hwnd)),
                            "visible": bool(win32gui.IsWindowVisible(hwnd)),
                        })
                except Exception:
                    pass
                return True

            win32gui.EnumWindows(_collect, None)
            print(f"window dump (pid={proc.pid}): {json.dumps(rows, ensure_ascii=False)}")
        except Exception as ex:
            print(f"window dump 失败: {type(ex).__name__}: {ex}")

        artifacts_dir = os.path.join(project_root, "artifacts")
        os.makedirs(artifacts_dir, exist_ok=True)
        try:
            img = win.capture_as_image()
            if img is None:
                # capture_as_image 需要 PIL；venv 未装时返回 None（既有环境缺口，见 ADR-0031/#136）
                warn_screenshot_unavailable("capture_as_image 返回 None")
            else:
                path = os.path.join(artifacts_dir, f"FAIL_{request.node.name}.png")
                img.save(path)
                print(f"失败截图已保存：{path}")
        except Exception as ex:
            warn_screenshot_unavailable(f"截图异常：{type(ex).__name__}: {ex}")
            
    # Clean shutdown
    try:
        proc.kill()
        proc.wait(timeout=2)
    except Exception:
        pass

@pytest.hookimpl(tryfirst=True, hookwrapper=True)
def pytest_runtest_makereport(item, call):
    outcome = yield
    setattr(item, f"rep_{outcome.get_result().when}", outcome.get_result())
