using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace StarPie.Models
{
    /// <summary>
    /// 应用运行配置模型：与 config.json 一一对应的持久化数据（语言、手势行为、轮盘外观与
    /// 几何、自定义配色/背景纹理、配置方案与场景隔离等）。文件读写由宿主内核的配置服务负责；
    /// 反序列化采用宽松模式（大小写不敏感、允许注释与尾随逗号），便于手工编辑。
    /// </summary>
    public class AppConfig
    {
        // —— 语言与手势行为 ——
        public string Language { get; set; } = "Auto"; // 界面语言："Auto"（按系统区域）/ "zh-CN" / "zh-TW" / "en" / "ja"
        public double DragThreshold { get; set; } = 25.0; // 手势触发阈值（像素）：按住拖动超过该距离才展开轮盘
        public bool EnableOuterEscapeCancel { get; set; } = true; // 是否启用"外圈逃逸取消"：指针越过逃逸距离即收起轮盘
        public double OuterEscapeDistance { get; set; } = 186.0; // 外圈逃逸取消的触发距离（像素）；0 时回退为轮盘半径 × 1.5

        // —— 主题与 UI 风格 ——
        public string AppTheme { get; set; } = "System"; // 应用界面主题："System" / "Light" / "Dark" / "MidnightNavy" / "RoyalViolet" / "TitaniumGray"
        public string WheelPalette { get; set; } = "System"; // 轮盘配色主题："System" / "Dark" / "Light" / "MatchaForest" / "GlacialIce" / "MorandiMuted" / "Custom"
        public string WheelStyle { get; set; } = "ClassicRing"; // 轮盘主题风格："ClassicRing" / "CleanSectors" / "Glassmorphism" / "CatPaw"

        // —— 轮盘几何 ——
        public bool ShowText { get; set; } = true; // 扇区上是否显示文字标签
        public double WheelRadius { get; set; } = 138.0; // 轮盘外半径（像素）
        public double InnerRadius { get; set; } = 52.0; // 扇区环的内半径（像素）
        public double CoreRadius { get; set; } = 50.0; // 中心核圆的半径（像素）
        public string Shape { get; set; } = "Original"; // 扇区形状："Original" / "Circle" / "RoundedRect" / "FloatingCapsules" / "HexagonHive"
        public double SectorGap { get; set; } = 2.0; // 扇区间的光学间隙：0 ~ 12 px
        public double SectorCornerRadius { get; set; } = 4.0; // 扇区圆角（导角）：0 ~ 16 px
        public string IconLayoutMode { get; set; } = "IconAndText"; // 扇区内容布局："IconAndText" / "IconOnly" / "TextOnly"
        public double SectorIconSize { get; set; } = 20.0; // 扇区图标尺寸：14.0 ~ 36.0 px
        public double SectorFontSize { get; set; } = 10.5; // 扇区文字字号：8.0 ~ 18.0 px

        // —— 中心核图标与图案自定义 ——
        public bool ShowCoreIcon { get; set; } = true; // 是否显示中心核图标
        public string CoreIconType { get; set; } = "Exit"; // 中心图标类型："Exit" / "Crosshair" / "Windows" / "Dot" / "Home" / "Power" / "Compass" / "CatPaw" / "Custom" / "Image"
        public string CoreCustomIconKey { get; set; } = ""; // "Custom" 时选择的图标键（内置/自定义图标清单）
        public string CoreCustomIconSvg { get; set; } = ""; // "Custom" 时粘贴的自定义 SVG 路径数据
        public string CoreCustomImagePath { get; set; } = ""; // "Image" 时使用的位图文件路径
        public string CoreCustomImageStretch { get; set; } = "UniformToFill"; // 位图填充方式（UniformToFill 等）

        // —— 高亮边缘光晕自定义 ——
        public string HighlightGlowPreset { get; set; } = "Auto"; // 光晕预设："Auto" / "Lilac" / "Blue" / "Emerald" / "Rose" / "Amber" / "Red" / "White" / "Custom"
        public string HighlightGlowColor { get; set; } = ""; // 自定义光晕颜色（如 "#FFA855F7"）；空表示按预设自动取色
        public double HighlightGlowRadius { get; set; } = 24.0; // 光晕扩散半径：8.0 ~ 48.0 px
        public double HighlightGlowOpacity { get; set; } = 0.85; // 光晕不透明度：0.0 ~ 1.0

        // —— 自定义配色微调（WheelPalette = "Custom" 时逐项覆盖风格默认观感）——
        public string CustomSectorBg { get; set; } = "#9016161A"; // 扇区背景色
        public string CustomSectorBorder { get; set; } = "#35FFFFFF"; // 扇区边框色
        public string CustomHighlightBg { get; set; } = "#E06C4DFF"; // 高亮扇区背景色
        public string CustomHighlightBorder { get; set; } = "#A0FFFFFF"; // 高亮扇区边框色
        public string CustomText { get; set; } = "#E0FFFFFF"; // 扇区文字颜色

        /// <summary>用户保存的自定义配色预设列表（WheelPalette 可选 "CustomPreset_{Id}" 或预设名引用）。</summary>
        public List<CustomColorPreset> CustomColorPresets { get; set; } = new List<CustomColorPreset>();

        /// <summary>
        /// 插件配置段：键为插件 id，值为该插件自己的配置对象（<c>plugins.&lt;id&gt;</c>）。
        /// </summary>
        /// <remarks>
        /// 该段归插件所有，宿主状态不写这里；旧配置没有该段时照常加载，缺段即空字典。
        /// 取值放宽到任意 JSON 值（而非只收对象）：手改配置写坏某一段时不该让整份配置回退默认值，
        /// 段内形状由读它的插件自行判定。宿主只在"彻底移除"时整段删除，不解释段内内容。
        /// </remarks>
        public Dictionary<string, JsonNode?> Plugins { get; set; } = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

        // —— 背景图片与纹理自定义 ——
        public string WheelBgImagePath { get; set; } = ""; // 轮盘背景图片路径（空表示不使用）
        public double WheelBgOpacity { get; set; } = 0.8; // 轮盘背景图片不透明度
        public string WheelBgStretch { get; set; } = "UniformToFill"; // 轮盘背景填充方式："UniformToFill" / "Uniform" / "Fill" / "None"
        public string CoreBgImagePath { get; set; } = ""; // 中心核背景图片路径（空表示不使用）
        public double CoreBgOpacity { get; set; } = 1.0; // 中心核背景图片不透明度
        public string CoreBgStretch { get; set; } = "UniformToFill"; // 中心核背景填充方式
        public string HighlightTexturePath { get; set; } = ""; // 高亮扇区纹理图片路径（空表示不使用）
        public double HighlightTextureOpacity { get; set; } = 0.7; // 高亮扇区纹理不透明度

        /// <summary>全部轮盘配置方案（含 "Global" 全局方案与各进程专属方案）。</summary>
        public List<WheelProfile> Profiles { get; set; } = new List<WheelProfile>();

        // —— 场景隔离设置 ——
        public List<string> BlacklistedProcesses { get; set; } = new List<string> { "mstsc.exe", "paint.exe" }; // 黑名单进程：处于前台时禁用手势/轮盘
        public bool DisableOnCtrl { get; set; } = false; // 按住 Ctrl 时禁用
        public bool DisableOnShift { get; set; } = false; // 按住 Shift 时禁用
        public bool DisableOnAlt { get; set; } = false; // 按住 Alt 时禁用
        public bool DisableOnFullScreen { get; set; } = true; // 前台为全屏窗口时禁用
    }
}
