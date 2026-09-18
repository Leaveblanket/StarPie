import json
import os
import re
import shutil
import stat
import subprocess
import threading
import time
import warnings
import winreg
from ctypes import COMError

import pytest
import win32api
import win32gui
import win32process
import win32ui
from pywinauto import Application, Desktop
from pywinauto.findwindows import ElementNotFoundError

from catalogs import PROGRAM_PICKER_TITLE

# 失败截图依赖 PIL（依赖清单见 tests/requirements.txt）。应用以真实可见形态运行，
# PrintWindow 抓到的即当前真实画面；缺 PIL 时显式告警并把原因写进运行 header。
try:
    import PIL  # noqa: F401

    PIL_AVAILABLE = True
except ImportError:
    PIL_AVAILABLE = False

_screenshot_warned = False

# 仓库根（tests/ 的上一级）：被测 exe 定位与失败取证落盘共用同一来源。
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


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


def _messagebox_hwnd(pid: int) -> int:
    """被测进程当前可见的系统对话框（#32770）HWND；不存在时为 0。"""
    for row in _process_windows(pid):
        if row["class"] == "#32770" and row["visible"]:
            return row["hwnd"]
    return 0


def answer_messagebox(pid: int, button_id: str = "1", timeout: float = 10.0) -> None:
    """等待被测进程弹出的系统对话框并按按钮 ID 应答（1=确定、6=是、7=否；与界面语言无关）。

    真实形态下 Save 成功提示与插件卸载确认都是模态 MessageBox：它阻塞产品 UI 线程，
    不做真实应答会让后续 UIA 调用与退出编排一起挂住。
    """
    deadline = time.time() + timeout
    dialog = None
    while True:
        hwnd = _messagebox_hwnd(pid)
        if hwnd:
            dialog = Desktop(backend="uia").window(handle=hwnd)
            break
        if time.time() >= deadline:
            raise AssertionError(f"等待系统对话框超时（{timeout}s，pid={pid}）")
        time.sleep(0.1)

    # 系统 MessageBox 的按钮 AutomationId 即 Win32 控件 ID（1/2/6/7），与界面语言无关；
    # 个别风格不暴露 ID 时回退第一个按钮（YesNo 的默认按钮在首位）。
    button = dialog.child_window(auto_id=button_id, control_type="Button")
    if not button.exists(timeout=1.0):
        button = dialog.child_window(control_type="Button", found_index=0)
    button.invoke()
    wait_dialog_closed(dialog)


def dismiss_residual_messagebox(pid: int) -> None:
    """收尾兜底：被测进程仍有系统对话框挂着时按第一个按钮应答（尽力而为，不抛错）。

    漏应答的模态框会挡住退出编排（Dispatcher 回调排队但无人处理），使收尾回落硬杀、
    在 shell 通知区留下幽灵托盘图标。
    """
    try:
        hwnd = _messagebox_hwnd(pid)
        if not hwnd:
            return
        dialog = Desktop(backend="uia").window(handle=hwnd)
        button = dialog.child_window(control_type="Button", found_index=0)
        if button.exists(timeout=1.0):
            button.invoke()
    except Exception:
        pass


def click_and_answer(
    win,
    auto_id: str,
    button_id: str = "1",
    control_type: str = "Button",
    timeout: float = 15.0,
) -> None:
    """点击会弹系统对话框的控件，并在弹框出现时按 button_id 应答。

    应答线程先就位再 invoke：invoke 自身也可能被模态框挡在目标进程侧（UIA 跨进程调用要等
    目标处理完），两条阻塞链各自推进、不互相等待。
    """
    pid = win.process_id()
    outcome: dict = {}
    done = threading.Event()

    def _respond():
        try:
            answer_messagebox(pid, button_id, timeout)
        except Exception as ex:
            outcome["error"] = ex
        finally:
            done.set()

    worker = threading.Thread(target=_respond, daemon=True, name="e2e-messagebox")
    worker.start()
    win.child_window(auto_id=auto_id, control_type=control_type).invoke()
    assert done.wait(timeout + 5.0), f"等待对话框应答线程超时：{auto_id}"
    worker.join(timeout=1.0)
    if "error" in outcome:
        raise AssertionError(f"应答 {auto_id} 触发的系统对话框失败：{outcome['error']}") from None


def save_settings(win) -> None:
    """点 Save 并按掉真实成功提示框（模态 MessageBox）。"""
    click_and_answer(win, "SaveButton", button_id="1")


def click_and_confirm_yes(win, auto_id: str, control_type: str = "Button") -> None:
    """点击会弹确认框的控件并按「是」应答（破坏性动作的真实确认路径）。"""
    click_and_answer(win, auto_id, button_id="6", control_type=control_type)


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

    抓到的即窗口当前真实画面（真实可见形态下与用户所见一致）。
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


# 各页"就绪控件"清单：goto 已断言锚点，这里覆盖本页其余稳定控件（页面真实挂载即全部就位）。
# 只用被多个用例重复检查过的控件，避免把条件可见的控件写进来造成假红；
# 存在性用例（如 test_profile_management_ui_and_buttons）自带清单，不走这里。
PAGE_READY_CONTROLS = {
    0: (("ThresholdSlider", "Slider"), ("ThresholdValueLabel", "Text"),
        ("NewBlacklistProcessTextBox", "Edit"), ("BlacklistListBox", "List"),
        ("OuterEscapeDistanceSlider", "Slider")),
    1: (("AppThemeComboBox", "ComboBox"), ("WheelPaletteComboBox", "ComboBox"),
        ("WheelStyleComboBox", "ComboBox"), ("ShapeComboBox", "ComboBox"),
        ("ShowTextCheckBox", "CheckBox"), ("IconLayoutModeComboBox", "ComboBox"),
        ("WheelRadiusSlider", "Slider"), ("SectorGapSlider", "Slider"),
        ("SectorCornerRadiusSlider", "Slider")),
    2: (("ProfilesListBox", "List"), ("SectorActionListTitleText", "Text"),
        ("Slot0ActionTypeComboBox", "ComboBox")),
    3: (("LanguageComboBox", "ComboBox"), ("AutoStartCheckBox", "CheckBox"),
        ("ExportConfigButton", "Button"), ("ImportConfigButton", "Button")),
}


def assert_page_ready(win, slot: int, timeout: float = 3.0) -> None:
    """断言某页的稳定控件全部就位（整组共用 timeout 预算；失败一次列出缺失项）。"""
    deadline = time.time() + timeout
    while True:
        missing = tuple(
            auto_id
            for auto_id, ctype in PAGE_READY_CONTROLS[slot]
            if not win.child_window(auto_id=auto_id, control_type=ctype).exists(timeout=0.2)
        )
        if not missing:
            return
        if time.time() >= deadline:
            raise AssertionError(f"NavPage{slot} 缺少控件: {missing}")
        time.sleep(0.1)


def wait_until(predicate, timeout: float = 5.0, interval: float = 0.1, description: str = "条件成立"):
    """轮询 predicate 直到返回真值；超时抛带最后取值的断言（替代固定 sleep）。

    predicate 抛出的异常按"条件尚未成立"处理——异常文本只进超时诊断，**不得当作成立返回**：
    断言型 predicate（如 `text_of` 的"控件必须存在"）一抛异常就被吞成通过，会让整条用例
    在目标不存在时静默变绿。
    """
    deadline = time.time() + timeout
    last = None
    while True:
        try:
            last = predicate()
        except Exception as ex:
            last = f"{type(ex).__name__}: {ex}"
        else:
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


def read_json_file(path: str, predicate=None, timeout: float = 5.0, message: str = ""):
    """轮询读取 JSON 文件：等文件出现、可选等 predicate 成立；到超时仍未成立即失败。

    替代"固定 sleep 后直接 open/json.load"——落盘稍慢时不再读到旧值或抛 FileNotFoundError。
    等待与断言收在一处：predicate 即本次要验的条件（用例不必读出文件后再把同一条件断言一遍），
    message 给业务文案——超时报告 = 业务文案 + 最后内容，条件只写一处且可诊断。
    """
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
            last_err = f"文件不存在: {path}"
        except json.JSONDecodeError as ex:
            last_err = f"JSON 解析失败: {ex}"
        if time.time() >= deadline:
            break
        time.sleep(0.1)
    what = message or f"等待 {os.path.basename(path)} 的目标状态"
    raise AssertionError(f"{what}（{timeout}s 内未成立）。{last_err} 最后内容: {last}")


def read_config(local_app_data, predicate=None, timeout: float = 5.0, message: str = ""):
    """轮询读取沙盒 config.json；predicate 成立即返回（到超时仍未成立即失败）。"""
    return read_json_file(
        os.path.join(str(local_app_data), "StarPie", "config.json"), predicate, timeout, message
    )


def read_plugin_state(local_app_data, predicate=None, timeout: float = 5.0, message: str = ""):
    """轮询读取沙盒宿主状态文件 plugin-state.json（启停 / 隔离 / 挂起版本等权威意图）。"""
    return read_json_file(
        os.path.join(str(local_app_data), "StarPie", "plugin-state.json"), predicate, timeout, message
    )


def probe_exe_from_config(local_app_data) -> str:
    """从沙箱配置读回预置的探针 exe 路径（gesture-probe 预置的 Global 扇区 0 Launch 参数）。

    动作执行类用例的落地证据按镜像路径判定，路径来源只此一处（预置见 seed_gesture_config）。
    """
    return read_config(local_app_data)["Profiles"][0]["Actions"][0]["Parameter"]


def global_action_type_is(config: dict, expected: str) -> bool:
    """配置里 Global 方案首个动作的类型是否为 expected。

    槽位动作类型下拉的 UIA 不暴露选中态时，配置里该字段是唯一观察面——
    本 predicate 供落盘断言复用（条件只写一处）。
    """
    glob = next((p for p in config.get("Profiles", []) if p.get("ProcessName") == "Global"), None)
    actions = (glob or {}).get("Actions") or []
    return bool(actions) and actions[0].get("Type") == expected


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

    产品对话框是 owned 窗口，pywinauto 的进程/桌面枚举不一定包含它，但 win32 枚举可见；
    拿到 HWND 后经 handle 绑定，child_window/invoke 与常规 WindowSpecification 一致。
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


def close_console(win, timeout: float = 8.0) -> None:
    """点 CloseButton 关闭设置台（关闭即销毁、托盘驻留），等窗口句柄失效。

    设置台关闭走真实关闭序列（导航出账 + 冲刷挂起落盘 + 托盘驻留），
    窗口销毁是"关闭即销毁"语义的观察面；重开走托盘双击/恢复消息（用例各自驱动）。
    """
    handle = win.handle
    win.child_window(auto_id="CloseButton", control_type="Button").invoke()
    wait_until(
        lambda: not win32gui.IsWindow(handle),
        timeout=timeout,
        description="设置台窗口已销毁（关闭即销毁、托盘驻留）",
    )


def open_program_picker(win, nav_slot: int = 2):
    """从手势页（NavPage2）经 AddProfileButton 打开程序选择器（真实模态对话框）。"""
    goto(win, nav_slot)
    add_btn = win.child_window(auto_id="AddProfileButton", control_type="Button")
    assert add_btn.exists(timeout=3), "AddProfileButton 必须存在"
    add_btn.invoke()
    return wait_dialog(PROGRAM_PICKER_TITLE)


def picker_filter(picker, text: str) -> None:
    """在程序选择器搜索框输入过滤词（ListView 虚拟化，只有过滤后目标项才被实例化）。"""
    search = picker.child_window(auto_id="SearchTextBox", control_type="Edit")
    assert search.exists(timeout=5), "程序选择器搜索框必须存在"
    search.set_edit_text(text)


def picker_programs(picker, timeout: float = 30.0):
    """等选择器列表首次填充出条目（扫描 + 逐条图标提取完成后才填充），返回列表控件。"""
    programs = picker.child_window(auto_id="ProgramsListView", control_type="List")
    wait_until(
        lambda: list_item_texts(programs) != [],
        timeout=timeout,
        description="程序选择器列表填充出条目",
    )
    return programs


def pick_program(picker, filter_text: str) -> None:
    """在程序选择器里过滤、选中目标条目并确认真实关闭（选中路径的统一实现）。"""
    picker_filter(picker, filter_text)
    programs = picker.child_window(auto_id="ProgramsListView", control_type="List")
    wait_until(
        lambda: any(filter_text in text for text in list_item_texts(programs)),
        timeout=30.0,
        description=f"程序选择器列出 {filter_text}",
    )
    for item in programs.children(control_type="ListItem"):
        if filter_text in item.window_text():
            item.select()
            break
    else:
        raise AssertionError(f"程序选择器没有可选的 {filter_text} 条目")

    picker.child_window(auto_id="OkButton", control_type="Button").invoke()
    wait_dialog_closed(picker)


def cancel_dialog(dialog) -> None:
    """点 CancelButton 关闭对话框并等关闭（取消路径的统一收尾）。"""
    dialog.child_window(auto_id="CancelButton", control_type="Button").invoke()
    wait_dialog_closed(dialog)


def find_app_path() -> str:
    """定位被测 exe（与运行器/CI 同一构建输出顺序）；找不到时报出全部候选路径。

    app fixture 与沙箱预置（如往用户插件目录放真实入口程序集副本）共用同一份定位逻辑，
    避免测试侧复算路径时与 fixture 漂移。
    """
    project_root = PROJECT_ROOT
    candidates = [
        os.path.join(project_root, "StarPie.Ui", "bin", config, tfm, "StarPie.exe")
        for config in ("Release", "Debug")
        for tfm in ("net10.0-windows10.0.19041.0", "net10.0-windows")
    ]
    for candidate in candidates:
        if os.path.exists(candidate):
            return candidate
    pytest.fail(f"Executable not found in {candidates}. Please build the project first.")


# 常驻托盘消息窗口的标题（进程存活期内恒在的常驻 HWND）与测试实例退出消息名
# （两端各自注册同一名称字符串，产品侧见 StarPie.Ui/TestInstanceExit.cs）。
TRAY_WINDOW_TITLE = "StarPieTrayWindow"
TEST_INSTANCE_EXIT_MESSAGE = "StarPie_TestInstance_Exit"

# 托盘菜单窗口标题与托盘回调消息（产品侧见 StarPie.Ui/Services/Shell/TrayIconManager.cs）：
# 菜单每次打开新建窗口，用例按标题定位；回调消息经 PostMessage 投递即等价于右键点托盘图标。
TRAY_MENU_WINDOW_TITLE = "StarPieTrayMenu"
TRAY_CALLBACK_MESSAGE = 0x8001  # WM_APP + 1
WM_RBUTTONUP = 0x0205
WM_LBUTTONDBLCLK = 0x0203

# 轮盘窗口标题（产品侧 StarPie.Ui/Views/Wheel/RadialWindow.xaml）：每次手势一个实例，关闭即销毁。
WHEEL_WINDOW_TITLE = "RadialWindow"

# 程序选择器/动作执行用例共用的探针程序：HKCU App Paths 注册的"记事本副本"——
# 只有插件的深扫来源（注册表 App Paths）会发现它，启用/停用两态由此可观察；
# 副本本身又是 Launch 动作的落地目标（动作执行证据只看进程，不看界面）。
PROBE_PROGRAM_NAME = "starpie-e2e-probe"
APP_PATHS_KEY = r"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"
PROBE_PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def plant_probe_executable() -> str:
    """在仓库 artifacts 落一份 cmd.exe 副本（不写注册表），返回 exe 路径。

    副本要能被 Launch 动作真的启动并作为独立进程存活（动作执行证据按镜像路径判定）：
    Win11 的 notepad.exe 是应用包入口（副本起不来自己的进程），cmd.exe 副本可稳定启动。
    """
    probe_dir = os.path.join(PROBE_PROJECT_ROOT, "artifacts", "e2e", "probe")
    os.makedirs(probe_dir, exist_ok=True)
    probe_exe = os.path.join(probe_dir, f"{PROBE_PROGRAM_NAME}.exe")
    system_root = os.environ.get("SystemRoot", r"C:\Windows")
    shutil.copyfile(os.path.join(system_root, "System32", "cmd.exe"), probe_exe)
    return probe_exe


def plant_probe_program() -> str:
    """注册一个只由插件深扫来源（App Paths）发现的程序，返回探针 exe 路径。"""
    probe_exe = plant_probe_executable()
    with winreg.CreateKey(
        winreg.HKEY_CURRENT_USER, f"{APP_PATHS_KEY}\\{PROBE_PROGRAM_NAME}.exe"
    ) as key:
        winreg.SetValueEx(key, "", 0, winreg.REG_SZ, probe_exe)
    return probe_exe


def remove_probe_program() -> None:
    """撤销探针注册（用例结束必调，避免污染真实用户注册表）。"""
    try:
        winreg.DeleteKey(winreg.HKEY_CURRENT_USER, f"{APP_PATHS_KEY}\\{PROBE_PROGRAM_NAME}.exe")
    except FileNotFoundError:
        pass


@pytest.fixture(scope="function")
def probe_program():
    """预置 App Paths 探针程序；用例结束撤销注册（真实注册表的副作用必须收口）。"""
    path = plant_probe_program()
    try:
        yield path
    finally:
        remove_probe_program()


def find_wheel_window(pid: int) -> int:
    """被测进程当前的轮盘窗口 HWND；不存在时为 0（关闭即销毁，不跨手势复用）。"""
    for row in _process_windows(pid):
        if row["visible"] and row["title"] == WHEEL_WINDOW_TITLE:
            return row["hwnd"]
    return 0


def find_process_by_executable(exe_path: str) -> list:
    """按可执行文件全路径查进程 pid 列表（Launch 动作"真的起了进程"的落地证据）。

    逐进程取镜像路径需要查询权限：打不开/受保护进程跳过（不静默吞掉目标进程——
    目标是我们自己启动的普通进程，查询一定成功）。
    """
    target = os.path.normcase(os.path.abspath(exe_path))
    found = []
    for pid in win32process.EnumProcesses():
        if pid <= 4:
            continue
        handle = None
        try:
            handle = win32api.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
            image = win32process.GetModuleFileNameEx(handle, 0)
        except Exception:
            continue
        finally:
            if handle:
                win32api.CloseHandle(handle)
        if image and os.path.normcase(os.path.abspath(image)) == target:
            found.append(pid)
    return found


def wait_process_started(exe_path: str, timeout: float = 10.0) -> list:
    """轮询等待目标 exe 的进程出现，返回其 pid 列表；超时抛断言。"""
    deadline = time.time() + timeout
    while True:
        pids = find_process_by_executable(exe_path)
        if pids:
            return pids
        if time.time() >= deadline:
            raise AssertionError(f"等待进程启动超时（{timeout}s）：{exe_path}")
        time.sleep(0.2)


def kill_processes(pids) -> None:
    """杀掉用例自己拉起的探针进程（不留后台残留）。"""
    for pid in pids or []:
        try:
            handle = win32api.OpenProcess(0x0001, False, pid)  # PROCESS_TERMINATE
            if handle:
                win32process.TerminateProcess(handle, 0)
                win32api.CloseHandle(handle)
        except Exception:
            pass


def find_tray_window(pid: int) -> int:
    """被测进程的常驻托盘消息窗口 HWND；不存在时为 0。

    该窗口是常驻 HWND（进程存活期内恒在），也是测试实例退出消息的接收端——设置台是瞬态窗口，
    关闭后不存在，不能作定位面。
    """
    hwnd = win32gui.FindWindow(None, TRAY_WINDOW_TITLE)
    if hwnd and win32process.GetWindowThreadProcessId(hwnd)[1] == pid:
        return hwnd
    return 0


def exit_via_test_message(pid: int, timeout: float = 15.0) -> None:
    """向常驻托盘窗口投递测试实例退出消息并等进程退出（真实退出编排的入口）。

    与 shutdown_app 的差别：只持有 pid、不持有 Popen 句柄，供用例在 fixture 之外
    驱动退出（如"退出时兜底落盘"类断言要在进程消失后读盘）。
    """
    tray = find_tray_window(pid)
    assert tray, f"常驻托盘消息窗口必须存在（标题 {TRAY_WINDOW_TITLE}）"
    message = win32gui.RegisterWindowMessage(TEST_INSTANCE_EXIT_MESSAGE)
    win32gui.PostMessage(tray, message, 0, 0)
    deadline = time.time() + timeout
    while time.time() < deadline:
        if pid not in win32process.EnumProcesses():
            return
        time.sleep(0.1)
    raise AssertionError(f"退出消息未被受理：{timeout}s 内进程未退出（pid={pid}）")


def shutdown_app(proc, timeout: float = 15.0) -> bool:
    """请求被测应用退出，返回是否走成了优雅退出。

    优雅退出（投递测试实例退出消息 → 常驻壳层按 ShellExitSequence 落盘/释放托盘/关应用）是唯一
    执行 `NIM_DELETE` 的路径。硬杀（`TerminateProcess`）不跑用户态收尾，托盘图标会以宿主窗口已
    失效的死条目留在 shell 通知区（幽灵托盘图标），累积到用户托盘里，故硬杀只作超时兜底并告警。
    """
    if proc.poll() is not None:
        return True

    tray = find_tray_window(proc.pid)
    if tray:
        message = win32gui.RegisterWindowMessage(TEST_INSTANCE_EXIT_MESSAGE)
        win32gui.PostMessage(tray, message, 0, 0)
        deadline = time.time() + timeout
        while time.time() < deadline:
            if proc.poll() is not None:
                return True
            time.sleep(0.1)
        warnings.warn(
            f"被测应用未在 {timeout}s 内按测试实例退出消息退出，回落硬杀——"
            "shell 通知区会留下幽灵托盘图标，检查常驻壳层的退出消息受理",
            stacklevel=2,
        )

    # 无托盘窗口（进程未起到注册图标那一步）时硬杀不留残留，无需告警。
    try:
        proc.kill()
        proc.wait(timeout=2)
    except Exception:
        pass
    return False


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


def _seed_plugin_state(local_app_data, plugins, developer_mode=False):
    """预置沙箱宿主状态文件：启动扫描前的权威意图（启停/隔离/开发者模式）。"""
    state_dir = local_app_data / "StarPie"
    state_dir.mkdir(parents=True, exist_ok=True)
    (state_dir / "plugin-state.json").write_text(
        json.dumps(
            {
                "SchemaVersion": 1,
                "DeveloperModeEnabled": developer_mode,
                "Plugins": plugins,
            },
            ensure_ascii=False,
        ),
        encoding="utf-8",
    )


def write_sandbox_config(local_app_data, config: dict) -> str:
    """把一份 config.json 预置进沙箱（应用启动前写入即"上次运行留下的配置"）。

    宽松反序列化下缺失键取模型默认值，故只需写本次用例关心的键。
    """
    state_dir = local_app_data / "StarPie"
    state_dir.mkdir(parents=True, exist_ok=True)
    path = state_dir / "config.json"
    path.write_text(json.dumps(config, ensure_ascii=False, indent=2), encoding="utf-8")
    return str(path)


def seed_gesture_config(local_app_data, probe_exe: str) -> None:
    """手势链路用例的配置：Global 4 扇区，仅扇区 0（正右）是探针 exe 的 Launch 动作，
    其余扇区为空动作（空 Type 在松开时按取消处理，不会误触发别的动作）。"""
    write_sandbox_config(
        local_app_data,
        {
            "Language": "zh-CN",
            "DragThreshold": 25,
            "EnableOuterEscapeCancel": True,
            "OuterEscapeDistance": 186,
            "BlacklistedProcesses": [],
            "Profiles": [
                {
                    "ProcessName": "Global",
                    "SectorCount": 4,
                    "Actions": [
                        {
                            "Type": "Launch",
                            "Name": "e2e 探针",
                            "Parameter": probe_exe,
                            # 保活参数：探针进程要活到用例观察到它（收尾由用例杀进程）。
                            "Arguments": "/c ping -n 15 127.0.0.1",
                            "IconKey": "Code",
                        },
                        {"Type": "", "Name": "", "Parameter": "", "IconKey": ""},
                        {"Type": "", "Name": "", "Parameter": "", "IconKey": ""},
                        {"Type": "", "Name": "", "Parameter": "", "IconKey": ""},
                    ],
                }
            ],
        },
    )


@pytest.fixture(scope="function")
def sandbox_seed(request, sandbox_env):
    """
    沙箱预置钩子：在应用启动前向沙箱写入文件，默认不写任何东西。

    用例经 `@pytest.mark.parametrize("sandbox_seed", [...], indirect=True)` 取用某个预置形态，
    只影响该用例的启动环境（如"停用内置程序来源插件"的宿主状态、预置 config.json）。
    """
    env, local_app_data = sandbox_env
    mode = getattr(request, "param", None)
    if mode == "disabled-program-source":
        _seed_plugin_state(local_app_data, {"starpie.builtin.program-source": {"Enabled": False}})
    elif mode == "gesture-probe":
        # 手势链路用例：探针 exe 作 Launch 目标（只落文件，不写注册表）。
        seed_gesture_config(local_app_data, plant_probe_executable())
    elif mode == "corrupt-config":
        # 损坏配置的降级路径：文件保留损坏内容，应用须照常可用（回退默认，不触碰文件）。
        state_dir = local_app_data / "StarPie"
        state_dir.mkdir(parents=True, exist_ok=True)
        (state_dir / "config.json").write_text("{ 这不是合法 JSON ", encoding="utf-8")
    elif mode == "partial-config":
        # 旧配置缺键：宽松反序列化下缺失键取模型默认值（只写关心的两键）。
        write_sandbox_config(local_app_data, {"Language": "zh-CN", "DragThreshold": 33})
    elif mode == "broken-user-plugin":
        # 开发者模式 + 清单损坏的用户包：发现阶段拒绝它，但不得影响启动与其它插件。
        _seed_plugin_state(local_app_data, {}, developer_mode=True)
        package_dir = local_app_data / "StarPie" / "plugins" / "e2e.broken.probe"
        package_dir.mkdir(parents=True)
        (package_dir / "plugin.json").write_text("{ 坏清单 ", encoding="utf-8")
    elif mode == "readonly-config":
        # 只读配置：合法内容可读可加载，落盘失败不得让应用崩溃或卡死。
        config_path = write_sandbox_config(
            local_app_data, {"Language": "zh-CN", "DragThreshold": 30}
        )
        os.chmod(config_path, stat.S_IREAD)
    elif mode == "disabled-sample-ui":
        _seed_plugin_state(local_app_data, {"starpie.builtin.sample-ui": {"Enabled": False}})
    elif mode == "developer-user-plugin":
        # 开发者模式 + 用户插件目录预置一个已停用的可发现包：清单复用内置程序来源插件的入口
        # 程序集副本（副本不在宿主/SDK 禁带名单内）。停用预置让「彻底移除」打在未装载的包上——
        # WPF 宿主里活动插件的程序集锁到进程退出（回收只降级为诊断），包目录删不掉是记录在案语义。
        _seed_plugin_state(
            local_app_data,
            {"e2e.user.probe": {"Enabled": False}},
            developer_mode=True,
        )
        state_dir = local_app_data / "StarPie"
        package_dir = state_dir / "plugins" / "e2e.user.probe"
        package_dir.mkdir(parents=True)
        install_plugins_dir = os.path.join(os.path.dirname(find_app_path()), "plugins")
        shutil.copyfile(
            # 内置程序来源包里的入口程序集副本（包子目录布局，不在宿主/SDK 禁带名单内）。
            os.path.join(install_plugins_dir, "starpie.builtin.program-source", "StarPie.Plugin.Programs.dll"),
            package_dir / "StarPie.Plugin.Programs.dll",
        )
        (package_dir / "plugin.json").write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "id": "e2e.user.probe",
                    "name": "e2e 用户探针",
                    "version": "1.0.0",
                    "sdk": "1.0",
                    "entryAssembly": "StarPie.Plugin.Programs.dll",
                    "entryType": "StarPie.Plugin.Programs.ProgramSourcePlugin",
                    "capabilities": [{"id": "program-source", "abi": 1}],
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
    elif mode == "quarantined-program-source":
        _seed_plugin_state(
            local_app_data,
            {
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
        )
    return mode


def start_app(env, timeout: float = 15.0, trigger_button: int | None = None):
    """按沙箱环境启动被测应用并绑定主窗口，返回 (proc, win)。

    与 app fixture 走同一条启动/取窗口路径；供"同一沙箱内重启"类用例在用例体里
    自己管理第二个进程（进程收尾用 stop_app）。

    trigger_button 非空时追加 `--trigger-button=<n>`：把触发键换成 SharpHook MouseButton
    的第 n 个按键（4/5 即鼠标侧键 XBUTTON1/XBUTTON2）。侧键未被抑制时不弹上下文菜单，
    手势链路的外部观测不必先收菜单——注入面见 tests/mouse_input.py 的 side_down/side_up。
    """

    app_path = find_app_path()
    # 真实可见形态启动（--allow-multiple：绕过单实例闸门、受理测试实例退出消息）：
    # 窗口真实呈现、对话框真实弹出、托盘序列真实执行；运行期间请勿操作键鼠（全局钩子在跑）。
    command = [app_path, "--allow-multiple"]
    if trigger_button is not None:
        command.append(f"--trigger-button={trigger_button}")
    proc = subprocess.Popen(command, env=env)

    # 主窗口按 HWND 绑定（绕开标题正则的多元素歧义）；启动阶段失败也要留窗口 dump 取证，
    # 而不是只报一句 "Failed to launch or connect"。
    try:
        Application(backend="uia").connect(process=proc.pid, timeout=timeout)
        hwnd = find_main_window(proc.pid, timeout=timeout)
        win = Desktop(backend="uia").window(handle=hwnd)
        win.wait("visible", timeout=timeout)
    except Exception as ex:
        dump_process_windows(proc.pid, label="应用启动失败取证")
        shutdown_app(proc, timeout=5.0)
        pytest.fail(f"Failed to launch or connect to application window: {type(ex).__name__}: {ex}")
    return proc, win


def stop_app(proc, timeout: float = 15.0) -> bool:
    """用例内自管进程的收尾：漏应答的模态框先按掉，再走真实退出路径（幂等）。"""
    dismiss_residual_messagebox(proc.pid)
    return shutdown_app(proc, timeout)


@pytest.fixture(scope="function")
def trigger_button(request):
    """启动期触发键（SharpHook MouseButton 编号）：默认 None 即产品默认右键。

    用例经 `@pytest.mark.parametrize("trigger_button", [4], indirect=True)` 取用（与
    sandbox_seed 同一模式）。4/5 是鼠标侧键（XBUTTON1/XBUTTON2）：侧键未被抑制时不弹
    上下文菜单，手势链路的外部观测不必先收菜单——本 fixture 只决定启动参数，
    注入面须同步用侧键（tests/mouse_input.py 的 side_down/side_up）。
    """
    return getattr(request, "param", None)


@pytest.fixture(scope="function")
def app(sandbox_env, sandbox_seed, trigger_button, request):
    env, local_app_data = sandbox_env

    if not PIL_AVAILABLE:
        warn_screenshot_unavailable("PIL 未安装")

    proc, win = start_app(env, trigger_button=trigger_button)

    yield win, local_app_data
    
    # 失败取证：窗口 dump 覆盖 setup/call 两个阶段；截图抓窗口当前真实画面。
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
            artifacts_dir = os.path.join(PROJECT_ROOT, "artifacts")
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
            
    # 收尾兜底：漏应答的模态框会挡住退出编排，先按掉再走真实退出路径
    #（硬杀会在 shell 通知区留下幽灵托盘图标）
    stop_app(proc)

@pytest.hookimpl(tryfirst=True, hookwrapper=True)
def pytest_runtest_makereport(item, call):
    outcome = yield
    setattr(item, f"rep_{outcome.get_result().when}", outcome.get_result())
