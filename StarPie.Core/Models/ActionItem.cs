namespace StarPie.Models
{
    /// <summary>
    /// 单个轮盘动作项：配置方案（<see cref="WheelProfile"/>）中每个扇区对应的一条动作。
    /// <see cref="Type"/> 决定 <see cref="Parameter"/> 的语义（启动程序 / 热键 / 系统动作）。
    /// </summary>
    public class ActionItem
    {
        /// <summary>动作类型："Launch"（启动程序）、"Hotkey"（发送热键）、"System"（系统动作）。</summary>
        public string Type { get; set; } = "Hotkey";

        /// <summary>轮盘扇区上显示的名称（属数据而非界面文案，由生产方提供本地化/默认名）。</summary>
        public string Name { get; set; } = "";

        /// <summary>动作参数：可执行文件路径、热键字符串或系统动作预设名。</summary>
        public string Parameter { get; set; } = "";

        /// <summary>启动程序时附加的可选命令行参数。</summary>
        public string Arguments { get; set; } = "";

        /// <summary>矢量图标键、emoji 或为空（不显示图标）。</summary>
        public string IconKey { get; set; } = "";

        /// <summary>自定义 SVG 路径几何数据（覆盖默认矢量图标）。</summary>
        public string CustomIconSvg { get; set; } = "";

        /// <summary>列表展示时返回动作名称。</summary>
        public override string ToString() => Name;
    }
}
