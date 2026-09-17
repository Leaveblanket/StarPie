"""托盘状态信号系统级用例（#152 导航视图出账与恢复重放；#156 关闭即销毁、托盘驻留、重开重建）。

设置台关闭即销毁（不再是隐藏驻留）→ 常驻壳层继续托盘驻留 → 恢复消息发往常驻壳层，
壳层重建并显示设置台、按最后导航槽位重放页面：UIA invoke 关闭 + 消息级恢复，
全程零物理键鼠输入。
出账置空/重放命中/lastSlot 为空/幂等/插件页 VM 回收由 NavigationSuspensionTests 锁；
窗口生命周期不变量（全局窗口集合只剩锚窗口、主窗口属性指向锚窗口）由
ResidentShellLifetimeTests 锁。
"""

import win32gui

from conftest import Desktop, find_main_window, goto, wait_until


def test_tray_release_rebuilds_page(app):
    """关闭设置台触发导航出账，恢复后按最后导航槽位重建新设置台且选中态保持。"""
    win, _ = app
    pid = win.process_id()

    # 停驻外观页（非首页）——出账记录最后导航槽位，恢复重放按它命中目录
    goto(win, 1)
    radio1 = win.child_window(auto_id="NavPage1", control_type="RadioButton")
    assert radio1.is_selected(), "出账前外观页导航项应处于选中态"

    first_handle = win.handle

    # UIA invoke 关闭按钮：设置台关闭即销毁（不隐藏驻留），进托盘序列执行导航出账
    close_btn = win.child_window(auto_id="CloseButton", control_type="Button")
    assert close_btn.exists(timeout=3), "CloseButton 必须存在"
    close_btn.invoke()

    wait_until(
        lambda: not win32gui.IsWindow(first_handle),
        timeout=8,
        description="设置台窗口已销毁（关闭即销毁、托盘驻留）",
    )

    # 常驻壳层仍在：托盘消息窗口的存活即进程仍驻留托盘的证据。
    tray_handle = win32gui.FindWindow(None, "StarPieTrayWindow")
    assert tray_handle, "常驻托盘消息窗口必须存在（常驻壳层承担恢复消息接收）"

    # 消息级恢复：单实例重激活的恢复管道（App.OnStartup 置前分支 → 常驻壳层 WndProc →
    # 设置台按需重建 + WPF ShowAndActivate）——测试经同一条注册窗口消息驱动，零物理键鼠输入。
    restore_msg = win32gui.RegisterWindowMessage("StarPie_SingleInstance_Restore")
    win32gui.SendMessage(tray_handle, restore_msg, 0, 0)

    # 重开重建：恢复后出现的是新窗口句柄（旧窗口已销毁，不是隐藏后复用）
    rebuilt = []

    def _rebuilt_handle():
        try:
            candidate = find_main_window(pid, timeout=0.5)
        except AssertionError:
            return False
        if candidate != first_handle:
            rebuilt.append(candidate)
            return True
        return False

    wait_until(_rebuilt_handle, timeout=10, description="设置台按需重建（新窗口句柄）")
    win = Desktop(backend="uia").window(handle=rebuilt[-1])
    win.wait("visible", timeout=5)

    # 恢复重放：外观页视图按最后导航槽位重建，锚点重新就位
    anchor = win.child_window(auto_id="AppearancePageSubheader", control_type="Text")
    wait_until(lambda: anchor.exists() and anchor.is_visible(), timeout=5, description="外观页锚点随重放重建")

    # 选中态保持：出账前停驻的导航项恢复后仍选中
    radio1 = win.child_window(auto_id="NavPage1", control_type="RadioButton")
    assert radio1.is_selected(), "恢复后外观页导航项选中态应保持"

    # 重建后的视图交互完好：导航往返真实可用
    goto(win, 2)
    goto(win, 1)
