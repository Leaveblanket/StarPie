using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinPieGestures.Services.Localization
{
    public enum LanguageCode
    {
        ZhCn, // 简体中文
        ZhTw, // 繁體中文
        En,   // English
        Ja    // 日本語
    }

    public static class I18n
    {
        private static LanguageCode _currentLanguage = LanguageCode.ZhCn;
        public static event Action? LanguageChanged;

        public static LanguageCode CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LanguageChanged?.Invoke();
                }
            }
        }

        public static string CurrentLanguageCode => _currentLanguage switch
        {
            LanguageCode.ZhTw => "zh-TW",
            LanguageCode.En => "en",
            LanguageCode.Ja => "ja",
            _ => "zh-CN"
        };

        public static void SetLanguage(string code)
        {
            if (string.Equals(code, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                var culture = CultureInfo.CurrentUICulture.Name;
                if (culture.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageCode.ZhTw;
                }
                else if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageCode.ZhCn;
                }
                else if (culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageCode.Ja;
                }
                else
                {
                    CurrentLanguage = LanguageCode.En;
                }
                return;
            }

            CurrentLanguage = code switch
            {
                "zh-TW" or "zh-HK" or "zh-Hant" => LanguageCode.ZhTw,
                "en" or "en-US" or "en-GB" => LanguageCode.En,
                "ja" or "ja-JP" => LanguageCode.Ja,
                _ => LanguageCode.ZhCn
            };
        }

        public static string T(string key) => GetString(key);

        public static string GetString(string key)
        {
            if (Translations.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(_currentLanguage, out var val))
                    return val;
                if (dict.TryGetValue(LanguageCode.ZhCn, out var fallback))
                    return fallback;
            }
            return key;
        }

        private static readonly Dictionary<string, Dictionary<LanguageCode, string>> Translations = new()
        {
            // Standard Global Buttons & Actions
            ["RenameCustomPresetButton"] = new()
            {
                [LanguageCode.ZhCn] = "✏️ 重命名预设",
                [LanguageCode.ZhTw] = "✏️ 重新命名預設",
                [LanguageCode.En] = "✏️ Rename Preset",
                [LanguageCode.Ja] = "✏️ プリセット名を変更"
            },
            ["RenameCustomPresetTitle"] = new()
            {
                [LanguageCode.ZhCn] = "重命名配色方案预设",
                [LanguageCode.ZhTw] = "重新命名配色方案預設",
                [LanguageCode.En] = "Rename Color Preset",
                [LanguageCode.Ja] = "カラープリセット名を変更"
            },
            ["RenameCustomPresetPrompt"] = new()
            {
                [LanguageCode.ZhCn] = "请输入配色方案预设的新名称：",
                [LanguageCode.ZhTw] = "請輸入配色方案預設的新名稱：",
                [LanguageCode.En] = "Enter a new name for the color preset:",
                [LanguageCode.Ja] = "カラープリセットの新しい名前を入力してください:"
            },
            ["OuterEscapeTitle"] = new()
            {
                [LanguageCode.ZhCn] = "顺势外甩脱离取消 (Outer Escape Cancel)",
                [LanguageCode.ZhTw] = "順勢外甩脫離取消 (Outer Escape Cancel)",
                [LanguageCode.En] = "Outer Escape Cancel",
                [LanguageCode.Ja] = "外側スワイプでキャンセル (Outer Escape)"
            },
            ["OuterEscapeDesc"] = new()
            {
                [LanguageCode.ZhCn] = "手势划出后若想放弃，无需拉回中心，直接顺势向外快速划出即可安全取消，0 误触。",
                [LanguageCode.ZhTw] = "手勢劃出後若想放棄，無需拉回中心，直接順勢向外快速劃出即可安全取消，0 誤觸。",
                [LanguageCode.En] = "Flick cursor outwards past the wheel radius to safely cancel without returning to center.",
                [LanguageCode.Ja] = "ホイールの外側へ素早くスワイプすることで、安全に操作をキャンセルできます。"
            },
                        ["OuterEscapeDistanceTitle"] = new()
            {
                [LanguageCode.ZhCn] = "外甩取消距离灵敏度 (Escape Distance):",
                [LanguageCode.ZhTw] = "外甩取消距離靈敏度 (Escape Distance):",
                [LanguageCode.En] = "Escape Distance Threshold (Sensitivity):",
                [LanguageCode.Ja] = "キャンセルスワイプ距離 (感度設定):"
            },
            ["OuterEscapeDistanceDesc"] = new()
            {
                [LanguageCode.ZhCn] = "设定光标划出距离中心多远时判定为放弃。数值越小越灵敏（更易甩出取消），数值越大越沉稳（需甩得更远）。",
                [LanguageCode.ZhTw] = "設定游標劃出距離中心多遠時判定為放棄。數值越小越靈敏（更易甩出取消），數值越大越沉穩（需甩得更遠）。",
                [LanguageCode.En] = "How far past the center the cursor must travel to cancel. Smaller values cancel easier, larger values require a farther flick.",
                [LanguageCode.Ja] = "中心からどれだけ離れたらキャンセルとするかを設定します。値が小さいほど敏感になり、大きいほど遠くへのスワイプが必要になります。"
            },
            ["OuterEscapeCheckbox"] = new()
            {
                [LanguageCode.ZhCn] = "启用向外顺势甩出取消手势 (推荐开启)",
                [LanguageCode.ZhTw] = "啟用向外順勢甩出取消手勢 (推薦開啟)",
                [LanguageCode.En] = "Enable Outer Escape Cancel (Recommended)",
                [LanguageCode.Ja] = "外側スワイプキャンセルを有効化 (推奨)"
            },
            ["IconPickerImport"] = new()
            {
                [LanguageCode.ZhCn] = "➕ 导入自定义图标...",
                [LanguageCode.ZhTw] = "➕ 匯入自訂圖示...",
                [LanguageCode.En] = "➕ Import Custom Icon...",
                [LanguageCode.Ja] = "➕ カスタムアイコンをインポート..."
            },
            ["CustomColorsExpanderTitle"] = new()
            {
                [LanguageCode.ZhCn] = "🎨 自定义高级配色与色彩微调",
                [LanguageCode.ZhTw] = "🎨 自訂進階配色與色彩微調",
                [LanguageCode.En] = "🎨 Custom Advanced Color Tuning",
                [LanguageCode.Ja] = "🎨 高度なカラーカスタマイズ"
            },
            ["CustomColorsExpanderDesc"] = new()
            {
                [LanguageCode.ZhCn] = "展开后可精准微调扇区底色、高亮光晕、边框线条、文字与光弧等各项色彩。",
                [LanguageCode.ZhTw] = "展開後可精準微調扇區底色、高亮光暈、邊框線條、文字與光弧等各項色彩。",
                [LanguageCode.En] = "Expand to fine-tune individual colors for sectors, highlights, borders, text, and glow.",
                [LanguageCode.Ja] = "セクター、ハイライト、ボーダー、テキストなどの色を個別に調整します。"
            },
            ["MilestonesOlderExpander"] = new()
            {
                [LanguageCode.ZhCn] = "📜 展开查看更早的历史版本演进 (Older Milestones)",
                [LanguageCode.ZhTw] = "📜 展開查看更早的歷史版本演進 (Older Milestones)",
                [LanguageCode.En] = "📜 View Older Milestones",
                [LanguageCode.Ja] = "📜 過去の更新履歴を表示"
            },
            ["BrowseAppTooltip"] = new()
            {
                [LanguageCode.ZhCn] = "选择应用程序或快捷方式...",
                [LanguageCode.ZhTw] = "選擇應用程式或捷徑...",
                [LanguageCode.En] = "Browse application or shortcut...",
                [LanguageCode.Ja] = "アプリまたはショートカットを参照..."
            },
            ["BrowseFolderTooltip"] = new()
            {
                [LanguageCode.ZhCn] = "选择本地文件夹...",
                [LanguageCode.ZhTw] = "選擇本機資料夾...",
                [LanguageCode.En] = "Browse local folder...",
                [LanguageCode.Ja] = "フォルダーを参照..."
            },
            ["BtnConfirm"] = new()
            {
                [LanguageCode.ZhCn] = "确定",
                [LanguageCode.ZhTw] = "確定",
                [LanguageCode.En] = "Confirm",
                [LanguageCode.Ja] = "確定"
            },
            ["BtnCancel"] = new()
            {
                [LanguageCode.ZhCn] = "取消",
                [LanguageCode.ZhTw] = "取消",
                [LanguageCode.En] = "Cancel",
                [LanguageCode.Ja] = "キャンセル"
            },
            ["BtnOk"] = new()
            {
                [LanguageCode.ZhCn] = "确定",
                [LanguageCode.ZhTw] = "確定",
                [LanguageCode.En] = "OK",
                [LanguageCode.Ja] = "OK"
            },
            ["BtnApply"] = new()
            {
                [LanguageCode.ZhCn] = "应用",
                [LanguageCode.ZhTw] = "套用",
                [LanguageCode.En] = "Apply",
                [LanguageCode.Ja] = "適用"
            },
            ["BtnTest"] = new()
            {
                [LanguageCode.ZhCn] = "测试",
                [LanguageCode.ZhTw] = "測試",
                [LanguageCode.En] = "Test",
                [LanguageCode.Ja] = "テスト"
            },
            ["BtnBrowseFolder"] = new()
            {
                [LanguageCode.ZhCn] = "选择文件夹...",
                [LanguageCode.ZhTw] = "選擇資料夾...",
                [LanguageCode.En] = "Browse Folder...",
                [LanguageCode.Ja] = "フォルダーを選択..."
            },
            ["ActionTypeFolder"] = new()
            {
                [LanguageCode.ZhCn] = "📂 打开文件夹",
                [LanguageCode.ZhTw] = "📂 開啟資料夾",
                [LanguageCode.En] = "📂 Open Folder",
                [LanguageCode.Ja] = "📂 フォルダーを開く"
            },
            ["ActionTypeHotkeyShort"] = new()
            {
                [LanguageCode.ZhCn] = "快捷热键",
                [LanguageCode.ZhTw] = "快捷熱鍵",
                [LanguageCode.En] = "Hotkey",
                [LanguageCode.Ja] = "ショートカット"
            },
            ["ActionTypeLaunchShort"] = new()
            {
                [LanguageCode.ZhCn] = "启动程序",
                [LanguageCode.ZhTw] = "啟動程式",
                [LanguageCode.En] = "Run App",
                [LanguageCode.Ja] = "アプリ起動"
            },
            ["ActionTypeFolderShort"] = new()
            {
                [LanguageCode.ZhCn] = "打开文件夹",
                [LanguageCode.ZhTw] = "開啟資料夾",
                [LanguageCode.En] = "Open Folder",
                [LanguageCode.Ja] = "フォルダー"
            },
            ["ActionTypeSystemShort"] = new()
            {
                [LanguageCode.ZhCn] = "系统控制",
                [LanguageCode.ZhTw] = "系統控制",
                [LanguageCode.En] = "System",
                [LanguageCode.Ja] = "システム"
            },
            ["ProfileCardTitle"] = new()
            {
                [LanguageCode.ZhCn] = "当前配置方案 (Profile)",
                [LanguageCode.ZhTw] = "當前配置方案 (Profile)",
                [LanguageCode.En] = "Active Profiles",
                [LanguageCode.Ja] = "プロファイル設定"
            },
            ["ProfileCardDesc"] = new()
            {
                [LanguageCode.ZhCn] = "选择或新建针对特定程序（如 Chrome、VS Code）或特定工作流的轮盘配置方案（支持双击重命名）。",
                [LanguageCode.ZhTw] = "選擇或新建針對特定程式（如 Chrome、VS Code）或特定工作流程的輪盤配置方案（支援按兩下重新命名）。",
                [LanguageCode.En] = "Select or create dedicated pie wheel profiles for specific apps (e.g. Chrome, VS Code) or workflows (double-click to rename).",
                [LanguageCode.Ja] = "アプリ（Chrome、VS Codeなど）やワークフローごとに専用のプロファイルを設定します（ダブルクリックで名前変更）。"
            },
            ["BtnAddAppProfile"] = new()
            {
                [LanguageCode.ZhCn] = "➕ 新增程序专属配置",
                [LanguageCode.ZhTw] = "➕ 新增程式專屬配置",
                [LanguageCode.En] = "➕ Add App Profile",
                [LanguageCode.Ja] = "➕ アプリ専用設定を追加"
            },
            ["BtnAddCustomProfile"] = new()
            {
                [LanguageCode.ZhCn] = "➕ 新建自定义配置",
                [LanguageCode.ZhTw] = "➕ 新建自訂配置",
                [LanguageCode.En] = "➕ Add Custom Profile",
                [LanguageCode.Ja] = "➕ カスタム設定を追加"
            },
            ["BtnRenameProfile"] = new()
            {
                [LanguageCode.ZhCn] = "✏️ 重命名当前配置",
                [LanguageCode.ZhTw] = "✏️ 重新命名當前配置",
                [LanguageCode.En] = "✏️ Rename Profile",
                [LanguageCode.Ja] = "✏️ 名前を変更"
            },
            ["BtnDeleteProfile"] = new()
            {
                [LanguageCode.ZhCn] = "🗑️ 删除当前配置",
                [LanguageCode.ZhTw] = "🗑️ 刪除當前配置",
                [LanguageCode.En] = "🗑️ Delete Profile",
                [LanguageCode.Ja] = "🗑️ 設定を削除"
            },
            ["SectorCountOptionTitle"] = new()
            {
                [LanguageCode.ZhCn] = "扇区方位数量 (Sector Count)",
                [LanguageCode.ZhTw] = "扇區方位數量 (Sector Count)",
                [LanguageCode.En] = "Sector Count",
                [LanguageCode.Ja] = "セクター数（キー数）"
            },
            ["SectorCountOptionDesc"] = new()
            {
                [LanguageCode.ZhCn] = "切换手势轮盘的切分数量。4 键最快最不易误触，8 键为标准全能方位，12 键适合功能密集场景。",
                [LanguageCode.ZhTw] = "切換手勢輪盤的切分數量。4 鍵最快最不易誤觸，8 鍵為標準全能方位，12 鍵適合功能密集場景。",
                [LanguageCode.En] = "Switch sector counts: 4-way for fast blind flicks, 8-way for balanced productivity, 12-way for high-density actions.",
                [LanguageCode.Ja] = "セクター数を切り替えます。4キー（誤操作防止）、8キー（標準全方位）、12キー（高密度機能）。"
            },
            ["SectorActionListTitle"] = new()
            {
                [LanguageCode.ZhCn] = "扇区动作映射列表",
                [LanguageCode.ZhTw] = "扇區動作對應列表",
                [LanguageCode.En] = "Sector Action Mappings",
                [LanguageCode.Ja] = "セクターアクションマッピング"
            },
            ["SectorActionListDesc"] = new()
            {
                [LanguageCode.ZhCn] = "为每个方位指定触发动作与图标。支持热键组合（如 Ctrl+C）、启动本地程序、打开文件夹与系统级操作。",
                [LanguageCode.ZhTw] = "為每個方位指定觸發動作與圖示。支援快捷熱鍵組合（如 Ctrl+C）、啟動本地程式、開啟資料夾與系統級操作。",
                [LanguageCode.En] = "Assign actions and icons for each sector. Supports hotkeys (e.g. Ctrl+C), app launching, folder opening, and system actions.",
                [LanguageCode.Ja] = "各方向の動作とアイコンを設定します。ショートカット（Ctrl+C等）、アプリ起動、フォルダー、システム制御に対応。"
            },
            ["IconPickerTitle"] = new()
            {
                [LanguageCode.ZhCn] = "选择动作矢量图标",
                [LanguageCode.ZhTw] = "選擇動作向量圖示",
                [LanguageCode.En] = "Select Vector Icon",
                [LanguageCode.Ja] = "ベクターアイコンを選択"
            },
            ["IconPickerHeader"] = new()
            {
                [LanguageCode.ZhCn] = "选择扇区动作矢量图标",
                [LanguageCode.ZhTw] = "選擇扇區動作向量圖示",
                [LanguageCode.En] = "Select Sector Vector Icon",
                [LanguageCode.Ja] = "セクターアイコンを選択"
            },
            ["IconPickerSubtitle"] = new()
            {
                [LanguageCode.ZhCn] = "精选 30+ 常用高保真矢量图形，支持在不同分辨率及 DPI 下无损清晰渲染。",
                [LanguageCode.ZhTw] = "精選 30+ 常用高保真向量圖形，支援在不同解析度及 DPI 下無損清晰渲染。",
                [LanguageCode.En] = "30+ high-fidelity vector icons with lossless crisp rendering across all DPI displays.",
                [LanguageCode.Ja] = "30種類以上の高精細ベクターアイコン。あらゆるDPIで美しく描画されます。"
            },
            ["IconPickerSearchTooltip"] = new()
            {
                [LanguageCode.ZhCn] = "输入图标名称或分类进行快速过滤...",
                [LanguageCode.ZhTw] = "輸入圖示名稱或分類進行快速篩選...",
                [LanguageCode.En] = "Search icon name or category...",
                [LanguageCode.Ja] = "アイコン名またはカテゴリで検索..."
            },
            ["IconPickerClear"] = new()
            {
                [LanguageCode.ZhCn] = "清空图标 (无图标)",
                [LanguageCode.ZhTw] = "清空圖示 (無圖示)",
                [LanguageCode.En] = "Clear Icon (No Icon)",
                [LanguageCode.Ja] = "アイコンをクリア (なし)"
            },
            ["IconPickerSelected"] = new()
            {
                [LanguageCode.ZhCn] = "已选图标:",
                [LanguageCode.ZhTw] = "已選圖示:",
                [LanguageCode.En] = "Selected Icon:",
                [LanguageCode.Ja] = "選択中のアイコン:"
            },
            ["IconPickerNone"] = new()
            {
                [LanguageCode.ZhCn] = "(未选择)",
                [LanguageCode.ZhTw] = "(未選擇)",
                [LanguageCode.En] = "(None)",
                [LanguageCode.Ja] = "(未選択)"
            },
            ["ColorPickerTitle"] = new()
            {
                [LanguageCode.ZhCn] = "色彩选择器与屏幕吸管 (Color Picker)",
                [LanguageCode.ZhTw] = "色彩選擇器與螢幕吸管 (Color Picker)",
                [LanguageCode.En] = "Color Picker & Eyedropper",
                [LanguageCode.Ja] = "カラーピッカー＆スポイト"
            },
            ["ColorPickerHue"] = new()
            {
                [LanguageCode.ZhCn] = "色相",
                [LanguageCode.ZhTw] = "色相",
                [LanguageCode.En] = "Hue",
                [LanguageCode.Ja] = "色相"
            },
            ["ColorPickerAlpha"] = new()
            {
                [LanguageCode.ZhCn] = "不透明",
                [LanguageCode.ZhTw] = "不透明",
                [LanguageCode.En] = "Opacity",
                [LanguageCode.Ja] = "不透明度"
            },
            ["ColorPickerEyedropperTitle"] = new()
            {
                [LanguageCode.ZhCn] = "🔍 屏幕取色吸管",
                [LanguageCode.ZhTw] = "🔍 螢幕取色吸管",
                [LanguageCode.En] = "🔍 Screen Eyedropper",
                [LanguageCode.Ja] = "🔍 画面スポイト"
            },
            ["ColorPickerEyedropperDesc"] = new()
            {
                [LanguageCode.ZhCn] = "点击后在屏幕任意窗口吸取精准色彩",
                [LanguageCode.ZhTw] = "點擊後在螢幕任意視窗吸取精準色彩",
                [LanguageCode.En] = "Pick color accurately from any window or desktop on screen",
                [LanguageCode.Ja] = "画面上の任意のウィンドウから正確な色を抽出します"
            },
            ["ColorPickerEyedropperBtn"] = new()
            {
                [LanguageCode.ZhCn] = "从屏幕吸色",
                [LanguageCode.ZhTw] = "從螢幕吸色",
                [LanguageCode.En] = "Pick Color",
                [LanguageCode.Ja] = "画面から吸色"
            },
            ["ColorPickerSwatches"] = new()
            {
                [LanguageCode.ZhCn] = "预设经典配色卡 (Quick Swatches - 滚轮滚动查看全部):",
                [LanguageCode.ZhTw] = "預設經典配色卡 (Quick Swatches - 滾輪滾動查看全部):",
                [LanguageCode.En] = "Preset Color Swatches (Scroll to browse):",
                [LanguageCode.Ja] = "プリセットカラーパレット (スクロールで全表示):"
            },
            ["ColorPickerApply"] = new()
            {
                [LanguageCode.ZhCn] = "应用色彩",
                [LanguageCode.ZhTw] = "套用色彩",
                [LanguageCode.En] = "Apply Color",
                [LanguageCode.Ja] = "色を適用"
            },
            ["InputDialogTitle"] = new()
            {
                [LanguageCode.ZhCn] = "配置方案 - StarPie",
                [LanguageCode.ZhTw] = "配置方案 - StarPie",
                [LanguageCode.En] = "Profile - StarPie",
                [LanguageCode.Ja] = "プロファイル - StarPie"
            },
            ["InputDialogEmpty"] = new()
            {
                [LanguageCode.ZhCn] = "名称不能为空，请输入有效的配置名称。",
                [LanguageCode.ZhTw] = "名稱不能為空，請輸入有效的配置名稱。",
                [LanguageCode.En] = "Name cannot be empty. Please enter a valid profile name.",
                [LanguageCode.Ja] = "名前を入力してください。"
            },
            ["Notice"] = new()
            {
                [LanguageCode.ZhCn] = "提示",
                [LanguageCode.ZhTw] = "提示",
                [LanguageCode.En] = "Notice",
                [LanguageCode.Ja] = "お知らせ"
            },
            ["Error"] = new()
            {
                [LanguageCode.ZhCn] = "错误",
                [LanguageCode.ZhTw] = "錯誤",
                [LanguageCode.En] = "Error",
                [LanguageCode.Ja] = "エラー"
            },

            // App Brand & Headers
            ["AppName"] = new()
            {
                [LanguageCode.ZhCn] = "StarPie",
                [LanguageCode.ZhTw] = "StarPie",
                [LanguageCode.En] = "StarPie",
                [LanguageCode.Ja] = "StarPie"
            },
            ["AppSubtitle"] = new()
            {
                [LanguageCode.ZhCn] = "现代鼠标轮盘笔势系统",
                [LanguageCode.ZhTw] = "現代滑鼠輪盤手勢系統",
                [LanguageCode.En] = "Modern Mouse Radial Gestures",
                [LanguageCode.Ja] = "次世代マウスラジアルジェスチャー"
            },
            ["WindowTitle"] = new()
            {
                [LanguageCode.ZhCn] = "StarPie 设置控制台 (Preferences)",
                [LanguageCode.ZhTw] = "StarPie 設定控制台 (Preferences)",
                [LanguageCode.En] = "StarPie Preferences Console",
                [LanguageCode.Ja] = "StarPie 環境設定コンソール"
            },

            // Sidebar Tabs
            ["TabTrigger"] = new()
            {
                [LanguageCode.ZhCn] = "🎯 触发与场景",
                [LanguageCode.ZhTw] = "🎯 觸發與場景",
                [LanguageCode.En] = "🎯 Trigger & Scenes",
                [LanguageCode.Ja] = "🎯 トリガーとシーン"
            },
            ["TabAppearance"] = new()
            {
                [LanguageCode.ZhCn] = "🎨 外观与形态",
                [LanguageCode.ZhTw] = "🎨 外觀與形態",
                [LanguageCode.En] = "🎨 Appearance & Shapes",
                [LanguageCode.Ja] = "🎨 外観と形状"
            },
            ["TabGestures"] = new()
            {
                [LanguageCode.ZhCn] = "⚡ 手势与动作",
                [LanguageCode.ZhTw] = "⚡ 手勢與動作",
                [LanguageCode.En] = "⚡ Gestures & Actions",
                [LanguageCode.Ja] = "⚡ ジェスチャーと動作"
            },
            ["TabAdvanced"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 高级与系统",
                [LanguageCode.ZhTw] = "⚙️ 進階與系統",
                [LanguageCode.En] = "⚙️ Advanced & System",
                [LanguageCode.Ja] = "⚙️ 高度な設定とシステム"
            },
            ["TabAbout"] = new()
            {
                [LanguageCode.ZhCn] = "📋 关于与更新",
                [LanguageCode.ZhTw] = "📋 關於與更新",
                [LanguageCode.En] = "📋 About & Updates",
                [LanguageCode.Ja] = "📋 情報と更新"
            },

            // Bottom Bar
            ["BottomStatusNote"] = new()
            {
                [LanguageCode.ZhCn] = "注: 所有修改均在内存中即时生效，点击【保存更改】持久化保存至硬盘。",
                [LanguageCode.ZhTw] = "註: 所有修改均在記憶體中即時生效，點擊【儲存變更】持久化儲存至硬碟。",
                [LanguageCode.En] = "Note: All changes take effect in memory immediately. Click [Save Changes] to persist to disk.",
                [LanguageCode.Ja] = "注: 変更はメモリ上で即座に有効になります。[変更を保存] で設定ファイルに永続化されます。"
            },
            ["BtnSave"] = new()
            {
                [LanguageCode.ZhCn] = "保存更改",
                [LanguageCode.ZhTw] = "儲存變更",
                [LanguageCode.En] = "Save Changes",
                [LanguageCode.Ja] = "変更を保存"
            },
            ["BtnClose"] = new()
            {
                [LanguageCode.ZhCn] = "关闭并隐藏",
                [LanguageCode.ZhTw] = "關閉並隱藏",
                [LanguageCode.En] = "Close & Hide",
                [LanguageCode.Ja] = "閉じて隠す"
            },

            // Tab 0: Trigger & Scenes
            ["TriggerHeader"] = new()
            {
                [LanguageCode.ZhCn] = "触发与场景隔离设置",
                [LanguageCode.ZhTw] = "觸發與場景隔離設定",
                [LanguageCode.En] = "Trigger & Scene Isolation",
                [LanguageCode.Ja] = "トリガーとシーンの分離設定"
            },
            ["TriggerSubheader"] = new()
            {
                [LanguageCode.ZhCn] = "在此配置全局鼠标手势的触发灵敏度、全屏游戏自动拦截与排除程序黑名单。",
                [LanguageCode.ZhTw] = "在此配置全域滑鼠手勢的觸發靈敏度、全螢幕遊戲自動攔截與排除程式黑名單。",
                [LanguageCode.En] = "Configure mouse gesture sensitivity, full-screen gaming bypass, and exclusion blacklist.",
                [LanguageCode.Ja] = "マウスジェスチャーの感度、フルスクリーンゲームでの自動回避、除外プロセスを設定します。"
            },
            ["SensitivityTitle"] = new()
            {
                [LanguageCode.ZhCn] = "手势触发灵敏度",
                [LanguageCode.ZhTw] = "手勢觸發靈敏度",
                [LanguageCode.En] = "Trigger Sensitivity",
                [LanguageCode.Ja] = "ジェスチャー起動感度"
            },
            ["SensitivityDesc"] = new()
            {
                [LanguageCode.ZhCn] = "按住鼠标右键移动超过指定像素距离后呼出手势轮盘。距离越小越灵敏，过小可能造成右键微抖动误触。",
                [LanguageCode.ZhTw] = "按住滑鼠右鍵移動超過指定像素距離後呼出手勢輪盤。距離越小越靈敏，過小可能造成右鍵微抖動誤觸。",
                [LanguageCode.En] = "Hold right-click and move beyond this pixel distance to trigger radial menu. Lower values are more sensitive.",
                [LanguageCode.Ja] = "右クリックを押しながら指定ピクセル以上移動するとホイールを呼び出します。値が小さいほど高感度です。"
            },
            ["SceneIsolationTitle"] = new()
            {
                [LanguageCode.ZhCn] = "场景隔离与防误触",
                [LanguageCode.ZhTw] = "場景隔離與防誤觸",
                [LanguageCode.En] = "Scene Isolation & Guard",
                [LanguageCode.Ja] = "シーン分離と誤操作防止"
            },
            ["SceneIsolationDesc"] = new()
            {
                [LanguageCode.ZhCn] = "当处于特定场景或配合修饰键操作时，自动绕过轮盘拦截，放行原生右键事件。",
                [LanguageCode.ZhTw] = "當處於特定場景或配合修飾鍵操作時，自動繞過輪盤攔截，放行原生右鍵事件。",
                [LanguageCode.En] = "Automatically bypass radial menu and pass-through native right-click in specific scenarios.",
                [LanguageCode.Ja] = "特定の環境や修飾キー操作時にホイールを無効化し、通常の右クリックを通過させます。"
            },
            ["FullScreenOption"] = new()
            {
                [LanguageCode.ZhCn] = "全屏游戏/独占应用自动禁用手势",
                [LanguageCode.ZhTw] = "全螢幕遊戲/獨佔應用自動禁用手勢",
                [LanguageCode.En] = "Auto-disable in Full-screen games / Exclusive apps",
                [LanguageCode.Ja] = "全画面ゲーム/専用アプリでジェスチャーを自動無効化"
            },
            ["FullScreenOptionDesc"] = new()
            {
                [LanguageCode.ZhCn] = "自动检测当前前台窗口是否处于全屏独占状态，避免游戏瞄准等右键操作被拦截。",
                [LanguageCode.ZhTw] = "自動檢測當前前台視窗是否處於全螢幕獨佔狀態，避免遊戲瞄準等右鍵操作被攔截。",
                [LanguageCode.En] = "Detects whether active window is running in full-screen to avoid intercepting gaming right-clicks.",
                [LanguageCode.Ja] = "アクティブなウィンドウが全画面かどうかを検知し、ゲームの照準等の右クリック操作を邪魔しません。"
            },
            ["ModifierPassTitle"] = new()
            {
                [LanguageCode.ZhCn] = "快捷键旁路穿透 (按住以下修饰键拖拽时不触发手势):",
                [LanguageCode.ZhTw] = "快速鍵旁路穿透 (按住以下修飾鍵拖曳時不觸發手勢):",
                [LanguageCode.En] = "Modifier Pass-Through (hold to bypass gestures):",
                [LanguageCode.Ja] = "修飾キーバイパス (押下中はジェスチャーを無効化):"
            },
            ["ModifierCtrl"] = new()
            {
                [LanguageCode.ZhCn] = "按住 Ctrl 键时旁路",
                [LanguageCode.ZhTw] = "按住 Ctrl 鍵時旁路",
                [LanguageCode.En] = "Bypass on Ctrl",
                [LanguageCode.Ja] = "Ctrl 押下時にバイパス"
            },
            ["ModifierShift"] = new()
            {
                [LanguageCode.ZhCn] = "按住 Shift 键时旁路",
                [LanguageCode.ZhTw] = "按住 Shift 鍵時旁路",
                [LanguageCode.En] = "Bypass on Shift",
                [LanguageCode.Ja] = "Shift 押下時にバイパス"
            },
            ["ModifierAlt"] = new()
            {
                [LanguageCode.ZhCn] = "按住 Alt 键时旁路",
                [LanguageCode.ZhTw] = "按住 Alt 鍵時旁路",
                [LanguageCode.En] = "Bypass on Alt",
                [LanguageCode.Ja] = "Alt 押下時にバイパス"
            },
            ["BlacklistTitle"] = new()
            {
                [LanguageCode.ZhCn] = "进程排除黑名单",
                [LanguageCode.ZhTw] = "行程排除黑名單",
                [LanguageCode.En] = "Process Exclusion Blacklist",
                [LanguageCode.Ja] = "除外プロセスブラックリスト"
            },
            ["BlacklistDesc"] = new()
            {
                [LanguageCode.ZhCn] = "在排除名单中的应用程序（如远程桌面、画图、3D建模软件）中，完全放行鼠标右键。",
                [LanguageCode.ZhTw] = "在排除名單中的應用程式（如遠端桌面、小畫家、3D建模軟體）中，完全放行滑鼠右鍵。",
                [LanguageCode.En] = "Native right-click is fully allowed within blacklisted applications (e.g. Remote Desktop, Paint, CAD).",
                [LanguageCode.Ja] = "登録されたアプリ（リモートデスクトップ、ペイント、3Dモデリング等）では右クリックを直接通します。"
            },
            ["BtnAddProcess"] = new()
            {
                [LanguageCode.ZhCn] = "➕ 添加进程",
                [LanguageCode.ZhTw] = "➕ 新增處理程序",
                [LanguageCode.En] = "➕ Add Process",
                [LanguageCode.Ja] = "➕ プロセス追加"
            },
            ["BtnPickProcess"] = new()
            {
                [LanguageCode.ZhCn] = "🔍 选择应用...",
                [LanguageCode.ZhTw] = "🔍 選擇應用程式...",
                [LanguageCode.En] = "🔍 Select App...",
                [LanguageCode.Ja] = "🔍 アプリを選択..."
            },
            ["BtnDeleteProcess"] = new()
            {
                [LanguageCode.ZhCn] = "🗑️ 移除选中",
                [LanguageCode.ZhTw] = "🗑️ 移除選取",
                [LanguageCode.En] = "🗑️ Remove Selected",
                [LanguageCode.Ja] = "🗑️ 選択項目を削除"
            },
            ["BlacklistPlaceholder"] = new()
            {
                [LanguageCode.ZhCn] = "输入进程名称 (如 solidworks.exe) 或点击右侧选择应用...",
                [LanguageCode.ZhTw] = "輸入處理程序名稱 (如 solidworks.exe) 或點擊右側選擇應用程式...",
                [LanguageCode.En] = "Enter process name (e.g. solidworks.exe) or browse...",
                [LanguageCode.Ja] = "プロセス名を入力 (例: solidworks.exe) またはアプリを選択..."
            },

            // Tab 1: Appearance & Shapes
            ["AppearanceHeader"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘外观与形态定制",
                [LanguageCode.ZhTw] = "輪盤外觀與形態自訂",
                [LanguageCode.En] = "Appearance & Shapes Customization",
                [LanguageCode.Ja] = "外観と形状のカスタマイズ"
            },
            ["AppearanceSubheader"] = new()
            {
                [LanguageCode.ZhCn] = "自由配置轮盘视觉风格、配色方案、高亮边缘光晕、几何切削、图标排版与中心核圆贴图。",
                [LanguageCode.ZhTw] = "自由配置輪盤視覺風格、配色方案、高亮邊緣光暈、幾何切削、圖示排版與中心核圓貼圖。",
                [LanguageCode.En] = "Customize visual styles, color palettes, highlight glow, geometry shapes, typography, and core image.",
                [LanguageCode.Ja] = "ビジュアルスタイル、配色テーマ、グロー発光、幾何学形状、アイコン配置、コアバッジをカスタマイズします。"
            },
            ["StyleTitle"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘渲染风格 (Visual Renderer)",
                [LanguageCode.ZhTw] = "輪盤渲染風格 (Visual Renderer)",
                [LanguageCode.En] = "Visual Renderer Style",
                [LanguageCode.Ja] = "ビジュアルレンダラー"
            },
            ["StyleGlass"] = new()
            {
                [LanguageCode.ZhCn] = "液态毛玻璃 (Glassmorphism)",
                [LanguageCode.ZhTw] = "液態毛玻璃 (Glassmorphism)",
                [LanguageCode.En] = "Liquid Glassmorphism",
                [LanguageCode.Ja] = "リキッドグラスモーフィズム"
            },
            ["StyleClassic"] = new()
            {
                [LanguageCode.ZhCn] = "经典圆环 (Classic Ring)",
                [LanguageCode.ZhTw] = "經典圓環 (Classic Ring)",
                [LanguageCode.En] = "Classic Ring",
                [LanguageCode.Ja] = "クラシックリング"
            },
            ["StyleClean"] = new()
            {
                [LanguageCode.ZhCn] = "极简扇区 (Clean Sectors)",
                [LanguageCode.ZhTw] = "極簡扇區 (Clean Sectors)",
                [LanguageCode.En] = "Clean Sectors",
                [LanguageCode.Ja] = "クリーンセクター"
            },
            ["StyleCatPaw"] = new()
            {
                [LanguageCode.ZhCn] = "萌宠猫爪 (Cute Cat Paw)",
                [LanguageCode.ZhTw] = "萌寵貓爪 (Cute Cat Paw)",
                [LanguageCode.En] = "Cute Cat Paw",
                [LanguageCode.Ja] = "キュートキャットポー (肉球)"
            },
            ["ThemeTitle"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘配色方案 (Color Palette)",
                [LanguageCode.ZhTw] = "輪盤配色方案 (Color Palette)",
                [LanguageCode.En] = "Wheel Color Palette",
                [LanguageCode.Ja] = "ホイール配色パレット"
            },
            ["BtnDeletePreset"] = new()
            {
                [LanguageCode.ZhCn] = "🗑️ 删除预设",
                [LanguageCode.ZhTw] = "🗑️ 刪除預設",
                [LanguageCode.En] = "🗑️ Delete Preset",
                [LanguageCode.Ja] = "🗑️ プリセット削除"
            },
            ["GlowTitle"] = new()
            {
                [LanguageCode.ZhCn] = "高亮边缘光晕 (Highlight Edge Glow)",
                [LanguageCode.ZhTw] = "高亮邊緣光暈 (Highlight Edge Glow)",
                [LanguageCode.En] = "Highlight Edge Glow",
                [LanguageCode.Ja] = "ハイライトエッジグロー発光"
            },
            ["GlowFollowTheme"] = new()
            {
                [LanguageCode.ZhCn] = "跟随主题高亮色 (Auto)",
                [LanguageCode.ZhTw] = "跟隨主題高亮色 (Auto)",
                [LanguageCode.En] = "Follow Theme (Auto)",
                [LanguageCode.Ja] = "テーマ連動 (自動)"
            },
            ["GlowRadius"] = new()
            {
                [LanguageCode.ZhCn] = "光晕弥散半径 (Glow Radius)",
                [LanguageCode.ZhTw] = "光暈彌散半徑 (Glow Radius)",
                [LanguageCode.En] = "Glow Radius",
                [LanguageCode.Ja] = "グロー拡散半径"
            },
            ["GlowOpacity"] = new()
            {
                [LanguageCode.ZhCn] = "光晕不透明度 (Glow Opacity)",
                [LanguageCode.ZhTw] = "光暈不透明度 (Glow Opacity)",
                [LanguageCode.En] = "Glow Opacity",
                [LanguageCode.Ja] = "グロー不透明度"
            },
            ["GeometryTitle"] = new()
            {
                [LanguageCode.ZhCn] = "几何形态与尺寸 (Geometry & Dimensions)",
                [LanguageCode.ZhTw] = "幾何形態與尺寸 (Geometry & Dimensions)",
                [LanguageCode.En] = "Geometry & Dimensions",
                [LanguageCode.Ja] = "幾何学形状とサイズ"
            },
            ["ShapeOriginal"] = new()
            {
                [LanguageCode.ZhCn] = "原生扇区 (Original Sector)",
                [LanguageCode.ZhTw] = "原生扇區 (Original Sector)",
                [LanguageCode.En] = "Original Sector",
                [LanguageCode.Ja] = "オリジナルセクター"
            },
            ["ShapeCircle"] = new()
            {
                [LanguageCode.ZhCn] = "极简圆形 (Floating Circle)",
                [LanguageCode.ZhTw] = "極簡圓形 (Floating Circle)",
                [LanguageCode.En] = "Floating Circle",
                [LanguageCode.Ja] = "フローティングサークル"
            },
            ["ShapeRounded"] = new()
            {
                [LanguageCode.ZhCn] = "平滑圆角 (Rounded Fillet)",
                [LanguageCode.ZhTw] = "平滑圓角 (Rounded Fillet)",
                [LanguageCode.En] = "Rounded Fillet",
                [LanguageCode.Ja] = "角丸フィレット"
            },
            ["ShapeCapsule"] = new()
            {
                [LanguageCode.ZhCn] = "圆润胶囊 (Pill Capsules)",
                [LanguageCode.ZhTw] = "圓潤膠囊 (Pill Capsules)",
                [LanguageCode.En] = "Pill Capsules",
                [LanguageCode.Ja] = "ピルカプセル"
            },
            ["ShapeHexagon"] = new()
            {
                [LanguageCode.ZhCn] = "未来蜂巢 (Hexagon Hive)",
                [LanguageCode.ZhTw] = "未來蜂巢 (Hexagon Hive)",
                [LanguageCode.En] = "Hexagon Hive",
                [LanguageCode.Ja] = "ヘキサゴンハニカム"
            },
            ["RadiusOuter"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘外半径 (Outer Radius)",
                [LanguageCode.ZhTw] = "輪盤外半徑 (Outer Radius)",
                [LanguageCode.En] = "Outer Radius",
                [LanguageCode.Ja] = "外側半径"
            },
            ["RadiusInner"] = new()
            {
                [LanguageCode.ZhCn] = "内环半径 (Inner Radius)",
                [LanguageCode.ZhTw] = "內環半徑 (Inner Radius)",
                [LanguageCode.En] = "Inner Radius",
                [LanguageCode.Ja] = "内側半径"
            },
            ["RadiusCore"] = new()
            {
                [LanguageCode.ZhCn] = "核圆半径 (Core Radius)",
                [LanguageCode.ZhTw] = "核圓半徑 (Core Radius)",
                [LanguageCode.En] = "Core Radius",
                [LanguageCode.Ja] = "コア半径"
            },
            ["SectorGap"] = new()
            {
                [LanguageCode.ZhCn] = "扇区间隙 (Sector Gap)",
                [LanguageCode.ZhTw] = "扇區間隙 (Sector Gap)",
                [LanguageCode.En] = "Sector Gap",
                [LanguageCode.Ja] = "セクター間隔"
            },
            ["SectorCornerRadius"] = new()
            {
                [LanguageCode.ZhCn] = "扇区倒角 (Corner Radius)",
                [LanguageCode.ZhTw] = "扇區倒角 (Corner Radius)",
                [LanguageCode.En] = "Corner Radius",
                [LanguageCode.Ja] = "角丸半径"
            },
            ["BtnResetGeometry"] = new()
            {
                [LanguageCode.ZhCn] = "重置形态默认值",
                [LanguageCode.ZhTw] = "重設形態預設值",
                [LanguageCode.En] = "Reset Geometry Defaults",
                [LanguageCode.Ja] = "形状初期値に戻す"
            },
            ["IconLayoutTitle"] = new()
            {
                [LanguageCode.ZhCn] = "图标与文字排版 (Layout & Typography)",
                [LanguageCode.ZhTw] = "圖示與文字排版 (Layout & Typography)",
                [LanguageCode.En] = "Layout & Typography",
                [LanguageCode.Ja] = "レイアウトと文字"
            },
            ["LayoutIconText"] = new()
            {
                [LanguageCode.ZhCn] = "图文并茂 (Icon + Text)",
                [LanguageCode.ZhTw] = "圖文並茂 (Icon + Text)",
                [LanguageCode.En] = "Icon & Text",
                [LanguageCode.Ja] = "アイコン＋文字"
            },
            ["LayoutIconOnly"] = new()
            {
                [LanguageCode.ZhCn] = "仅显示图标 (Icon Only)",
                [LanguageCode.ZhTw] = "僅顯示圖示 (Icon Only)",
                [LanguageCode.En] = "Icon Only",
                [LanguageCode.Ja] = "アイコンのみ"
            },
            ["LayoutTextOnly"] = new()
            {
                [LanguageCode.ZhCn] = "仅显示文字 (Text Only)",
                [LanguageCode.ZhTw] = "僅顯示文字 (Text Only)",
                [LanguageCode.En] = "Text Only",
                [LanguageCode.Ja] = "文字のみ"
            },
            ["SectorIconSize"] = new()
            {
                [LanguageCode.ZhCn] = "图标大小 (Icon Size)",
                [LanguageCode.ZhTw] = "圖示大小 (Icon Size)",
                [LanguageCode.En] = "Icon Size",
                [LanguageCode.Ja] = "アイコンサイズ"
            },
            ["SectorFontSize"] = new()
            {
                [LanguageCode.ZhCn] = "文字字号 (Font Size)",
                [LanguageCode.ZhTw] = "文字字級 (Font Size)",
                [LanguageCode.En] = "Font Size",
                [LanguageCode.Ja] = "文字サイズ"
            },
            ["CoreTitle"] = new()
            {
                [LanguageCode.ZhCn] = "中心核圆图案定制 (Center Core Customization)",
                [LanguageCode.ZhTw] = "中心核圓圖案自訂 (Center Core Customization)",
                [LanguageCode.En] = "Center Core Customization",
                [LanguageCode.Ja] = "中央コアのカスタマイズ"
            },
            ["CoreShowIcon"] = new()
            {
                [LanguageCode.ZhCn] = "显示中心图案 / 贴图",
                [LanguageCode.ZhTw] = "顯示中心圖案 / 貼圖",
                [LanguageCode.En] = "Show Core Icon / Image",
                [LanguageCode.Ja] = "中央アイコン/画像を表示"
            },
            ["CoreIconType"] = new()
            {
                [LanguageCode.ZhCn] = "核圆图案模式",
                [LanguageCode.ZhTw] = "核圓圖案模式",
                [LanguageCode.En] = "Core Pattern Mode",
                [LanguageCode.Ja] = "コアパターンモード"
            },
            ["CorePatternExit"] = new()
            {
                [LanguageCode.ZhCn] = "取消叉号 (Cancel Cross)",
                [LanguageCode.ZhTw] = "取消叉號 (Cancel Cross)",
                [LanguageCode.En] = "Cancel Cross",
                [LanguageCode.Ja] = "キャンセルバツ"
            },
            ["CorePatternCrosshair"] = new()
            {
                [LanguageCode.ZhCn] = "精准准心 (Crosshair)",
                [LanguageCode.ZhTw] = "精準準心 (Crosshair)",
                [LanguageCode.En] = "Crosshair",
                [LanguageCode.Ja] = "照準レティクル"
            },
            ["CorePatternWindows"] = new()
            {
                [LanguageCode.ZhCn] = "Windows 微标",
                [LanguageCode.ZhTw] = "Windows 微標",
                [LanguageCode.En] = "Windows Emblem",
                [LanguageCode.Ja] = "Windows ロゴ"
            },
            ["CorePatternDot"] = new()
            {
                [LanguageCode.ZhCn] = "极简圆点 (Minimal Dot)",
                [LanguageCode.ZhTw] = "極簡圓點 (Minimal Dot)",
                [LanguageCode.En] = "Minimal Dot",
                [LanguageCode.Ja] = "ミニマルドット"
            },
            ["CorePatternHome"] = new()
            {
                [LanguageCode.ZhCn] = "主页图标 (Home)",
                [LanguageCode.ZhTw] = "首頁圖示 (Home)",
                [LanguageCode.En] = "Home",
                [LanguageCode.Ja] = "ホーム"
            },
            ["CorePatternPower"] = new()
            {
                [LanguageCode.ZhCn] = "电源图标 (Power)",
                [LanguageCode.ZhTw] = "電源圖示 (Power)",
                [LanguageCode.En] = "Power",
                [LanguageCode.Ja] = "電源"
            },
            ["CorePatternCompass"] = new()
            {
                [LanguageCode.ZhCn] = "星空罗盘 (Compass)",
                [LanguageCode.ZhTw] = "星空羅盤 (Compass)",
                [LanguageCode.En] = "Compass",
                [LanguageCode.Ja] = "コンパス"
            },
            ["CorePatternCatPaw"] = new()
            {
                [LanguageCode.ZhCn] = "萌宠猫爪 (Cat Paw)",
                [LanguageCode.ZhTw] = "萌寵貓爪 (Cat Paw)",
                [LanguageCode.En] = "Cat Paw",
                [LanguageCode.Ja] = "肉球"
            },
            ["CorePatternImage"] = new()
            {
                [LanguageCode.ZhCn] = "🖼️ 自定义本地图片贴图...",
                [LanguageCode.ZhTw] = "🖼️ 自訂本機圖片貼圖...",
                [LanguageCode.En] = "🖼️ Custom Local Image...",
                [LanguageCode.Ja] = "🖼️ カスタム画像ファイル..."
            },
            ["BtnBrowseImage"] = new()
            {
                [LanguageCode.ZhCn] = "浏览选择图片",
                [LanguageCode.ZhTw] = "瀏覽選擇圖片",
                [LanguageCode.En] = "Browse Image",
                [LanguageCode.Ja] = "画像を選択"
            },
            ["ConsoleThemeTitle"] = new()
            {
                [LanguageCode.ZhCn] = "软件控制台主题 (Console Theme)",
                [LanguageCode.ZhTw] = "軟體控制台主題 (Console Theme)",
                [LanguageCode.En] = "Console Theme",
                [LanguageCode.Ja] = "コントロールパネルテーマ"
            },
            ["ThemeSystem"] = new()
            {
                [LanguageCode.ZhCn] = "🖥️ 跟随 Windows 系统 (Auto)",
                [LanguageCode.ZhTw] = "🖥️ 跟隨 Windows 系統 (Auto)",
                [LanguageCode.En] = "🖥️ Follow Windows System",
                [LanguageCode.Ja] = "🖥️ Windows システムに従う"
            },
            ["ThemeLight"] = new()
            {
                [LanguageCode.ZhCn] = "☀️ 极简纯白 (Pure Light)",
                [LanguageCode.ZhTw] = "☀️ 極簡純白 (Pure Light)",
                [LanguageCode.En] = "☀️ Pure Light",
                [LanguageCode.Ja] = "☀️ ピュアライト"
            },
            ["ThemeDark"] = new()
            {
                [LanguageCode.ZhCn] = "🌙 极夜曜黑 (Oled Dark)",
                [LanguageCode.ZhTw] = "🌙 極夜曜黑 (Oled Dark)",
                [LanguageCode.En] = "🌙 OLED Dark",
                [LanguageCode.Ja] = "🌙 OLEDダーク"
            },
            ["ThemeNavy"] = new()
            {
                [LanguageCode.ZhCn] = "🌌 午夜深蓝 (Midnight Navy)",
                [LanguageCode.ZhTw] = "🌌 午夜深藍 (Midnight Navy)",
                [LanguageCode.En] = "🌌 Midnight Navy",
                [LanguageCode.Ja] = "🌌 ミッドナイトネイビー"
            },
            ["ThemeViolet"] = new()
            {
                [LanguageCode.ZhCn] = "🔮 暗夜紫罗兰 (Royal Violet)",
                [LanguageCode.ZhTw] = "🔮 暗夜紫羅蘭 (Royal Violet)",
                [LanguageCode.En] = "🔮 Royal Violet",
                [LanguageCode.Ja] = "🔮 ロイヤルバイオレット"
            },
            ["ThemeGray"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 钛金深灰 (Titanium Gray)",
                [LanguageCode.ZhTw] = "⚙️ 鈦金深灰 (Titanium Gray)",
                [LanguageCode.En] = "⚙️ Titanium Gray",
                [LanguageCode.Ja] = "⚙️ チタングレー"
            },

            // Tab 2: Gestures & Actions
            ["GesturesHeader"] = new()
            {
                [LanguageCode.ZhCn] = "手势轮盘分位与动作配置",
                [LanguageCode.ZhTw] = "手勢輪盤分位與動作配置",
                [LanguageCode.En] = "Gesture Sectors & Action Mappings",
                [LanguageCode.Ja] = "セクター配置とアクション設定"
            },
            ["SectorCountTitle"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘方位按键数 (Sector Count)",
                [LanguageCode.ZhTw] = "輪盤方位按鍵數 (Sector Count)",
                [LanguageCode.En] = "Sector Count",
                [LanguageCode.Ja] = "セクター数（キー数）"
            },
            ["SectorCount4"] = new()
            {
                [LanguageCode.ZhCn] = "4 键 (十字方位 / Cross 4-Way)",
                [LanguageCode.ZhTw] = "4 鍵 (十字方位 / Cross 4-Way)",
                [LanguageCode.En] = "4 Sectors (Cross 4-Way)",
                [LanguageCode.Ja] = "4キー (十字方向)"
            },
            ["SectorCount8"] = new()
            {
                [LanguageCode.ZhCn] = "8 键 (八卦全向 / Standard 8-Way)",
                [LanguageCode.ZhTw] = "8 鍵 (八卦全向 / Standard 8-Way)",
                [LanguageCode.En] = "8 Sectors (Standard 8-Way)",
                [LanguageCode.Ja] = "8キー (全方向8方位)"
            },
            ["SectorCount12"] = new()
            {
                [LanguageCode.ZhCn] = "12 键 (钟表表盘 / Clock Dial 12-Way)",
                [LanguageCode.ZhTw] = "12 鍵 (鐘錶錶盤 / Clock Dial 12-Way)",
                [LanguageCode.En] = "12 Sectors (Clock Dial 12-Way)",
                [LanguageCode.Ja] = "12キー (時計盤12方位)"
            },
            ["ActionTypeHotkey"] = new()
            {
                [LanguageCode.ZhCn] = "⌨️ 键盘快捷键",
                [LanguageCode.ZhTw] = "⌨️ 鍵盤快速鍵",
                [LanguageCode.En] = "⌨️ Keyboard Hotkey",
                [LanguageCode.Ja] = "⌨️ キーボードショートカット"
            },
            ["ActionTypeLaunch"] = new()
            {
                [LanguageCode.ZhCn] = "🚀 启动程序/打开网页",
                [LanguageCode.ZhTw] = "🚀 啟動程式/開啟網頁",
                [LanguageCode.En] = "🚀 Launch App / Open URL",
                [LanguageCode.Ja] = "🚀 アプリ起動 / Webを開く"
            },
            ["ActionTypeSystem"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 系统控制指令",
                [LanguageCode.ZhTw] = "⚙️ 系統控制指令",
                [LanguageCode.En] = "⚙️ System Action",
                [LanguageCode.Ja] = "⚙️ システム制御コマンド"
            },
            ["BtnRecordHotkey"] = new()
            {
                [LanguageCode.ZhCn] = "点击录制热键",
                [LanguageCode.ZhTw] = "點擊錄製快速鍵",
                [LanguageCode.En] = "Click to Record Hotkey",
                [LanguageCode.Ja] = "クリックしてショートカット録画"
            },
            ["BtnBrowseApp"] = new()
            {
                [LanguageCode.ZhCn] = "🔍 选择应用程序...",
                [LanguageCode.ZhTw] = "🔍 選擇應用程式...",
                [LanguageCode.En] = "🔍 Select Application...",
                [LanguageCode.Ja] = "🔍 アプリケーションを選択..."
            },

            // Tab 3: Advanced & System
            ["AdvancedHeader"] = new()
            {
                [LanguageCode.ZhCn] = "系统集成与高级偏好设置",
                [LanguageCode.ZhTw] = "系統整合與進階偏好設定",
                [LanguageCode.En] = "System Integration & Preferences",
                [LanguageCode.Ja] = "システム統合と高度な設定"
            },
            ["LanguageTitle"] = new()
            {
                [LanguageCode.ZhCn] = "界面语言 (Display Language)",
                [LanguageCode.ZhTw] = "介面語言 (Display Language)",
                [LanguageCode.En] = "Display Language",
                [LanguageCode.Ja] = "表示言語 (Display Language)"
            },
            ["LanguageDesc"] = new()
            {
                [LanguageCode.ZhCn] = "选择软件控制台与轮盘的显示语言，支持即时热切换并自动保存。",
                [LanguageCode.ZhTw] = "選擇軟體控制台與輪盤的顯示語言，支援即時熱切換並自動儲存。",
                [LanguageCode.En] = "Select language for StarPie. Applies immediately without restarting.",
                [LanguageCode.Ja] = "StarPieの表示言語を選択します。再起動不要で即時に切り替わります。"
            },

            // Program Picker Dialog
            ["ProgramPickerTitle"] = new()
            {
                [LanguageCode.ZhCn] = "选择程序",
                [LanguageCode.ZhTw] = "選擇程式",
                [LanguageCode.En] = "Select Program",
                [LanguageCode.Ja] = "プログラムを選択"
            },
            ["ProgramPickerHeader"] = new()
            {
                [LanguageCode.ZhCn] = "从已安装的软件和开始菜单中选择",
                [LanguageCode.ZhTw] = "從已安裝的軟體與開始功能表中選擇",
                [LanguageCode.En] = "Select from Installed Apps & Start Menu",
                [LanguageCode.Ja] = "インストール済みアプリやスタートメニューから選択"
            },
            ["ProgramPickerPlaceholder"] = new()
            {
                [LanguageCode.ZhCn] = "搜索软件名称、可执行文件或路径...",
                [LanguageCode.ZhTw] = "搜尋軟體名稱、執行檔或路徑...",
                [LanguageCode.En] = "Search app name, executable, or path...",
                [LanguageCode.Ja] = "アプリ名、実行可能ファイル、またはパスを検索..."
            },
            ["ProgramPickerScanning"] = new()
            {
                [LanguageCode.ZhCn] = "正在智能检索系统中已安装的软件，请稍候...",
                [LanguageCode.ZhTw] = "正在智慧檢索系統中已安裝的軟體，請稍候...",
                [LanguageCode.En] = "Scanning installed programs, please wait...",
                [LanguageCode.Ja] = "インストール済みアプリをスキャンしています..."
            },
            ["BtnManualBrowse"] = new()
            {
                [LanguageCode.ZhCn] = "手动浏览文件...",
                [LanguageCode.ZhTw] = "手動瀏覽檔案...",
                [LanguageCode.En] = "Browse File...",
                [LanguageCode.Ja] = "手動で参照..."
            },
            ["LangZhCn"] = new()
            {
                [LanguageCode.ZhCn] = "🇨🇳 简体中文 (Simplified Chinese)",
                [LanguageCode.ZhTw] = "🇨🇳 簡體中文 (Simplified Chinese)",
                [LanguageCode.En] = "🇨🇳 简体中文 (Simplified Chinese)",
                [LanguageCode.Ja] = "🇨🇳 簡体字中国語 (Simplified Chinese)"
            },
            ["LangZhTw"] = new()
            {
                [LanguageCode.ZhCn] = "🇭🇰/🇹🇼 繁體中文 (Traditional Chinese)",
                [LanguageCode.ZhTw] = "🇭🇰/🇹🇼 繁體中文 (Traditional Chinese)",
                [LanguageCode.En] = "🇭🇰/🇹🇼 繁體中文 (Traditional Chinese)",
                [LanguageCode.Ja] = "🇭🇰/🇹🇼 繁体字中国語 (Traditional Chinese)"
            },
            ["LangEn"] = new()
            {
                [LanguageCode.ZhCn] = "🇺🇸 English (US/UK)",
                [LanguageCode.ZhTw] = "🇺🇸 English (US/UK)",
                [LanguageCode.En] = "🇺🇸 English (US/UK)",
                [LanguageCode.Ja] = "🇺🇸 英語 (English)"
            },
            ["LangJa"] = new()
            {
                [LanguageCode.ZhCn] = "🇯🇵 日本語 (Japanese)",
                [LanguageCode.ZhTw] = "🇯🇵 日本語 (Japanese)",
                [LanguageCode.En] = "🇯🇵 日本語 (Japanese)",
                [LanguageCode.Ja] = "🇯🇵 日本語 (Japanese)"
            },
            ["LangAuto"] = new()
            {
                [LanguageCode.ZhCn] = "🖥️ 跟随系统 (System Default)",
                [LanguageCode.ZhTw] = "🖥️ 跟隨系統 (System Default)",
                [LanguageCode.En] = "🖥️ System Default",
                [LanguageCode.Ja] = "🖥️ システム既定 (System Default)"
            },

            ["StartupTitle"] = new()
            {
                [LanguageCode.ZhCn] = "开机自启动",
                [LanguageCode.ZhTw] = "開機自啟動",
                [LanguageCode.En] = "Run on Windows Startup",
                [LanguageCode.Ja] = "Windows起動時に自動起動"
            },
            ["StartupDesc"] = new()
            {
                [LanguageCode.ZhCn] = "在 Windows 开机登录时静默自启动并在后台托盘驻留。",
                [LanguageCode.ZhTw] = "在 Windows 開機登入時靜默自啟動並在後台托盤駐留。",
                [LanguageCode.En] = "Automatically start StarPie silently minimized to tray on login.",
                [LanguageCode.Ja] = "Windows起動時に自動でタスクトレイに常駐します。"
            },
            ["MemoryTitle"] = new()
            {
                [LanguageCode.ZhCn] = "极简内存优化 (Working Set Trim)",
                [LanguageCode.ZhTw] = "極簡記憶體最佳化 (Working Set Trim)",
                [LanguageCode.En] = "Memory Optimization",
                [LanguageCode.Ja] = "メモリ最適化 (ワーキングセット圧縮)"
            },
            ["MemoryDesc"] = new()
            {
                [LanguageCode.ZhCn] = "启用 Windows 进程工作集深度修剪，后台常驻内存低至 15~25MB。",
                [LanguageCode.ZhTw] = "啟用 Windows 行程工作集深度修剪，後台常駐記憶體低至 15~25MB。",
                [LanguageCode.En] = "Deep trims working set, keeping background RAM usage under 20MB.",
                [LanguageCode.Ja] = "メモリを自動トリムし、バックグラウンド使用量を15〜25MBに維持します。"
            },
            ["BtnTrimMemory"] = new()
            {
                [LanguageCode.ZhCn] = "立即压缩物理内存",
                [LanguageCode.ZhTw] = "立即壓縮實體記憶體",
                [LanguageCode.En] = "Trim RAM Now",
                [LanguageCode.Ja] = "今すぐメモリ圧縮"
            },
            ["ElevateTitle"] = new()
            {
                [LanguageCode.ZhCn] = "管理员权限提升 (Run as Admin)",
                [LanguageCode.ZhTw] = "系統管理員權限提升 (Run as Admin)",
                [LanguageCode.En] = "Run as Administrator",
                [LanguageCode.Ja] = "管理者権限で実行"
            },
            ["ElevateDesc"] = new()
            {
                [LanguageCode.ZhCn] = "以管理员身份重启，可在任务管理器、系统设置等高权限窗口中正常唤起手势。",
                [LanguageCode.ZhTw] = "以系統管理員身分重啟，可在工作管理員、系統設定等高權限視窗中正常呼出手勢。",
                [LanguageCode.En] = "Relaunch with administrator privileges to interact with elevated windows.",
                [LanguageCode.Ja] = "管理者権限で再起動し、タスクマネージャー等の高権限画面でも動作可能にします。"
            },
            ["BtnElevate"] = new()
            {
                [LanguageCode.ZhCn] = "🛡️ 以管理员身份重启",
                [LanguageCode.ZhTw] = "🛡️ 以系統管理員身分重啟",
                [LanguageCode.En] = "🛡️ Restart as Administrator",
                [LanguageCode.Ja] = "🛡️ 管理者として再起動"
            },
            ["BackupTitle"] = new()
            {
                [LanguageCode.ZhCn] = "配置备份与恢复 (Backup & Reset)",
                [LanguageCode.ZhTw] = "配置備份與恢復 (Backup & Reset)",
                [LanguageCode.En] = "Backup & Reset",
                [LanguageCode.Ja] = "バックアップとリセット"
            },
            ["BtnExportConfig"] = new()
            {
                [LanguageCode.ZhCn] = "导出配置备份",
                [LanguageCode.ZhTw] = "匯出配置備份",
                [LanguageCode.En] = "Export Backup",
                [LanguageCode.Ja] = "設定をエクスポート"
            },
            ["BtnImportConfig"] = new()
            {
                [LanguageCode.ZhCn] = "导入配置文件",
                [LanguageCode.ZhTw] = "匯入設定檔",
                [LanguageCode.En] = "Import Config",
                [LanguageCode.Ja] = "設定をインポート"
            },
            ["BtnResetConfig"] = new()
            {
                [LanguageCode.ZhCn] = "恢复出厂设置",
                [LanguageCode.ZhTw] = "恢復原廠設定",
                [LanguageCode.En] = "Restore Factory Defaults",
                [LanguageCode.Ja] = "初期設定にリセット"
            },

            // Tab 4: About & Updates
            ["AboutHeader"] = new()
            {
                [LanguageCode.ZhCn] = "关于 StarPie & 版本记录",
                [LanguageCode.ZhTw] = "關於 StarPie & 版本記錄",
                [LanguageCode.En] = "About StarPie & Changelog",
                [LanguageCode.Ja] = "StarPie について & 更新履歴"
            },
            ["AboutDesc"] = new()
            {
                [LanguageCode.ZhCn] = "高质感、极速现代 Windows 鼠标轮盘笔势工具",
                [LanguageCode.ZhTw] = "高質感、極速現代 Windows 滑鼠輪盤手勢工具",
                [LanguageCode.En] = "High-aesthetic, ultra-fast modern Windows mouse radial gestures tool.",
                [LanguageCode.Ja] = "洗練されたデザインと高速な応答性を誇る次世代マウスジェスチャーツール"
            },
            ["BtnOpenChangelog"] = new()
            {
                [LanguageCode.ZhCn] = "查看完整 CHANGELOG",
                [LanguageCode.ZhTw] = "檢視完整 CHANGELOG",
                [LanguageCode.En] = "View Full CHANGELOG",
                [LanguageCode.Ja] = "完全な更新履歴を表示"
            },
            ["ChangelogNotFound"] = new()
            {
                [LanguageCode.ZhCn] = "CHANGELOG.md 文件位于应用程序根目录。",
                [LanguageCode.ZhTw] = "CHANGELOG.md 檔案位於應用程式根目錄。",
                [LanguageCode.En] = "CHANGELOG.md is located in the application directory.",
                [LanguageCode.Ja] = "CHANGELOG.md はアプリケーション フォルダーにあります。"
            },
            ["ChangelogOpenFailed"] = new()
            {
                [LanguageCode.ZhCn] = "无法打开 CHANGELOG.md。",
                [LanguageCode.ZhTw] = "無法開啟 CHANGELOG.md。",
                [LanguageCode.En] = "Unable to open CHANGELOG.md.",
                [LanguageCode.Ja] = "CHANGELOG.md を開けません。"
            },
            ["MilestonesTitle"] = new()
            {
                [LanguageCode.ZhCn] = "版本演进里程碑 (Milestones)",
                [LanguageCode.ZhTw] = "版本演進里程碑 (Milestones)",
                [LanguageCode.En] = "Version Milestones",
                [LanguageCode.Ja] = "バージョン履歴"
            },

            // Dialogs & System Tray
            ["MsgSaveSuccess"] = new()
            {
                [LanguageCode.ZhCn] = "设置已成功保存至硬盘！",
                [LanguageCode.ZhTw] = "設定已成功儲存至硬碟！",
                [LanguageCode.En] = "Settings successfully saved to disk!",
                [LanguageCode.Ja] = "設定が正常に保存されました！"
            },
            ["MsgConfirmDeletePreset"] = new()
            {
                [LanguageCode.ZhCn] = "确定要永久删除此自定义配色方案吗？\n删除后不可恢复。",
                [LanguageCode.ZhTw] = "確定要永久刪除此自訂配色方案嗎？\n刪除後不可恢復。",
                [LanguageCode.En] = "Are you sure you want to delete this custom color preset?\nThis cannot be undone.",
                [LanguageCode.Ja] = "このカスタム配色プリセットを削除してもよろしいですか？\n削除後は復元できません。"
            },
            ["MsgConfirmReset"] = new()
            {
                [LanguageCode.ZhCn] = "确定要恢复出厂默认设置吗？所有自定义手势与样式将被重置。",
                [LanguageCode.ZhTw] = "確定要恢復原廠預設設定嗎？所有自訂手勢與樣式將被重設。",
                [LanguageCode.En] = "Are you sure you want to restore factory defaults? All customizations will be reset.",
                [LanguageCode.Ja] = "工場出荷時の初期設定に戻してもよろしいですか？すべてのカスタム設定がリセットされます。"
            },
            ["TrayPause"] = new()
            {
                [LanguageCode.ZhCn] = "⏸️ 暂停手势",
                [LanguageCode.ZhTw] = "⏸️ 暫停手勢",
                [LanguageCode.En] = "⏸️ Pause Gestures",
                [LanguageCode.Ja] = "⏸️ ジェスチャーを一時停止"
            },
            ["TrayResume"] = new()
            {
                [LanguageCode.ZhCn] = "▶️ 恢复手势",
                [LanguageCode.ZhTw] = "▶️ 恢復手勢",
                [LanguageCode.En] = "▶️ Resume Gestures",
                [LanguageCode.Ja] = "▶️ ジェスチャーを再開"
            },
            ["TrayPreferences"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 偏好设置 (Settings)",
                [LanguageCode.ZhTw] = "⚙️ 偏好設定 (Settings)",
                [LanguageCode.En] = "⚙️ Preferences (Settings)",
                [LanguageCode.Ja] = "⚙️ 環境設定 (Settings)"
            },
            ["TrayAppearance"] = new()
            {
                [LanguageCode.ZhCn] = "🎨 外观与形态 (Appearance)",
                [LanguageCode.ZhTw] = "🎨 外觀與形態 (Appearance)",
                [LanguageCode.En] = "🎨 Appearance & Shapes",
                [LanguageCode.Ja] = "🎨 外観と形状"
            },
            ["TrayGestures"] = new()
            {
                [LanguageCode.ZhCn] = "⚡ 手势与动作 (Mappings)",
                [LanguageCode.ZhTw] = "⚡ 手勢與動作 (Mappings)",
                [LanguageCode.En] = "⚡ Gestures & Actions",
                [LanguageCode.Ja] = "⚡ ジェスチャーと動作"
            },
            ["TrayAbout"] = new()
            {
                [LanguageCode.ZhCn] = "📋 更新日志与关于 (About)",
                [LanguageCode.ZhTw] = "📋 更新日誌與關於 (About)",
                [LanguageCode.En] = "📋 About & Changelog",
                [LanguageCode.Ja] = "📋 情報と更新履歴"
            },
            ["TrayElevate"] = new()
            {
                [LanguageCode.ZhCn] = "🛡️ 以管理员身份重启",
                [LanguageCode.ZhTw] = "🛡️ 以系統管理員身分重啟",
                [LanguageCode.En] = "🛡️ Restart as Administrator",
                [LanguageCode.Ja] = "🛡️ 管理者として再起動"
            },
            ["TrayExit"] = new()
            {
                [LanguageCode.ZhCn] = "❌ 退出 StarPie",
                [LanguageCode.ZhTw] = "❌ 退出 StarPie",
                [LanguageCode.En] = "❌ Exit StarPie",
                [LanguageCode.Ja] = "❌ StarPie を終了"
            },
            ["TrayTooltip"] = new()
            {
                [LanguageCode.ZhCn] = "StarPie v1.4.1 - 现代化鼠标轮盘笔势",
                [LanguageCode.ZhTw] = "StarPie v1.4.1 - 現代化滑鼠輪盤手勢",
                [LanguageCode.En] = "StarPie v1.4.1 - Modern Mouse Radial Gestures",
                [LanguageCode.Ja] = "StarPie v1.4.1 - 次世代マウスラジアルジェスチャー"
            }
        };
    }
}
