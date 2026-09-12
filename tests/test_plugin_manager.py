"""插件最小管理面 e2e：列表/状态、经产品路径的启停往返、隔离态重试与诊断入口。"""

import json
import os
import time

import pytest
from conftest import assert_text_contains, goto, text_of

PLUGIN_ID = "starpie.builtin.program-source"
PLUGIN_STATUS = f"PluginManagerStatus_{PLUGIN_ID}"
PLUGIN_TOGGLE = f"PluginManagerToggle_{PLUGIN_ID}"
PLUGIN_RETRY = f"PluginManagerRetry_{PLUGIN_ID}"
PLUGIN_DIAGNOSTICS = f"PluginManagerDiagnostics_{PLUGIN_ID}"


def _read_plugin_state(local_app_data, predicate, timeout=5.0):
    """轮询读取沙箱 plugin-state.json，直到 predicate 成立（宿主落盘稍慢时不误判）。"""
    path = os.path.join(str(local_app_data), "StarPie", "plugin-state.json")
    deadline = time.time() + timeout
    last = None
    while True:
        try:
            with open(path, "r", encoding="utf-8") as f:
                last = json.load(f)
            if predicate(last):
                return last
        except (FileNotFoundError, json.JSONDecodeError):
            last = None
        if time.time() >= deadline:
            break
        time.sleep(0.1)
    raise AssertionError(f"等待 plugin-state.json 超时（{timeout}s）。最后内容: {last}")


def test_plugin_manager_lists_builtin_plugin(app):
    """管理页列出内置插件：状态为活动，诊断面板给出报告正文。"""
    win, _ = app
    goto(win, 4)

    name = text_of(win, f"PluginManagerName_{PLUGIN_ID}", "Text", timeout=5.0)
    assert name, "插件条目必须有展示名"
    assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")

    win.child_window(auto_id=PLUGIN_DIAGNOSTICS, control_type="Button").invoke()

    diagnostics = assert_text_contains(win, "PluginDiagnosticsText", "Edit", "内置", timeout=5.0)
    assert PLUGIN_ID in diagnostics


def test_plugin_manager_toggle_roundtrip(app):
    """经产品路径停用/启用：界面状态与宿主状态文件同步变化。"""
    win, local_app_data = app
    goto(win, 4)
    assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")

    win.child_window(auto_id=PLUGIN_TOGGLE, control_type="Button").invoke()

    assert_text_contains(win, PLUGIN_STATUS, "Text", "已停用")
    _read_plugin_state(
        local_app_data,
        lambda state: state["Plugins"][PLUGIN_ID]["Enabled"] is False,
    )

    # 停用后的诊断面板给出降级回收说明：WPF 宿主里程序集留到重启释放，不谎报完全回收。
    win.child_window(auto_id=PLUGIN_DIAGNOSTICS, control_type="Button").invoke()
    diagnostics = text_of(win, "PluginDiagnosticsText", "Edit", timeout=3.0)
    assert "重启宿主后释放" in diagnostics

    win.child_window(auto_id=PLUGIN_TOGGLE, control_type="Button").invoke()

    assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")
    _read_plugin_state(
        local_app_data,
        lambda state: state["Plugins"][PLUGIN_ID]["Enabled"] is True,
    )


@pytest.mark.parametrize("sandbox_seed", ["quarantined-program-source"], indirect=True)
def test_plugin_manager_retry_recovers_quarantined_plugin(app):
    """隔离态：管理页展示隔离原因与残留清单，显式重试后恢复活动。"""
    win, _ = app
    goto(win, 4)

    assert_text_contains(win, PLUGIN_STATUS, "Text", "已隔离")
    win.child_window(auto_id=PLUGIN_DIAGNOSTICS, control_type="Button").invoke()
    diagnostics = text_of(win, "PluginDiagnosticsText", "Edit", timeout=3.0)
    assert "e2e 预置隔离" in diagnostics
    assert "入口程序集" in diagnostics

    # 隔离态另有停用入口：显式放弃隔离、落停用意图（重试是另一条独立入口）。
    assert win.child_window(auto_id=PLUGIN_TOGGLE, control_type="Button").exists(timeout=3.0), (
        "隔离态必须提供停用入口"
    )

    retry = win.child_window(auto_id=PLUGIN_RETRY, control_type="Button")
    assert retry.exists(timeout=3.0), "隔离态必须提供重试入口"
    retry.invoke()

    assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")
