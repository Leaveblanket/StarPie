using System;
using StarPie.Icons;
using StarPie.Services.Icons;

namespace StarPie.Wheel
{
    /// <summary>
    /// 单个扇区的动作数据（与 <c>ActionItem</c> 同形的窄字段）：内核只认这些字段，不认全局配置。
    /// 配置消费面收窄时不必回头改内核签名。
    /// </summary>
    public readonly struct WheelSectorInput
    {
        /// <summary>扇区显示文字（已由消费方解析，含空扇区占位文案）；空串即不画文字。</summary>
        public string Text { get; }

        /// <summary>动作类型（原样参与内置向量判定；空类型不给内置向量）。无动作槽位由消费方
        /// 传 "Hotkey"。</summary>
        public string Type { get; }

        public string Parameter { get; }

        public string IconKey { get; }

        /// <summary>动作自带的自定义 SVG 路径数据（回退链最优先）。</summary>
        public string CustomIconSvg { get; }

        /// <summary>图标键命中自定义图标目录时的条目；未命中为 null。</summary>
        public CustomIconItem? CustomIcon { get; }

        public WheelSectorInput(
            string? text,
            string? type,
            string? parameter,
            string? iconKey,
            string? customIconSvg,
            CustomIconItem? customIcon = null)
        {
            Text = text ?? "";
            Type = type ?? "";
            Parameter = parameter ?? "";
            IconKey = iconKey ?? "";
            CustomIconSvg = customIconSvg ?? "";
            CustomIcon = customIcon;
        }
    }

    /// <summary>单个轮盘的窄排版字段（同样不含配置对象）。</summary>
    public readonly struct WheelSectorLayoutSpec
    {
        public int SectorCount { get; }

        /// <summary>排版模式（IconAndText / IconOnly / TextOnly）；空值回落 IconAndText。</summary>
        public string IconLayoutMode { get; }

        public bool ShowText { get; }

        public double SectorIconSize { get; }

        public double SectorFontSize { get; }

        public WheelSectorLayoutSpec(
            int sectorCount,
            string? iconLayoutMode,
            bool showText,
            double sectorIconSize,
            double sectorFontSize)
        {
            SectorCount = sectorCount;
            IconLayoutMode = string.IsNullOrEmpty(iconLayoutMode)
                ? WheelSectorContentKernel.DefaultLayoutMode
                : iconLayoutMode;
            ShowText = showText;
            SectorIconSize = sectorIconSize;
            SectorFontSize = sectorFontSize;
        }
    }

    /// <summary>
    /// 扇区内容内核（纯逻辑、零 WPF、可 headless 构造）：运行时轮盘与外观页预览共用的
    /// 「扇区内容是什么」唯一来源——图标五级回退链、按扇区数的排版缩放、内置向量字面量。
    /// 「怎么画」仍归各自渲染方（轮盘的窗口动画/生命周期、预览的 Canvas 绘制与 hover）。
    /// </summary>
    /// <remarks>
    /// 回退链以运行态为正典：动作自带 SVG → 自定义图标目录（<c>custom:</c> 前缀，SVG 取路径数据、
    /// 位图取文件）→ 内置矢量目录按图标键 → 程序图标（仅 Launch）→ 内置向量按类型与系统参数。
    /// 系统参数经 <see cref="WheelBuiltInIcons.SystemParameterIconKey"/> 映射，不经目录原样查键。
    /// SVG 路径数据的可解析性由调用方经 <paramref name="isParsableSvg"/> 注入（解析是 WPF 面），
    /// 不可解析即回落下一级——只对用户提供的 SVG（动作自带与自定义图标目录）施加，内置数据可信。
    /// </remarks>
    public static class WheelSectorContentKernel
    {
        /// <summary>自定义图标键前缀（图标目录中由用户导入的条目）。</summary>
        public const string CustomIconKeyPrefix = "custom:";

        /// <summary>排版模式缺省值。</summary>
        public const string DefaultLayoutMode = "IconAndText";

        private const string IconOnlyLayoutMode = "IconOnly";
        private const string TextOnlyLayoutMode = "TextOnly";
        private const string LaunchActionType = "Launch";

        // 未配置时的回落，与配置模型默认值同值。
        private const double DefaultSectorIconSize = 20.0;
        private const double DefaultSectorFontSize = 10.5;

        // 布局模式的补偿：纯图标无文字分担视觉重量故放大，纯文字同理放大字号。
        private const double IconOnlyBoost = 1.35;
        private const double TextOnlyFontBoost = 1.0;

        // 程序图标自带留白，比其他图标宽出这一档。
        private const double ProgramIconPadding = 4.0;

        // 图标与文字之间的呼吸间距。
        private const double IconTextGap = 2.0;
        private const double TextTopMargin = 1.0;

        /// <summary>图标键是否指向自定义图标目录（<c>custom:</c> 前缀，忽略大小写）。</summary>
        public static bool IsCustomIconKey(string? iconKey)
            => !string.IsNullOrEmpty(iconKey)
               && iconKey.StartsWith(CustomIconKeyPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>构建单个扇区的内容。见类型级 remarks 的回退链与回落口径。</summary>
        /// <param name="scale">内容随几何一同缩放的倍率（预览画布用；运行时轮盘传 1）。</param>
        /// <param name="isParsableSvg">用户提供的 SVG 路径数据可否解析；省略视为全部可解析。</param>
        public static WheelSectorContent Build(
            WheelSectorInput sector,
            WheelSectorLayoutSpec layout,
            double scale = 1.0,
            Func<string, bool>? isParsableSvg = null)
        {
            if (scale <= 0)
            {
                scale = 1.0;
            }

            SectorMetrics metrics = SectorMetrics.For(layout.SectorCount);
            bool textOnly = layout.IconLayoutMode == TextOnlyLayoutMode;
            bool iconOnly = layout.IconLayoutMode == IconOnlyLayoutMode;
            // 布局级「要不要文字」：文字是否非空不改变图标间距口径。
            bool layoutShowsText = layout.ShowText && !iconOnly;

            double configuredIconSize = layout.SectorIconSize > 0 ? layout.SectorIconSize : DefaultSectorIconSize;
            double configuredFontSize = layout.SectorFontSize > 0 ? layout.SectorFontSize : DefaultSectorFontSize;

            WheelIconContent icon = WheelIconContent.None;
            double iconSize = 0;
            if (!textOnly)
            {
                icon = ResolveIcon(sector, isParsableSvg);
                if (icon.Kind != WheelIconKind.None)
                {
                    double baseIconSize = iconOnly ? configuredIconSize * IconOnlyBoost : configuredIconSize;
                    iconSize = baseIconSize * metrics.IconScale * scale;
                    if (icon.Kind == WheelIconKind.ProgramIcon)
                    {
                        iconSize += ProgramIconPadding * scale;
                    }
                }
            }

            double baseFontSize = textOnly ? configuredFontSize + TextOnlyFontBoost : configuredFontSize;
            double fontSize = Math.Min(
                Math.Max(baseFontSize * scale, metrics.FontFloor * scale),
                metrics.FontCap * scale);

            return new WheelSectorContent(
                icon: icon,
                iconSize: iconSize,
                iconBottomMargin: layoutShowsText ? IconTextGap * scale : 0.0,
                showText: layoutShowsText && !string.IsNullOrEmpty(sector.Text),
                text: sector.Text,
                fontSize: fontSize,
                textMaxWidth: metrics.TextMaxWidth * scale,
                textTopMargin: TextTopMargin * scale,
                containerWidth: metrics.ContainerWidth * scale,
                containerHeight: metrics.ContainerHeight * scale);
        }

        private static WheelIconContent ResolveIcon(WheelSectorInput sector, Func<string, bool>? isParsableSvg)
        {
            // 类型原样参与判定：空类型不给内置向量；无动作槽位的 "Hotkey" 由消费方给出
            // （WheelSectorViewModel 的视图默认值），内核不再自造回落。
            string type = sector.Type;

            // 1. 动作自带的自定义 SVG
            if (!string.IsNullOrEmpty(sector.CustomIconSvg) && IsParsable(isParsableSvg, sector.CustomIconSvg))
            {
                return new WheelIconContent(WheelIconKind.SvgPath, sector.CustomIconSvg);
            }

            if (!string.IsNullOrEmpty(sector.IconKey))
            {
                if (IsCustomIconKey(sector.IconKey))
                {
                    // 2. 自定义图标目录：SVG 取路径数据，位图取文件
                    if (sector.CustomIcon is { } custom)
                    {
                        if (custom.IsSvg)
                        {
                            if (IsParsable(isParsableSvg, custom.SvgData))
                            {
                                return new WheelIconContent(WheelIconKind.SvgPath, custom.SvgData);
                            }
                        }
                        else
                        {
                            return new WheelIconContent(WheelIconKind.CustomImageFile, custom.FilePath);
                        }
                    }
                }
                else
                {
                    // 3. 内置矢量目录按图标键
                    string? svgData = IconCatalog.GetSvgPathByKey(sector.IconKey);
                    if (!string.IsNullOrEmpty(svgData))
                    {
                        return new WheelIconContent(WheelIconKind.SvgPath, svgData);
                    }
                }
            }

            // 4. 程序图标
            if (type == LaunchActionType && !string.IsNullOrEmpty(sector.Parameter))
            {
                return new WheelIconContent(WheelIconKind.ProgramIcon, sector.Parameter);
            }

            // 5. 内置向量按动作类型与系统参数
            string? builtIn = WheelBuiltInIcons.ResolveSvgPath(type, sector.Parameter);
            if (!string.IsNullOrEmpty(builtIn))
            {
                return new WheelIconContent(WheelIconKind.SvgPath, builtIn);
            }

            return WheelIconContent.None;
        }

        private static bool IsParsable(Func<string, bool>? isParsableSvg, string svgPathData)
            => isParsableSvg?.Invoke(svgPathData) ?? true;

        /// <summary>按扇区数的排版缩放纯数据表：命中 4/12 走专列，其余（含 6/8）走通用列。
        /// 字号以区间表达（下限仅 4 扇区抬升、上限仅 12 扇区压低），不再是散落的条件表达式。</summary>
        private readonly struct SectorMetrics
        {
            private static readonly SectorMetrics FourSectors = new SectorMetrics(
                containerWidth: 96.0, containerHeight: 72.0,
                iconScale: 1.20, fontFloor: 11.5, fontCap: double.PositiveInfinity,
                textMaxWidth: 90.0);

            private static readonly SectorMetrics TwelveSectors = new SectorMetrics(
                containerWidth: 58.0, containerHeight: 48.0,
                iconScale: 0.82, fontFloor: 0.0, fontCap: 9.5,
                textMaxWidth: 50.0);

            private static readonly SectorMetrics Default = new SectorMetrics(
                containerWidth: 84.0, containerHeight: 64.0,
                iconScale: 1.00, fontFloor: 0.0, fontCap: double.PositiveInfinity,
                textMaxWidth: 78.0);

            public double ContainerWidth { get; }
            public double ContainerHeight { get; }
            public double IconScale { get; }
            public double FontFloor { get; }
            public double FontCap { get; }
            public double TextMaxWidth { get; }

            private SectorMetrics(
                double containerWidth, double containerHeight,
                double iconScale, double fontFloor, double fontCap,
                double textMaxWidth)
            {
                ContainerWidth = containerWidth;
                ContainerHeight = containerHeight;
                IconScale = iconScale;
                FontFloor = fontFloor;
                FontCap = fontCap;
                TextMaxWidth = textMaxWidth;
            }

            public static SectorMetrics For(int sectorCount)
            {
                switch (sectorCount)
                {
                    case 4:
                        return FourSectors;
                    case 12:
                        return TwelveSectors;
                    default:
                        return Default;
                }
            }
        }
    }
}
