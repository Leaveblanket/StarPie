import json
import os
import re
import subprocess
import time
import warnings
from ctypes import COMError

import pytest
import win32gui
import win32process
import win32ui
from pywinauto import Application, Desktop
from pywinauto.findwindows import ElementNotFoundError

# -OnScreen 调试形态（窗口可见、Save 的系统提示框真实弹出）。
# 默认（静默形态）被测应用在屏幕左上角且提示框不呈现（ADR-0031/0032），e2e 不应等待任何弹窗。
ONSCREEN = os.environ.get("STARPIE_E2E_ONSCREEN") == "1"

# 失败截图依赖 PIL（依赖清单见 tests/requirements.txt）。静默形态窗口固定在屏幕左上角、
# 被 DWM 合成，PrintWindow 能抓到真实内容；缺 PIL 时显式告警并把原因写进运行 header。
try:
    import PIL  # noqa: F401

    PIL_AVAILABLE = True
except ImportError:
    PIL_AVAILABLE = False

_screenshot_warned = False


def screenshot_unavailable_reason():
    """失败截图不可用的原因；可用时返回 None（header/告警/运行器口径共用一份判断）。"""
    if not PIL_AVAILABLE:
        return "PIL 未安装（pip install -r tests/requirements.txt 恢复）"
    return None


def warn_screenshot_unavailable(reason: str) -> None:
    """显式暴露失败截图缺口（warning 级；环境缺件不升格为测试失败）。"""
    global _screenshot_warned
    if _screenshot_warned:
        return
    _screenshot_warned = True
    warnings.warn(
        f"失败截图不可用：{reason}",
        stacklevel=2,
    )


def pytest_report_header(config):
    """截图不可用时把原因写进每次运行的 header（-Status 另有 screenshotAvailable 汇总）。"""
    reason = screenshot_unavailable_reason()
    return f"失败截图不可用：{reason}" if reason else None


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


def _process_windows(pid: int) -> list:
    """枚举进程全部顶层窗口（含隐藏窗）；原始字段供 dump 与主窗口解析共用。"""
    rows = []

    def _collect(hwnd, _):
        try:
            if win32process.GetWindowThreadProcessId(hwnd)[1] == pid:
                rows.append({
                    "hwnd": hwnd,
                    "class": win32gui.GetClassName(hwnd)[:60],
                    "title": win32gui.GetWindowText(hwnd)[:60],
                    "rect": list(win32gui.GetWindowRect(hwnd)),
                    "visible": bool(win32gui.IsWindowVisible(hwnd)),
                    "owner": win32gui.GetWindow(hwnd, 4),  # GW_OWNER；0 = 无属主
                })
        except Exception:
            pass
        return True

    win32gui.EnumWindows(_collect, None)
    return rows


def dump_process_windows(pid: int, label: str = "进程顶层窗口") -> list:
    """打印进程全部顶层窗口清单，供失败与偶发场景取证。

    窗口匹配阶段的异常此前只有 pywinauto 的"N elements match"计数、没有窗口清单；
    本函数把候选的 hwnd/类名/标题/矩形/可见性/属主落进运行日志。
    """
    rows = _process_windows(pid)
    printable = [
        {**row, "hwnd": hex(row["hwnd"]), "owner": hex(row["owner"]) if row["owner"] else "0x0"}
        for row in rows
    ]
    print(f"{label} (pid={pid}): {json.dumps(printable, ensure_ascii=False)}")
    return rows


def _rect_area(rect: list) -> int:
    left, top, right, bottom = rect
    return max(0, right - left) * max(0, bottom - top)


def find_main_window(pid: int, timeout: float = 15.0) -> int:
    """解析主窗口 HWND：标题以 StarPie 开头的可见顶层窗口里取面积最大者。

    主窗口按 HWND 绑定（Desktop.window(handle=...)）而不是按标题正则匹配，绕开 pywinauto
    "标题命中多个元素即 ElementAmbiguousError"的解析路径；候选多于一个时先落窗口 dump 取证，
    再取面积最大者（隐藏窗标题不匹配、对话框面积远小于主窗口）。
    """
    deadline = time.time() + timeout
    while True:
        candidates = [
            row for row in _process_windows(pid)
            if row["visible"] and row["title"].startswith("StarPie")
        ]
        if candidates:
            if len(candidates) > 1:
                dump_process_windows(pid, label="主窗口候选不唯一（取面积最大者）")
                warnings.warn(
                    f"主窗口候选不唯一（{len(candidates)} 个）：已取面积最大者，dump 见运行日志",
                    stacklevel=2,
                )
            return max(candidates, key=lambda row: _rect_area(row["rect"]))["hwnd"]
        if time.time() >= deadline:
            raise AssertionError(f"等待主窗口超时（{timeout}s，pid={pid}）")
        time.sleep(0.1)


def capture_window_image(hwnd: int):
    """PrintWindow(PW_RENDERFULLCONTENT) 抓窗口位图，返回 PIL Image；取不到内容时返回 None。

    静默形态窗口在屏幕内，本函数抓到的即当前真实画面。
    """
    left, top, right, bottom = win32gui.GetWindowRect(hwnd)
    width, height = right - left, bottom - top
    if width <= 0 or height <= 0:
        return None
    hdc = win32gui.GetWindowDC(hwnd)
    try:
        src = win32ui.CreateDCFromHandle(hdc)
        dst = src.CreateCompatibleDC()
        bitmap = win32ui.CreateBitmap()
        try:
            bitmap.CreateCompatibleBitmap(src, width, height)
            dst.SelectObject(bitmap)
            from ctypes import windll

            if not windll.user32.PrintWindow(hwnd, dst.GetSafeHdc(), 2):  # PW_RENDERFULLCONTENT
                return None
            info = bitmap.GetInfo()
            bits = bitmap.GetBitmapBits(True)
            from PIL import Image

            return Image.frombuffer(
                "RGB", (info["bmWidth"], info["bmHeight"]), bits, "raw", "BGRX", 0, 1
            ).copy()
        finally:
            win32gui.DeleteObject(bitmap.GetHandle())
            dst.DeleteDC()
            src.DeleteDC()
    finally:
        win32gui.ReleaseDC(hwnd, hdc)


def is_blank_image(img) -> bool:
    """客户区单色（未渲染/未合成空壳的实际表现）视为无内容，不落盘以免误导。"""
    client = img.crop((0, min(40, img.height // 4), img.width, img.height))
    colors = client.getcolors(maxcolors=2)
    return colors is not None and len(colors) == 1


# 每个页面的"锚点控件"：页面 View 按导航重建（旧页卸载、新页挂载），锚点出现即证明真的切到了该页。
PAGE_ANCHORS = {
    0: ("EnableOuterEscapeCheckBox", "CheckBox"),
    1: ("AppearancePageSubheader", "Text"),
    2: ("GesturesPageSubheader", "Text"),
    3: ("AdvancedPageSubheader", "Text"),
    4: ("PluginManagerSubheader", "Text"),
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


def wait_until(predicate, timeout: float = 5.0, interval: float = 0.1, description: str = "条件成立"):
    """轮询 predicate 直到返回真值；超时抛带最后取值的断言（替代固定 sleep）。"""
    deadline = time.time() + timeout
    last = None
    while True:
        try:
            last = predicate()
        except Exception as ex:
            last = f"{type(ex).__name__}: {ex}"
        if last:
            return last
        if time.time() >= deadline:
            raise AssertionError(f"等待「{description}」超时（{timeout}s），最后取值: {last!r}")
        time.sleep(interval)


def text_of(win, auto_id: str, control_type: str = "Text", timeout: float = 1.0) -> str:
    """读控件文本；控件不存在即断言失败（供轮询调用，单次等待要短）。"""
    control = win.child_window(auto_id=auto_id, control_type=control_type)
    assert control.exists(timeout=timeout), f"{auto_id} 必须存在"
    return control.window_text()


def assert_text_contains(win, auto_id: str, control_type: str, expected: str, timeout: float = 5.0) -> str:
    """轮询等待控件文本包含 expected 后返回该文本（替代"改设置后 sleep 再断言"）。"""
    seen = {}

    def _matches():
        seen["text"] = text_of(win, auto_id, control_type, timeout=0.3)
        return expected in seen["text"]

    wait_until(_matches, timeout=timeout, description=f"{auto_id} 文本包含 {expected!r}")
    return seen["text"]


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


def wait_for_label_value(win, auto_id: str, expected: float, timeout: float = 3.0) -> float:
    """轮询数值标签直到等于期望值（替代滑块操作后的固定等待）。"""
    seen = {}

    def _matches():
        seen["value"] = label_value(win, auto_id, timeout=0.3)
        return abs(seen["value"] - expected) < 0.01

    wait_until(
        _matches,
        timeout=timeout,
        description=f"{auto_id} 显示 {expected}",
    )
    return seen["value"]


def list_item_texts(list_box, timeout: float = 1.0) -> list:
    """读列表框全部项文本；列表不存在即断言失败。"""
    assert list_box.exists(timeout=timeout), "列表控件必须存在"
    return [item.window_text() for item in list_box.children(control_type="ListItem")]


def assert_catalog(combo, catalog, timeout: float = 3.0) -> None:
    """断言下拉项目录与产品目录一致；目录变更须同步测试常量（失败信息带两侧目录）。"""
    assert combo.exists(timeout=timeout), "下拉控件必须存在"
    count = combo.item_count()
    assert count == len(catalog), (
        f"下拉项数与产品目录不一致：期望 {len(catalog)} 项 {list(catalog)}，实为 {count} 项；"
        f"产品目录确已变更时同步更新测试目录常量"
    )


def select_option(
    combo,
    catalog,
    name: str,
    timeout: float = 3.0,
    verify_selection: bool = True,
    attempts: int = 3,
) -> int:
    """按产品目录（Tag 固定顺序）选择下拉项，并等待选中态回读一致后返回 index。

    UIA 不暴露选项文本（item_texts 不可用）、SelectedValuePath 取 Tag，故用目录常量定名：
    catalog 即产品侧下拉项顺序，item_count 不符会显式失败（目录变更须同步测试常量）；
    选中态回读保证点击 Save 前 UI 已真的切换，替代 `select(int)` + 固定等待。
    UIA `select(index)` 偶发静默空选（选中态停在 null），回读不通过时重试 select，
    连试 attempts 次仍不收敛才失败。
    verify_selection=False 用于 UIA 不暴露选中态的 ComboBox（selected_index 恒为 None，
    如 Slot*ActionTypeComboBox）：此时以用例的落盘值断言为门。
    """
    assert_catalog(combo, catalog, timeout)
    if name not in catalog:
        raise AssertionError(f"产品目录 {list(catalog)} 中不存在选项 {name!r}")
    index = catalog.index(name)
    if not verify_selection:
        combo.select(index)
        return index

    per_attempt = max(0.5, timeout / attempts)
    last_error = None
    for attempt in range(attempts):
        combo.select(index)
        try:
            wait_until(
                lambda: combo.selected_index() == index,
                timeout=per_attempt,
                description=f"{name}（index {index}）选中态回读",
            )
            return index
        except AssertionError as ex:
            last_error = ex
    raise AssertionError(
        f"选择 {name!r}（index {index}）后选中态未回读，重试 {attempts} 次仍失败：{last_error}"
    ) from None


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
    COMError，故此轮询同时处理"仍存在"与"已不可解析"两种状态。只把这两类"窗口已没了"
    的异常当作已关闭，其它异常照常抛出（宽 except 会把真实问题吞成"已关闭"）。
    """
    deadline = time.time() + timeout
    while True:
        try:
            if not dialog.exists(timeout=0.2):
                return
        except (COMError, ElementNotFoundError):
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
def sandbox_seed(request, sandbox_env):
    """
    沙箱预置钩子：在应用启动前向沙箱写入文件，默认不写任何东西。

    用例经 `@pytest.mark.parametrize("sandbox_seed", [...], indirect=True)` 取用某个预置形态，
    只影响该用例的启动环境（如"停用内置程序来源插件"的宿主状态）。
    """
    env, local_app_data = sandbox_env
    mode = getattr(request, "param", None)
    if mode == "disabled-program-source":
        state_dir = local_app_data / "StarPie"
        state_dir.mkdir(parents=True, exist_ok=True)
        (state_dir / "plugin-state.json").write_text(
            json.dumps(
                {
                    "SchemaVersion": 1,
                    "DeveloperModeEnabled": False,
                    "Plugins": {"starpie.builtin.program-source": {"Enabled": False}},
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
    elif mode == "quarantined-program-source":
        state_dir = local_app_data / "StarPie"
        state_dir.mkdir(parents=True, exist_ok=True)
        (state_dir / "plugin-state.json").write_text(
            json.dumps(
                {
                    "SchemaVersion": 1,
                    "DeveloperModeEnabled": False,
                    "Plugins": {
                        "starpie.builtin.program-source": {
                            "Enabled": True,
                            "Quarantine": {
                                "Reason": "e2e 预置隔离：装载失败",
                                "Since": "2026-01-01T00:00:00+08:00",
                                "Residuals": [
                                    {
                                        "Kind": "Assembly",
                                        "Detail": "入口程序集 StarPie.Plugin.Programs",
                                    }
                                ],
                            },
                        }
                    },
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
    return mode


@pytest.fixture(scope="function")
def app(sandbox_env, sandbox_seed, request):
    env, local_app_data = sandbox_env

    if not PIL_AVAILABLE:
        warn_screenshot_unavailable("PIL 未安装")

    # Locate the executable
    project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    candidates = [
        os.path.join(project_root, "StarPie.Ui", "bin", config, tfm, "StarPie.exe")
        for config in ("Release", "Debug")
        for tfm in ("net10.0-windows10.0.19041.0", "net10.0-windows")
    ]
    app_path = next((c for c in candidates if os.path.exists(c)), None)
    if not app_path:
        pytest.fail(f"Executable not found in {candidates}. Please build the project first.")
        
    # Start the process with sandboxed environment variables.
    # 默认静默形态（--background：屏幕左上角 + 不可激活 + 点击穿透 + 不进任务栏，键鼠不被打扰）；
    # ONSCREEN（STARPIE_E2E_ONSCREEN=1，scripts/run-e2e.ps1 -OnScreen）时窗口正常显示，供调试。
    flags = ["--allow-multiple"]
    if not ONSCREEN:
        flags.append("--background")
    proc = subprocess.Popen([app_path, *flags], env=env)
    
    # 主窗口按 HWND 绑定（绕开标题正则的多元素歧义）；setup 阶段失败也要留窗口 dump 取证，
    # 而不是只报一句 "Failed to launch or connect"。
    try:
        pw_app = Application(backend="uia").connect(process=proc.pid, timeout=15)
        hwnd = find_main_window(proc.pid, timeout=15)
        win = Desktop(backend="uia").window(handle=hwnd)
        win.wait("visible", timeout=15)
    except Exception as ex:
        dump_process_windows(proc.pid, label="fixture setup 失败取证")
        try:
            proc.kill()
            proc.wait(timeout=2)
        except Exception:
            pass
        pytest.fail(f"Failed to launch or connect to application window: {type(ex).__name__}: {ex}")
        
    yield win, local_app_data
    
    # 失败取证：窗口 dump 覆盖 setup/call 两个阶段；截图在静默形态下即可用（窗口屏内被合成）。
    failed_phase = next(
        (
            phase
            for phase in ("setup", "call", "teardown")
            if getattr(getattr(request.node, f"rep_{phase}", None), "failed", False)
        ),
        None,
    )
    if failed_phase:
        dump_process_windows(proc.pid, label=f"失败取证（{failed_phase} 阶段）")
        reason = screenshot_unavailable_reason()
        if reason:
            warn_screenshot_unavailable(reason)
        else:
            artifacts_dir = os.path.join(project_root, "artifacts")
            os.makedirs(artifacts_dir, exist_ok=True)
            try:
                img = capture_window_image(win.handle)
                if img is None or is_blank_image(img):
                    warn_screenshot_unavailable("PrintWindow 未取到窗口内容")
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
