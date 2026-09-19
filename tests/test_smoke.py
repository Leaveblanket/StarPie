"""合并巡检层（快检）：一次实例扫一个页面/主题的稳定观察面，失败项附单例复审入口。

两档运行：
- 开工/日常快检：`scripts/run-e2e.ps1 -TestPath tests/test_smoke.py`
  —— 少量实例覆盖页面就绪、控件存在、目录一致、语言刷新、插件页只读、选择器可开可取消。
- 具体问题复审：按失败报告里的节点跑单例，例如
  `scripts/run-e2e.ps1 -NoBuild -TestPath tests/test_settings.py::test_v130_wheel_themes_and_custom_preset_and_text_sync`
  —— 单例保持原粒度（每用例一个沙箱实例 + 精确断言），巡检只负责指出"哪一片"坏了。

纪律：
- 巡检只做"只读 + 可撤回"检查（如语言切换后切回 zh-CN）；改运行态/落盘/破坏性/依赖启动
  预置/全局桌面副作用的检查留在单例层，不进巡检。
- 子检查失败不中断后续：巡检的定位是一次扫出全部问题，`SmokeReport` 收齐后统一报告；
  单个子检查内部仍快速失败。
- 断言复用 conftest 共享 helper，与单例同源；巡检绿不代表对应行为无回归——回归判定仍以
  全量单例 + 落盘专验为准（tests/test_persistence.py）。
"""

from catalogs import (
    APP_THEME_CATALOG,
    CORE_ICON_CATALOG,
    GLOW_PRESET_CATALOG,
    ICON_LAYOUT_MODE_CATALOG,
    LANGUAGE_CATALOG,
    PROGRAM_PICKER_TITLE,
    PROGRAM_SOURCE_PLUGIN_ID,
    SAMPLE_UI_PLUGIN_ID,
    SHAPE_CATALOG,
    WHEEL_PALETTE_CATALOG,
    WHEEL_STYLE_CATALOG,
)
from conftest import (
    assert_catalog,
    assert_page_ready,
    assert_text_contains,
    cancel_dialog,
    goto,
    list_item_texts,
    select_option,
    text_of,
    wait_dialog,
)

PLUGIN_STATUS = f"PluginManagerStatus_{PROGRAM_SOURCE_PLUGIN_ID}"
PLUGIN_DIAGNOSTICS = f"PluginManagerDiagnostics_{PROGRAM_SOURCE_PLUGIN_ID}"
SAMPLE_UI_NAV = f"NavPlugin_{SAMPLE_UI_PLUGIN_ID}"


class SmokeReport:
    """巡检子检查收集器：跑完全部子检查再统一报告，失败项附复审判例节点。

    只收集不吞咽：异常原样进报告（类型 + 文本），`assert_clean` 一次性抛出；
    子检查之间不互相中断，避免"修一个才发现下一个"的往返。
    """

    def __init__(self) -> None:
        self.failures = []

    def check(self, name: str, review: str, fn) -> None:
        try:
            fn()
        except Exception as ex:
            self.failures.append((name, review, ex))

    def assert_clean(self, scope: str) -> None:
        if not self.failures:
            return
        lines = [
            f"{index}. {name} —— 复审: {review}\n     {type(ex).__name__}: {ex}"
            for index, (name, review, ex) in enumerate(self.failures, 1)
        ]
        raise AssertionError(f"{scope} 巡检失败 {len(self.failures)} 项：\n" + "\n".join(lines))


def _assert_controls(win, controls, timeout: float = 3.0) -> None:
    """断言一组 (auto_id, control_type) 控件全部存在（失败一次列出缺失项）。"""
    missing = [
        auto_id
        for auto_id, ctype in controls
        if not win.child_window(auto_id=auto_id, control_type=ctype).exists(timeout=timeout)
    ]
    assert not missing, f"缺少控件: {missing}"


def test_smoke_console_pages_and_controls(app):
    """设置台页面与控件巡检：四页导航/就绪 + 各页稳定控件存在性（只读，不改运行态）。"""
    win, _ = app
    report = SmokeReport()

    def _pages():
        for slot in range(4):
            goto(win, slot)
            assert_page_ready(win, slot)

    report.check(
        "四页导航与稳定控件就绪",
        "tests/test_settings.py::test_switch_all_tabs_smoothly",
        _pages,
    )

    def _trigger_controls():
        goto(win, 0)
        _assert_controls(win, (
            ("TriggerButtonBadgeText", "Text"),
            ("TriggerButtonOptionRight", "RadioButton"),
            ("ResetTriggerButtonButton", "Button"),
            ("EnableOuterEscapeCheckBox", "CheckBox"),
            ("AddBlacklistButton", "Button"),
            ("DeleteBlacklistButton", "Button"),
            ("DisableOnFullScreenCheckBox", "CheckBox"),
            ("DisableOnCtrlCheckBox", "CheckBox"),
            ("DisableOnShiftCheckBox", "CheckBox"),
            ("DisableOnAltCheckBox", "CheckBox"),
        ))

    report.check(
        "触发页触发键配置面、旁路开关与黑名单控件就位",
        "tests/test_wheel_gesture.py::test_wheel_config_surface_switch_trigger_live",
        _trigger_controls,
    )

    def _appearance_geometry():
        goto(win, 1)
        _assert_controls(win, (
            ("InnerRadiusSlider", "Slider"),
            ("CoreRadiusSlider", "Slider"),
            ("SectorIconSizeSlider", "Slider"),
            ("SectorFontSizeSlider", "Slider"),
        ))

    report.check(
        "外观页几何/图标/文字控件就位",
        "tests/test_settings.py::test_appearance_geometry_layout_and_showtext_live_apply",
        _appearance_geometry,
    )

    def _appearance_glow_and_panel():
        goto(win, 1)
        _assert_controls(win, (
            ("HighlightGlowPresetComboBox", "ComboBox"),
            ("ShowCoreIconCheckBox", "CheckBox"),
            ("CoreIconTypeComboBox", "ComboBox"),
        ))
        # 配色面板容器在折叠态也在树里（展开是交互，留给单例）
        assert win.child_window(auto_id="CustomColorExpander", control_type="Group").exists(timeout=3), (
            "CustomColorExpander 必须存在"
        )

    report.check(
        "外观页 glow/中心图标/配色面板容器就位",
        "tests/test_settings.py::test_v136_glow_color_customization_config_memory_and_core_image",
        _appearance_glow_and_panel,
    )

    def _wheelinteraction_controls():
        goto(win, 2)
        _assert_controls(
            win,
            (
                ("AddProfileButton", "Button"),
                ("AddCustomProfileButton", "Button"),
                ("RenameProfileButton", "Button"),
                ("DeleteProfileButton", "Button"),
                ("SectorCount4Radio", "RadioButton"),
                ("SectorCount8Radio", "RadioButton"),
                ("SectorCount12Radio", "RadioButton"),
            ),
        )
        items = list_item_texts(win.child_window(auto_id="ProfilesListBox", control_type="List"))
        assert any("Global" in item for item in items), f"Global 兜底方案必须列出: {items}"

    report.check(
        "轮盘页方案按钮/扇区单选/Global 兜底方案就位",
        "tests/test_settings.py::test_profile_management_ui_and_buttons",
        _wheelinteraction_controls,
    )

    def _advanced_admin_entry():
        goto(win, 3)
        restart = win.child_window(auto_id="AdminRestartNowButton", control_type="Button")
        assert restart.exists(timeout=3), "非提权态下「立即以管理员身份重启」入口应当出现"
        assert not restart.is_enabled(), "提权自启的任务不存在时入口不可点"
        assert text_of(win, "AdminRestartNowHint", "Text", timeout=3).strip(), "入口不可点时必须呈现原因"

    report.check(
        "高级页管理员重启入口只读检查",
        "tests/test_settings.py::test_admin_restart_entry_visibility_without_elevation",
        _advanced_admin_entry,
    )

    report.assert_clean("设置台页面与控件")


def test_smoke_catalogs_and_i18n(app):
    """目录一致性与语言巡检：全部下拉目录 + WheelBg* 缺席 + 语言往返（切回 zh-CN）。"""
    win, _ = app
    report = SmokeReport()

    def _appearance_catalogs():
        goto(win, 1)
        for auto_id, catalog in (
            ("AppThemeComboBox", APP_THEME_CATALOG),
            ("WheelPaletteComboBox", WHEEL_PALETTE_CATALOG),
            ("WheelStyleComboBox", WHEEL_STYLE_CATALOG),
            ("ShapeComboBox", SHAPE_CATALOG),
            ("CoreIconTypeComboBox", CORE_ICON_CATALOG),
            ("HighlightGlowPresetComboBox", GLOW_PRESET_CATALOG),
            ("IconLayoutModeComboBox", ICON_LAYOUT_MODE_CATALOG),
        ):
            assert_catalog(win.child_window(auto_id=auto_id, control_type="ComboBox"), catalog)

    report.check(
        "外观页下拉目录与产品目录一致",
        "tests/test_settings.py::test_v130_wheel_themes_and_custom_preset_and_text_sync",
        _appearance_catalogs,
    )

    def _wheel_bg_absent():
        goto(win, 1)
        auto_ids = {element.element_info.automation_id for element in win.descendants()}
        assert "WheelPaletteComboBox" in auto_ids, f"外观页控件枚举异常，否定断言不可证否: {sorted(auto_ids)}"
        stale = sorted(auto_id for auto_id in auto_ids if auto_id.startswith("WheelBg"))
        assert not stale, f"已下线的轮盘背景图片控件仍存在: {stale}"

    report.check(
        "WheelBg* 下线控件整体缺席（全量枚举可证否）",
        "tests/test_settings.py::test_app_theme_switch_and_removed_wheel_bg_controls",
        _wheel_bg_absent,
    )

    def _language_roundtrip():
        goto(win, 3)
        assert_catalog(win.child_window(auto_id="LanguageComboBox", control_type="ComboBox"), LANGUAGE_CATALOG)

        select_option(win.child_window(auto_id="LanguageComboBox", control_type="ComboBox"), LANGUAGE_CATALOG, "en")
        assert_text_contains(win, "SaveButton", "Button", "Save")
        goto(win, 1)
        assert_text_contains(win, "AppearancePageSubheader", "Text", "Customize visual styles")

        goto(win, 3)
        select_option(win.child_window(auto_id="LanguageComboBox", control_type="ComboBox"), LANGUAGE_CATALOG, "ja")
        assert_text_contains(win, "SaveButton", "Button", "保存")

        select_option(win.child_window(auto_id="LanguageComboBox", control_type="ComboBox"), LANGUAGE_CATALOG, "zh-CN")
        assert_text_contains(win, "AdvancedPageHeader", "Text", "系统集成与高级偏好设置")

    report.check(
        "语言目录与 en/ja/zh-CN 切换即时刷新（切回 zh-CN）",
        "tests/test_settings.py::test_v138_i18n_multilanguage_support",
        _language_roundtrip,
    )

    report.assert_clean("目录与 i18n")


def test_smoke_plugin_page_readonly(app):
    """插件页只读巡检：内置插件活动态 + 诊断面板 + 非提权无警示 + 插件导航项可达。"""
    win, _ = app
    report = SmokeReport()

    def _plugin_list_and_diagnostics():
        goto(win, 4)
        name = text_of(win, f"PluginManagerName_{PROGRAM_SOURCE_PLUGIN_ID}", "Text", timeout=5.0)
        assert name, "插件条目必须有展示名"
        assert_text_contains(win, PLUGIN_STATUS, "Text", "活动")
        assert not win.child_window(auto_id="PluginManagerElevatedNotice", control_type="Text").exists(timeout=1.0), (
            "非提权态插件页不得出现提权警示行"
        )
        win.child_window(auto_id=PLUGIN_DIAGNOSTICS, control_type="Button").invoke()
        diagnostics = assert_text_contains(win, "PluginDiagnosticsText", "Edit", "内置", timeout=5.0)
        assert PROGRAM_SOURCE_PLUGIN_ID in diagnostics

    report.check(
        "内置插件活动态与诊断面板",
        "tests/test_plugin_manager.py::test_plugin_manager_lists_builtin_plugin",
        _plugin_list_and_diagnostics,
    )

    def _plugin_page_navigation():
        radio = win.child_window(auto_id=SAMPLE_UI_NAV, control_type="RadioButton")
        assert radio.exists(timeout=5.0), "启动装载的 UI 插件必须注册侧边栏导航项"
        radio.select()
        header = win.child_window(auto_id="SampleUiPageHeader", control_type="Text")
        assert header.exists(timeout=5.0), "导航到插件页后未出现页面锚点"
        assert "UI 示例" in header.window_text()

    report.check(
        "UI 插件导航项与页面锚点可达",
        "tests/test_plugin_manager.py::test_plugin_page_navigation_and_interaction",
        _plugin_page_navigation,
    )

    report.assert_clean("插件页只读")


def test_smoke_program_picker_opens_and_cancels(app):
    """程序选择器巡检：打开呈现/取消干净、方案列表不新增（可撤回，无落盘）。"""
    win, _ = app
    report = SmokeReport()

    def _picker_cancel():
        goto(win, 2)
        profiles_list = win.child_window(auto_id="ProfilesListBox", control_type="List")
        before = list_item_texts(profiles_list)

        win.child_window(auto_id="AddProfileButton", control_type="Button").invoke()
        picker = wait_dialog(PROGRAM_PICKER_TITLE)
        rect = picker.element_info.rectangle
        assert rect.left > -1000 and rect.top > -1000, f"程序选择器必须呈现于屏幕，got {rect}"
        cancel_dialog(picker)

        goto(win, 3)
        goto(win, 2)
        after = list_item_texts(profiles_list)
        assert after == before, f"取消选择不应新增方案: {before} -> {after}"

    report.check(
        "程序选择器打开/取消与方案列表不变",
        "tests/test_settings.py::test_program_picker_opens_and_cancels_cleanly",
        _picker_cancel,
    )

    report.assert_clean("程序选择器")
