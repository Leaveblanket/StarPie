"""插件最小管理面 e2e：列表/状态、经产品路径的启停往返、隔离态重试与诊断入口。"""

import json
import os
import re
import time

import pytest
from conftest import assert_text_contains, goto, text_of, wait_until

PLUGIN_ID = "starpie.builtin.program-source"
PLUGIN_STATUS = f"PluginManagerStatus_{PLUGIN_ID}"
PLUGIN_TOGGLE = f"PluginManagerToggle_{PLUGIN_ID}"
PLUGIN_RETRY = f"PluginManagerRetry_{PLUGIN_ID}"
PLUGIN_DIAGNOSTICS = f"PluginManagerDiagnostics_{PLUGIN_ID}"

SAMPLE_UI_ID = "starpie.builtin.sample-ui"
SAMPLE_UI_STATUS = f"PluginManagerStatus_{SAMPLE_UI_ID}"
SAMPLE_UI_NAV = f"NavPlugin_{SAMPLE_UI_ID}"
SAMPLE_UI_UPDATE = f"PluginManagerUpdate_{SAMPLE_UI_ID}"
SAMPLE_UI_SETTINGS_SECTION = "SampleUiSettingsGreeting"

USER_PLUGIN_ID = "e2e.user.probe"


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


def _wait_absent(win, auto_id: str, control_type: str, timeout: float = 8.0) -> None:
    """轮询等待控件从 UIA 树消失（动态注册面摘除是异步收尾，不能只做一次 exists）。"""
    wait_until(
        lambda: not win.child_window(auto_id=auto_id, control_type=control_type).exists(timeout=0.3),
        timeout=timeout,
        description=f"{auto_id} 离场",
    )


def test_plugin_page_navigation_and_interaction(app):
    """插件页经 NavPlugin_<id> 可达：页面锚点、宿主定时器心跳与页面绑定交互。"""
    win, _ = app

    radio = win.child_window(auto_id=SAMPLE_UI_NAV, control_type="RadioButton")
    assert radio.exists(timeout=5.0), "启动装载的 UI 插件必须注册侧边栏导航项"
    radio.select()

    header = win.child_window(auto_id="SampleUiPageHeader", control_type="Text")
    assert header.exists(timeout=5.0), "导航到插件页后未出现页面锚点"
    assert "UI 示例" in header.window_text()

    # 宿主签发定时器在真实进程里推进心跳：计数大于 0 即证回调链活着且落在插件 VM 上。
    def _ticks():
        match = re.search(r"(\d+) 次", text_of(win, "SampleUiHeartbeatTicks", timeout=0.5))
        return int(match.group(1)) if match else None

    ticks = wait_until(_ticks, timeout=5.0, description="定时器心跳计数 > 0")
    assert ticks > 0

    # 双向绑定交互：编辑框回写 VM，回显行同步更新——插件页绑定面在真实进程工作。
    editor = win.child_window(auto_id="SampleUiMessageInput", control_type="Edit")
    assert editor.exists(timeout=3.0), "插件页消息编辑框必须存在"
    editor.set_edit_text("e2e 问好")
    assert_text_contains(win, "SampleUiEchoText", "Text", "e2e 问好")


@pytest.mark.parametrize("sandbox_seed", ["disabled-sample-ui"], indirect=True)
def test_disabled_plugin_degrades_extensions(app):
    """停用 UI 插件后的降级：导航项与设置区块离场不空壳，其余插件不受波及。"""
    win, _ = app

    assert not win.child_window(auto_id=SAMPLE_UI_NAV, control_type="RadioButton").exists(timeout=3.0), (
        "停用插件的导航项不得出现"
    )

    goto(win, 4)
    assert_text_contains(win, SAMPLE_UI_STATUS, "Text", "已停用")
    assert not win.child_window(auto_id=SAMPLE_UI_SETTINGS_SECTION, control_type="Text").exists(timeout=3.0), (
        "停用插件的设置区块不得出现"
    )
    assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")


def test_plugin_manager_update_ui_plugin_pending_restart(app):
    """界面插件更新转入待重启：状态与说明文案同步，挂起版本落宿主状态文件，注册面即刻摘除。"""
    win, local_app_data = app
    goto(win, 4)

    assert_text_contains(win, SAMPLE_UI_STATUS, "Text", "活动")
    assert win.child_window(auto_id=SAMPLE_UI_SETTINGS_SECTION, control_type="Text").exists(timeout=3.0), (
        "活动插件应贡献设置区块"
    )

    win.child_window(auto_id=SAMPLE_UI_UPDATE, control_type="Button").invoke()

    assert_text_contains(win, SAMPLE_UI_STATUS, "Text", "待重启")
    pending = assert_text_contains(
        win, f"PluginManagerPendingRestart_{SAMPLE_UI_ID}", "Text", "下次启动生效"
    )
    assert "1.0.0" in pending, f"挂起说明必须带待装载版本：{pending}"
    _read_plugin_state(
        local_app_data,
        lambda state: state["Plugins"][SAMPLE_UI_ID].get("PendingVersion") == "1.0.0",
    )

    # 界面插件更新语义 = 旧实例即刻卸载、新版本下次启动装载：导航项与设置区块随资产出账。
    _wait_absent(win, SAMPLE_UI_NAV, "RadioButton")
    _wait_absent(win, SAMPLE_UI_SETTINGS_SECTION, "Text")


@pytest.mark.parametrize("sandbox_seed", ["developer-user-plugin"], indirect=True)
def test_plugin_manager_uninstall_removes_user_plugin(app):
    """彻底移除用户目录插件：后台形态确认按「是」应答，包与宿主状态离场，内置插件不受波及。"""
    win, local_app_data = app
    user_package_dir = os.path.join(str(local_app_data), "StarPie", "plugins", USER_PLUGIN_ID)
    goto(win, 4)

    # 预置为停用：未装载的包没有进程内文件锁，四类产物（包/配置/数据/状态）可全量断言清理。
    assert_text_contains(win, f"PluginManagerStatus_{USER_PLUGIN_ID}", "Text", "已停用")

    win.child_window(auto_id=f"PluginManagerUninstall_{USER_PLUGIN_ID}", control_type="Button").invoke()

    _wait_absent(win, f"PluginManagerName_{USER_PLUGIN_ID}", "Text")
    _read_plugin_state(local_app_data, lambda state: USER_PLUGIN_ID not in state["Plugins"])
    assert not os.path.exists(user_package_dir), "用户包目录必须随彻底移除删除"
    assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")
