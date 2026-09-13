"""托盘状态信号系统级用例（#152：导航视图出账与恢复重放）。

进托盘导航出账 → 恢复重放重建视图：UIA invoke 关闭（隐藏到托盘）→ 消息级恢复
（等价单实例重激活的 ShowWindow(SW_RESTORE) 路径），全程零物理键鼠输入。

该用例以可见形态启动（@pytest.mark.onscreen）：后台静默形态下出账动作禁用
（TrayVisibilitySignal 门控），恢复重放为 no-op，覆盖不到"出账 → 重建"路径。
出账置空/重放命中/lastSlot 为空/幂等/插件页 VM 回收由 NavigationSuspensionTests 锁。
"""

import win32gui
import pytest

from conftest import goto, wait_until


@pytest.mark.onscreen
def test_tray_release_rebuilds_page(app):
    """关闭隐藏到托盘触发导航出账，恢复后按最后导航槽位重建视图且选中态保持。"""
    win, local_app_data = app

    # 停驻外观页（非首页）——出账记录最后导航槽位，恢复重放按它命中目录
    goto(win, 1)
    radio1 = win.child_window(auto_id="NavPage1", control_type="RadioButton")
    assert radio1.is_selected(), "出账前外观页导航项应处于选中态"

    # UIA invoke 关闭按钮：窗口隐藏到托盘（非退出），进托盘序列执行导航出账
    close_btn = win.child_window(auto_id="CloseButton", control_type="Button")
    assert close_btn.exists(timeout=3), "CloseButton 必须存在"
    close_btn.invoke()

    wait_until(lambda: not win.is_visible(), timeout=3, description="窗口隐藏到托盘")

    # 消息级恢复：单实例重激活的恢复管道（App.OnStartup 置前分支 → 主框架 WndProc →
    # WPF ShowAndActivate）——测试经同一条注册窗口消息驱动，零物理键鼠输入。
    restore_msg = win32gui.RegisterWindowMessage("StarPie_SingleInstance_Restore")
    win32gui.SendMessage(win.handle, restore_msg, 0, 0)
    wait_until(lambda: win.is_visible(), timeout=5, description="窗口从托盘恢复可见")

    # 恢复重放：外观页视图按最后导航槽位重建，锚点重新就位
    anchor = win.child_window(auto_id="AppearancePageSubheader", control_type="Text")
    wait_until(lambda: anchor.exists() and anchor.is_visible(), timeout=5, description="外观页锚点随重放重建")

    # 选中态保持：出账前停驻的导航项恢复后仍选中
    assert radio1.is_selected(), "恢复后外观页导航项选中态应保持"

    # 重建后的视图交互完好：导航往返真实可用
    goto(win, 2)
    goto(win, 1)
