using StarPie.Sdk.Models;
using StarPie.Sdk.Services.Wheel;
using StarPie.Ui.Services.Wheel;
using StarPie.Host.Wheel;

namespace StarPie.Ui.ViewModels.Wheel
{
    /// <summary>
    /// 运行时轮盘的瞬态视图数据投影：只含渲染真正需要的轮盘外观字段，由
    /// <see cref="StarPie.Ui.Services.Wheel.WheelFactory"/> 在每次轮盘手势创建轮盘时从运行态配置
    /// <b>快照组装</b>，生命周期与轮盘窗口一致。
    /// </summary>
    /// <remarks>
    /// 视图因此看不见全局配置对象——黑名单、配置方案等与渲染无关的字段不再可达。投影是快照而非
    /// 引用：轮盘弹出期间改配置不会回流到已弹出的轮盘（亚秒级瞬态，下次轮盘手势自然取新值），
    /// 不引入变更传播。
    /// </remarks>
    public sealed class WheelViewData
    {
        // ---- 风格与配色：决定皮肤与画刷 ----

        public string WheelStyle { get; }

        public string WheelPalette { get; }

        /// <summary>配色解析与样式渲染的窄输入（运行态与预览态共用的同一类型）。</summary>
        public WheelPaletteInput PaletteInput { get; }

        // ---- 几何 ----

        public double WheelRadius { get; }

        public double InnerRadius { get; }

        public double CoreRadius { get; }

        // ---- 扇区内容与排版 ----

        public string Shape { get; }

        public double SectorGap { get; }

        public double SectorCornerRadius { get; }

        public string IconLayoutMode { get; }

        public bool ShowText { get; }

        public double SectorIconSize { get; }

        public double SectorFontSize { get; }

        // ---- 中心核 ----

        public bool ShowCoreIcon { get; }

        public string CoreIconType { get; }

        public string CoreCustomIconKey { get; }

        public string CoreCustomIconSvg { get; }

        /// <summary>核自定义位图路径；文件是否存在与加载由图标资产服务判定，视图不读磁盘。</summary>
        public string CoreCustomImagePath { get; }

        /// <summary>核背景位图路径；同上。</summary>
        public string CoreBgImagePath { get; }

        public string CoreBgStretch { get; }

        public double CoreBgOpacity { get; }

        private WheelViewData(
            string wheelStyle,
            string wheelPalette,
            WheelPaletteInput paletteInput,
            double wheelRadius,
            double innerRadius,
            double coreRadius,
            string shape,
            double sectorGap,
            double sectorCornerRadius,
            string iconLayoutMode,
            bool showText,
            double sectorIconSize,
            double sectorFontSize,
            bool showCoreIcon,
            string coreIconType,
            string coreCustomIconKey,
            string coreCustomIconSvg,
            string coreCustomImagePath,
            string coreBgImagePath,
            string coreBgStretch,
            double coreBgOpacity)
        {
            WheelStyle = wheelStyle;
            WheelPalette = wheelPalette;
            PaletteInput = paletteInput;
            WheelRadius = wheelRadius;
            InnerRadius = innerRadius;
            CoreRadius = coreRadius;
            Shape = shape;
            SectorGap = sectorGap;
            SectorCornerRadius = sectorCornerRadius;
            IconLayoutMode = iconLayoutMode;
            ShowText = showText;
            SectorIconSize = sectorIconSize;
            SectorFontSize = sectorFontSize;
            ShowCoreIcon = showCoreIcon;
            CoreIconType = coreIconType;
            CoreCustomIconKey = coreCustomIconKey;
            CoreCustomIconSvg = coreCustomIconSvg;
            CoreCustomImagePath = coreCustomImagePath;
            CoreBgImagePath = coreBgImagePath;
            CoreBgStretch = coreBgStretch;
            CoreBgOpacity = coreBgOpacity;
        }

        /// <summary>从运行态配置取一份瞬态投影：逐字段取值（不持引用），配色微调区经
        /// <see cref="WheelPaletteInput.FromConfig"/> 快照。</summary>
        public static WheelViewData FromConfig(AppConfig? config)
        {
            var c = config ?? new AppConfig();

            return new WheelViewData(
                wheelStyle: string.IsNullOrEmpty(c.WheelStyle) ? WheelStyleNames.Default : c.WheelStyle,
                wheelPalette: string.IsNullOrEmpty(c.WheelPalette) ? WheelPaletteNames.System : c.WheelPalette,
                paletteInput: WheelPaletteInput.FromConfig(config),
                wheelRadius: c.WheelRadius,
                innerRadius: c.InnerRadius,
                coreRadius: c.CoreRadius,
                shape: c.Shape ?? "Original",
                sectorGap: c.SectorGap,
                sectorCornerRadius: c.SectorCornerRadius,
                iconLayoutMode: c.IconLayoutMode ?? WheelSectorContentKernel.DefaultLayoutMode,
                showText: c.ShowText,
                sectorIconSize: c.SectorIconSize,
                sectorFontSize: c.SectorFontSize,
                showCoreIcon: c.ShowCoreIcon,
                coreIconType: string.IsNullOrEmpty(c.CoreIconType) ? "Exit" : c.CoreIconType,
                coreCustomIconKey: c.CoreCustomIconKey ?? "",
                coreCustomIconSvg: c.CoreCustomIconSvg ?? "",
                coreCustomImagePath: c.CoreCustomImagePath ?? "",
                coreBgImagePath: c.CoreBgImagePath ?? "",
                coreBgStretch: c.CoreBgStretch ?? "UniformToFill",
                coreBgOpacity: c.CoreBgOpacity);
        }
    }
}
