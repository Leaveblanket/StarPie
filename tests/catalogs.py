"""产品契约的单一出处：下拉目录、对话框标题与插件 id。

用例与 helper 统一从这里取名，避免同一份产品契约在多个文件里各写一份——
目录/标题变更时只改这里，`select_option` / `assert_catalog` 会以显式失败提示同步
（并注明产品侧来源文件，便于回查）。
"""

# 产品下拉目录（Tag 的固定顺序；UIA 不暴露选项文本，故按目录定名）
APP_THEME_CATALOG = ("System", "Light", "Dark", "MidnightNavy", "RoyalViolet", "TitaniumGray")  # InterfaceThemeSettingsViewModel
WHEEL_STYLE_CATALOG = ("ClassicRing", "CleanSectors", "Glassmorphism")  # AppearanceSettingsPage.xaml
WHEEL_PALETTE_CATALOG = ("System", "Dark", "Light", "MatchaForest", "GlacialIce", "MorandiMuted")  # 固定项；自定义预设追加在后
SHAPE_CATALOG = ("Original", "Circle", "RoundedCapsule", "HexagonHive")  # AppearanceSettingsPage.xaml
CORE_ICON_CATALOG = ("Exit", "Crosshair", "Windows", "Dot", "Home", "Power", "Compass", "CatPaw", "Custom", "Image")  # AppearanceSettingsPage.xaml
GLOW_PRESET_CATALOG = ("Auto", "Lilac", "Blue", "Emerald", "Rose", "Amber", "Red", "White", "Custom")  # AppearanceSettingsPage.xaml
LANGUAGE_CATALOG = ("zh-CN", "zh-TW", "en", "ja", "Auto")  # AdvancedSettingsPage.xaml
SLOT_ACTION_TYPE_CATALOG = ("Hotkey", "Launch", "Folder", "System")  # SlotViewModel.ActionTypes
ICON_LAYOUT_MODE_CATALOG = ("IconAndText", "IconOnly", "TextOnly")  # AppearanceSettingsPage.xaml

# 对话框标题（Win32 标题 = 定位面，产品侧窗口标题即此文本）
PROGRAM_PICKER_TITLE = "选择程序 - StarPie"
ICON_PICKER_TITLE = "选择动作矢量图标 - StarPie"
COLOR_PICKER_TITLE = "色彩选择器与屏幕吸管 (Color Picker) - StarPie"
SAVE_PRESET_TITLE = "保存配色预设"
EXPORT_DIALOG_TITLE = "导出配置文件"
IMPORT_DIALOG_TITLE = "选择要导入的配置文件"

# 内置/测试插件 id（管理页 AutomationId 由用例按 id 拼接）
PROGRAM_SOURCE_PLUGIN_ID = "starpie.builtin.program-source"
SAMPLE_UI_PLUGIN_ID = "starpie.builtin.sample-ui"
USER_PLUGIN_ID = "e2e.user.probe"
BROKEN_PLUGIN_ID = "e2e.broken.probe"
