"""降级路径与运行期观察 e2e。

降级：坏输入（损坏清单的用户插件、只读配置）不得让应用起不来或卡死——坏件被拒/落盘
失败后，其余功能仍可用。观察：设置台关闭后的内存/句柄回落（关闭序列里含内存整理步骤），
以及"关闭后仍能完成一次手势"这条壳层仍活着的证据。
"""

import ctypes
import os
import stat
import warnings

import pytest
import win32api
import win32process
from catalogs import BROKEN_PLUGIN_ID, PROGRAM_SOURCE_PLUGIN_ID
from conftest import (
    assert_text_contains,
    close_console,
    exit_via_test_message,
    find_wheel_window,
    goto,
    kill_processes,
    probe_exe_from_config,
    read_config,
    save_settings,
    wait_for_label_value,
    wait_process_started,
    wait_until,
)
from mouse_input import drag_right, press_right_at, release_right

PLUGIN_STATUS = f"PluginManagerStatus_{PROGRAM_SOURCE_PLUGIN_ID}"

START = (600, 400)
SECTOR_DRAG = 110


_KERNEL32 = ctypes.WinDLL("kernel32", use_last_error=True)


def _handle_count(process_handle) -> int:
    """进程句柄数（pywin32 没有现成封装，走 kernel32.GetProcessHandleCount）。"""
    count = ctypes.c_ulong(0)
    if not _KERNEL32.GetProcessHandleCount(int(process_handle), ctypes.byref(count)):
        return -1
    return count.value


def _process_stats(pid: int) -> dict:
    """取进程的内存/句柄快照（观察性指标，不做严格等值断言）。"""
    handle = win32api.OpenProcess(0x0400 | 0x0010, False, pid)  # QUERY_INFORMATION | VM_READ
    assert handle, f"无法打开进程 pid={pid}"
    try:
        counters = win32process.GetProcessMemoryInfo(handle)
        return {"working_set": counters["WorkingSetSize"], "handles": _handle_count(handle)}
    finally:
        win32api.CloseHandle(handle)


@pytest.mark.parametrize("sandbox_seed", ["broken-user-plugin"], indirect=True)
def test_broken_plugin_manifest_does_not_break_startup(app):
    """清单损坏的用户包：不装载、不影响启动，内置插件照常活动。"""
    win, _ = app
    goto(win, 4)

    assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")

    status = win.child_window(auto_id=f"PluginManagerStatus_{BROKEN_PLUGIN_ID}", control_type="Text")
    if status.exists(timeout=1.0):
        text = status.window_text()
        assert "活动" not in text, f"坏清单的包不得判为活动: {text!r}"

    # 设置台其余页面仍可用（坏件只波及自己）
    goto(win, 0)
    goto(win, 4)


@pytest.mark.parametrize("sandbox_seed", ["readonly-config"], indirect=True)
def test_readonly_config_degrades_without_crash(app):
    """只读 config.json：加载照常，落盘失败不崩不卡；磁盘保持原值。"""
    win, local_app_data = app
    config_path = os.path.join(str(local_app_data), "StarPie", "config.json")

    try:
        goto(win, 0)
        wait_for_label_value(win, "ThresholdValueLabel", 30.0)  # 预置值可加载

        slider = win.child_window(auto_id="ThresholdSlider", control_type="Slider")
        slider.set_value(60.0)
        wait_for_label_value(win, "ThresholdValueLabel", 60.0)
        save_settings(win)  # 落盘必然失败，但提示框与事件链必须走完

        # 应用仍可用：导航往返 + 磁盘保持只读前的原值
        goto(win, 3)
        goto(win, 0)
        read_config(
            local_app_data,
            predicate=lambda c: abs(c.get("DragThreshold", 0) - 30) < 0.01,
            message="只读文件不得被改写（DragThreshold 应保持 30）",
        )
    finally:
        try:
            os.chmod(config_path, stat.S_IWRITE | stat.S_IREAD)
        except OSError:
            pass


@pytest.mark.parametrize("sandbox_seed", ["gesture-probe"], indirect=True)
def test_residency_after_console_close_and_gesture_still_works(app):
    """观察：关闭设置台后内存/句柄不失控，且壳层仍能完成一次手势（钩子与编排都活着）。

    阈值放宽为"不失控"而非"必须下降"：工作集回收受 CLR/系统影响，严格下降会假红。
    """
    win, local_app_data = app
    pid = win.process_id()

    before = _process_stats(pid)

    close_console(win)

    after = _process_stats(pid)
    growth_mb = (after["working_set"] - before["working_set"]) / 1024 / 1024
    warnings.warn(
        "关闭设置台前后：working_set {0:.1f}MB -> {1:.1f}MB（{2:+.1f}MB）；handles {3} -> {4}".format(
            before["working_set"] / 1024 / 1024,
            after["working_set"] / 1024 / 1024,
            growth_mb,
            before["handles"],
            after["handles"],
        ),
        stacklevel=2,
    )
    if growth_mb > 30 or after["handles"] > before["handles"] + 50:
        warnings.warn("设置台关闭后回收偏弱（超过软阈值），检查关闭序列的回收步骤", stacklevel=2)
    assert after["working_set"] <= before["working_set"] + 100 * 1024 * 1024, (
        f"关闭后工作集异常膨胀: {before} -> {after}"
    )
    assert after["handles"] <= before["handles"] + 200, f"关闭后句柄异常膨胀: {before} -> {after}"

    # 壳层仍活着：设置台关着也能完成一次手势并执行动作
    probe_exe = probe_exe_from_config(local_app_data)
    press_right_at(*START)
    drag_right(SECTOR_DRAG)
    wait_until(lambda: find_wheel_window(pid), timeout=3.0, description="关闭设置台后轮盘仍能弹出")
    release_right()
    wait_until(
        lambda: find_wheel_window(pid) == 0,
        timeout=3.0,
        description="松手后轮盘收起",
    )
    pids = wait_process_started(probe_exe, timeout=10.0)
    kill_processes(pids)
    exit_via_test_message(pid)
