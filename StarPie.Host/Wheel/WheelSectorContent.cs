namespace StarPie.Wheel
{
    /// <summary>扇区图标的内容形态：决定消费方把 <see cref="WheelIconContent.Data"/> 交给谁去取图。</summary>
    public enum WheelIconKind
    {
        /// <summary>无图标可用（该扇区不画图标元素）。</summary>
        None,

        /// <summary>矢量路径数据，消费方自行 <c>Geometry.Parse</c> 后画 Path。</summary>
        SvgPath,

        /// <summary>自定义图标目录中的位图文件路径，消费方经图标资产服务取图像源。</summary>
        CustomImageFile,

        /// <summary>程序/文件路径，消费方经图标资产服务提取文件图标；提取不到即不画。</summary>
        ProgramIcon,
    }

    /// <summary>扇区图标的内容（纯数据）：形态与取图入参。尺寸由
    /// <see cref="WheelSectorContent.IconSize"/> 给出，不含任何控件。</summary>
    public sealed class WheelIconContent
    {
        /// <summary>无图标。</summary>
        public static readonly WheelIconContent None = new WheelIconContent(WheelIconKind.None, "");

        public WheelIconKind Kind { get; }

        /// <summary>随 <see cref="Kind"/> 解释：SvgPath 为路径数据，CustomImageFile 为位图文件路径，
        /// ProgramIcon 为程序/文件路径；None 为空串。</summary>
        public string Data { get; }

        public WheelIconContent(WheelIconKind kind, string data)
        {
            Kind = kind;
            Data = data ?? "";
        }
    }

    /// <summary>
    /// 单个扇区的内容（纯数据，零控件）：图标取什么、多大，文字排多大、多宽，容器占多少。
    /// 消费方只负责把这份数据画成自己的元素（Path/Image/TextBlock），不再自行决策内容。
    /// </summary>
    public sealed class WheelSectorContent
    {
        public WheelIconContent Icon { get; }

        /// <summary>图标元素边长（已含按扇区数的缩放与程序图标加宽）；<see cref="WheelIconKind.None"/> 时无意义。</summary>
        public double IconSize { get; }

        /// <summary>图标元素下边距（布局显示文字时为图标—文字间距，否则为 0）。</summary>
        public double IconBottomMargin { get; }

        /// <summary>是否绘制文字：布局显示文字且文字非空。为 false 时下标不画 TextBlock。</summary>
        public bool ShowText { get; }

        public string Text { get; }

        public double FontSize { get; }

        public double TextMaxWidth { get; }

        /// <summary>文字元素上边距。</summary>
        public double TextTopMargin { get; }

        /// <summary>承载图标与文字的容器尺寸（消费方据此把内容居中到扇区落点）。</summary>
        public double ContainerWidth { get; }

        public double ContainerHeight { get; }

        public WheelSectorContent(
            WheelIconContent icon,
            double iconSize,
            double iconBottomMargin,
            bool showText,
            string text,
            double fontSize,
            double textMaxWidth,
            double textTopMargin,
            double containerWidth,
            double containerHeight)
        {
            Icon = icon;
            IconSize = iconSize;
            IconBottomMargin = iconBottomMargin;
            ShowText = showText;
            Text = text ?? "";
            FontSize = fontSize;
            TextMaxWidth = textMaxWidth;
            TextTopMargin = textTopMargin;
            ContainerWidth = containerWidth;
            ContainerHeight = containerHeight;
        }
    }
}
