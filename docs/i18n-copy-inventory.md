# T28 文案缺口盘点清单（#33）

> 依据：ADR-0010「内容与机制分离」；本清单只盘点、不实现。
> 范围：设置界面（5 页 + 壳层 + 对话框 + View 动态生成项）所有未本地化硬编码文案。
> 方法：全量扫描 `WinPieGestures/Views/**/*.xaml`（Text/Content/ToolTip/Title 字面量）与 `I18n.Translations` 键表对照；行号基于 `main` @ b46420e。
> 非目标：托盘品牌/版本名（StarPie v1.4.1 / DevInstance.Suffix）锁死不翻译；历史里程碑日期、版本号、纯 emoji 不译。

## 统计

| 类别 | 数量 | 说明 |
|---|---|---|
| A 键已存在、XAML 未接线 | ≈55 | T24 迁移遗漏，接线即可（但见「键值漂移」） |
| B 键缺失需新增（标签/ToolTip） | ≈40 | 需补 zh-CN/zh-TW/en/ja 四语言值 |
| B2 键缺失（About 里程碑正文） | 25 标题 + 29 描述 | 是否翻译需产品决策（见建议） |
| C 锁死不翻译 | 品牌/版本号/日期/emoji | Sidebar、About 头部、里程碑版本号与日期 |

## A 类：键已存在、XAML 未接线

> ⚠️ **键值漂移**：下列键在 I18n 中的 zh 值可能与当前 XAML 硬编码文案**不一致**（旧键值残留）。接线前需逐条确认目标文案（用 XAML 现值刷新键值，或采用键现值）。

### AboutSettingsPage.xaml
| 行 | XAML 现值 | 现有键 | 键 zh 现值 | 漂移 |
|---|---|---|---|---|
| 36 | 关于与更新日志 | `AboutHeader` | 关于 StarPie & 版本记录 | ⚠️ |
| 83 | 高质感、极速现代 Windows 鼠标轮盘笔势工具 | `AboutDesc` | 高质感、极速现代… | ✅ 一致 |
| 198 | 版本演进里程碑 (Milestones) | `MilestonesTitle` | 版本演进里程碑 (Milestones) | ✅ 一致 |

### AdvancedSettingsPage.xaml
| 行 | XAML 现值 | 现有键 | 键 zh 现值 | 漂移 |
|---|---|---|---|---|
| 77–81 | 语言下拉五项 | `LangZhCn/ZhTw/En/Ja/Auto` | 语言名不翻译 | ✅ 语言名本身不随界面语言变 |
| 183 | 立即清理未引用的工作集物理内存 | — | — | B 类见下 |

### TriggerSettingsPage.xaml
| 行 | XAML 现值 | 现有键 | 键 zh 现值 | 漂移 |
|---|---|---|---|---|
| 70 | 超过轮盘外圈范围后立即解除高亮… | `OuterEscapeDesc` | （需查值） | 待确认（T24 漏接，x:Name 仍在） |

### AppearanceSettingsPage.xaml（键库已全，接线量最大）
| 行 | XAML 现值 | 现有键 | 键 zh 现值 | 漂移 |
|---|---|---|---|---|
| 55 | 软件控制台界面主题 (App Theme) | `ConsoleThemeTitle` | 软件控制台主题 (Console Theme) | ⚠️ |
| 60–65 | 主题下拉六项 | `ThemeSystem/Light/Dark/Navy/Violet/Gray` | 极简纯白 (Pure Light) 等 | ⚠️ 多项 |
| 76 | 轮盘视觉风格与色彩 | `StyleTitle` | 轮盘渲染风格 (Visual Renderer) | ⚠️ |
| 80–82 | 风格下拉三项 | `StyleClassic/Clean/Glass` | 经典圆环/极简扇区/液态毛玻璃 | ✅ 一致 |
| 183 | 高亮边缘光晕模式 (Highlight Glow): | `GlowTitle` | 高亮边缘光晕 (Highlight Edge Glow) | ⚠️ |
| 光晕不透明度/弥散半径 | `GlowOpacity`/`GlowRadius` | 一致 | ✅ |
| 几何形态与尺寸微调 | `GeometryTitle` | 几何形态与尺寸 (Geometry & Dimensions) | ⚠️ |
| 扇区切削形态下拉 | `ShapeOriginal/Circle/Rounded/Capsule/Hexagon` | 原生扇区/极简圆形/平滑圆角/圆润胶囊/未来蜂巢 | ⚠️ 多项 |
| 扇区光学缝隙间距 | `SectorGap` | 扇区间隙 (Sector Gap) | ⚠️ |
| 扇区边缘平滑倒角 | `SectorCornerRadius` | 扇区倒角 (Corner Radius) | ⚠️ |
| 轮盘整体半径(外径) | `RadiusOuter` | 轮盘外半径 (Outer Radius) | ⚠️ |
| 扇区内半径 | `RadiusInner` | 内环半径 (Inner Radius) | ⚠️ |
| 中心核心圆半径 | `RadiusCore` | 核圆半径 (Core Radius) | ⚠️ |
| 一键重置为推荐几何尺寸 | `BtnResetGeometry` | 重置形态默认值 | ⚠️ |
| 图标与排版选项 | `IconLayoutTitle` | 图标与文字排版 (Layout & Typography) | ⚠️ |
| 排版下拉三项 | `LayoutIconText/IconOnly/TextOnly` | 图文并茂/仅显示图标/仅显示文字 | ⚠️ |
| 图标尺寸大小 | `SectorIconSize` | 图标大小 (Icon Size) | ⚠️ |
| 文字字号大小 | `SectorFontSize` | 文字字号 (Font Size) | ⚠️ |
| 中心核圆与图案设置 | `CoreTitle` | 中心核圆图案定制 | ⚠️ |
| 显示核圆中心图标/图案 | `CoreShowIcon` | 显示中心图案 / 贴图 | ⚠️ |
| 核圆图案类型 | `CoreIconType` | 核圆图案模式 | ⚠️ |
| 核圆图案下拉九项 | `CorePatternExit/Crosshair/Windows/Dot/Home/Power/Compass/CatPaw/Image` | 取消叉号/精准准心/… | ⚠️ 多项 |
| 浏览图片... | `BtnBrowseImage` | 浏览选择图片 | ⚠️ |

### SidebarView.xaml / MainView.xaml
| 行 | XAML 现值 | 判定 |
|---|---|---|
| Sidebar 43/71/73 | StarPie / v1.4.1 / © 2026 StarPie | C 锁死（品牌/版本） |
| Sidebar 副标题 | `{DynamicResource AppSubtitle}` | ✅ 已目标态 |
| MainView 底栏 | `{DynamicResource BottomStatusNote/BtnSave/BtnClose}` | ✅ 已目标态 |

### 对话框
| 文件:行 | XAML 现值 | 判定 |
|---|---|---|
| ColorPickerWindow:5 / IconPickerWindow:4 / ProgramPickerWindow:4 | 设计期 Title 占位 | ✅ 运行时 ctor `I18n.T(...)` 即时取词覆盖（ADR-0010 例外），已目标态 |

## B 类：键缺失需新增（建议键名 + 分类）

> 分类：`声明式` = 静态可见 → `{DynamicResource}`；`即时取词` = 动态/一次性 → `I18n.T()`；`ToolTip` 静态挂 XAML → `{DynamicResource}`。

### 页面副标题
| 位置 | 现值 | 建议键 | 分类 |
|---|---|---|---|
| AdvancedSettingsPage:40 | 管理界面语言、Windows 开机自启… | `AdvancedSubheader` | 声明式 |
| GesturesSettingsPage:38 | 支持针对不同前台应用程序… | `GesturesSubheader` | 声明式 |
| AboutSettingsPage:39 | StarPie 现代鼠标轮盘笔势工具版本信息… | `AboutSubheader` | 声明式 |
| TriggerSettingsPage:70 | 超过轮盘外圈范围后… | `OuterEscapeDesc`（A 类） | 声明式 |

### GesturesSettingsPage ToolTip
| 行 | 现值 | 建议键 |
|---|---|---|
| 59 | 从已安装软件或开始菜单中选择程序创建专属配置 | `AddProfileTooltip` |
| 60 | 自定义命名创建新的轮盘配置方案 | `AddCustomProfileTooltip` |
| 61 | 重命名选中的配置方案 | `RenameProfileTooltip` |
| 62 | 删除选中的配置方案 | `DeleteProfileTooltip` |
| 111 | 点击选取矢量图标 | `PickIconTooltip` |
| 144 | 选择的应用程序路径 | `SelectedAppPathTooltip` |
| 145 | 选择应用程序或快捷方式... | `BrowseAppTooltip`（A 类键存在，接 XAML） |
| 157 | 选择的本地文件夹路径 | `SelectedFolderPathTooltip` |
| 158 | 选择本地文件夹... | `BrowseFolderTooltip`（A 类键存在，接 XAML） |
| 173 | 启动参数 (如命令行参数或URL) | `LaunchArgsTooltip` |

### TriggerSettingsPage ToolTip
| 行 | 现值 | 建议键 |
|---|---|---|
| 146 | 从已安装软件列表中快速选择要排除的程序 | `BlacklistBrowseTooltip` |
| 149 | 将输入框中的进程名称加入排除黑名单 | `BlacklistAddTooltip` |

### AdvancedSettingsPage ToolTip
| 行 | 现值 | 建议键 |
|---|---|---|
| 183 | 立即清理未引用的工作集物理内存 | `TrimMemoryTooltip` |

### AppearanceSettingsPage 标签/ToolTip/按钮
| 行 | 现值 | 建议键 |
|---|---|---|
| 56 | 定制 StarPie 控制台整体界面的视觉色彩风格… | `ConsoleThemeDesc` |
| 78 | 主题风格 (UiStyle): | `StyleTypeLabel` |
| 86 | 轮盘配色方案 (Wheel Theme): | `WheelThemeLabel` |
| 93 | 重命名当前选中的自定义配色方案预设 | `RenamePresetTooltip` |
| 109 | 自定义十六进制色彩 (色盘调色 / 屏幕吸色): | `CustomColorsLabel` |
| 119/135/151/167/183 | 扇区底色/边框、高亮底色/边框、文字颜色 | `CustomSectorBgLabel` 等 5 键 |
| 121/137/153/169/185 | 打开调色板选取颜色 | `PickColorTooltip`（复用） |
| 124/140/156/172/188 | 从屏幕任意位置吸取颜色 | `PickEyedropperTooltip`（复用） |
| 368 | 在轮盘扇区中显示动作名称文字 | `ShowTextLabel` |
| 436 | 选择图标... | `BtnChooseIcon` |
| 462 | 自定义图片本地路径 | `CoreCustomImageTooltip` |
| 464 | 清除 | `BtnClear` |
| 489 | 实时交互画布 (Live Preview) | `LivePreviewTitle` |
| 491 | 60FPS 同步渲染 | `LivePreviewFps`（或锁死） |
| 494 | 💡 移动鼠标至下方轮盘可实时测试高亮与磁吸手感 | `LivePreviewHint` |
| 510 | 一键重置为推荐几何尺寸 | 接 `BtnResetGeometry`（A 类，值待定） |

### 对话框 / 样式
| 位置 | 现值 | 建议键 |
|---|---|---|
| ColorPickerWindow:78 | 调色板与屏幕取色 | `ColorPickerHeader` |
| ColorPickerWindow:79 | 在色盘中精调色彩，或使用屏幕吸管捕获任意像素。 | `ColorPickerSubtitle` |
| IconPickerWindow:100 | 支持导入本地 SVG 矢量图或 PNG / ICO / JPG 图片图标 | `IconPickerImportTooltip` |
| SettingsStyles.xaml:487 | 清空快捷键 | `ClearHotkeyTooltip` |

## B2 类：About 里程碑正文（产品决策点）

`AboutSettingsPage.xaml` 105–183（当前版本 v1.4.1 头部 4 条）与 215–465（历史 v1.4.0→v1.3.0 共 8 个版本）均为硬编码中文。

**建议**：历史里程碑锁死不翻译（版本号+日期本来锁死；正文按常见 changelog 惯例只译最新版本），仅 v1.4.1 的 4 条正文 + 8 个版本标题加键。若票面要求全部随语言切换，则需新增 25 标题 + 29 描述键 × 4 语言。**此决策需产品确认，勿在盘点阶段擅自定。**

## C 类：锁死不翻译

- SidebarView.xaml 43/71/73：StarPie、v1.4.1、© 2026 StarPie
- AboutSettingsPage.xaml 72/78：StarPie、v1.4.1
- AboutSettingsPage.xaml 105/215/248/281/320/353/386/419/452：版本号（v1.4.1…v1.3.0）
- AboutSettingsPage.xaml 228/261/294/333/366/399/432/465：日期（2026-08-xx）
- 纯 emoji 图标（📁/📂/🌐/🛡️/🚀/🔍 等）不建键
- RadialWindow.xaml:4 `Title="RadialWindow"`：轮盘窗口标题不可见（待确认运行时是否覆盖；如可见则归 B 类）

## 实施建议（接线顺序）

1. **先确认键值漂移策略**：A 类键用「XAML 现值」更新 I18n 值后接线（保持当前 UI 文案不变），或采用键现值（改变 UI 文案）。建议前者，避免无计划文案变更。
2. **A 类接线**（纯 XAML，约 55 处）：改 `{DynamicResource}`，删除纯回填 x:Name（`GesturesPageSubheader`/`AdvancedPageSubheader`/`OuterEscapeCheckboxDescText`），需要 e2e 定位的补 `AutomationProperties.AutomationId`。
3. **B 类补键**（约 40 键 × 4 语言）→ I18n.cs 后接线。
4. **B2 待产品决策**。
5. **e2e**：在 `tests/test_settings.py` 扩展语言切换断言（按 AutomationId 定位，禁按文本定位）；重点页：Advanced（语言下拉五项即时切换）、Appearance（主题/风格/形态下拉文案随切语）、About（页头/副标题/里程碑折叠）。

## 2026-09-04 更新：C1（#40）已实现——轮盘瞬态叠加层文案键

> 上文为 #33/T28 盘点快照（范围：设置界面）；本组键为 #40 落地时新增，已实现，非待办缺口。
> 语义：轮盘按手势瞬态创建/绘制，属即时取词——创建/绘制时读当前语言，不随切换刷新。

| 键 | zh-CN | zh-TW | en | ja | 消费点 |
|---|---|---|---|---|---|
| `WheelCoreTitle` | 全局动作 | 全域動作 | Global Actions | グローバル操作 | `WheelViewModel` 构造（Global Profile 核心标题） |
| `WheelCoreSubtitle` | {0} 键动作 | {0} 鍵動作 | {0} Actions | {0} アクション | `WheelViewModel` 构造（{0}=扇区数） |
| `WheelSectorEmpty` | 未设置 | 未設定 | Not set | 未設定 | `RadialWindow.RenderSectors` 空扇区占位 |
