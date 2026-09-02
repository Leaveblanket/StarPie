namespace WinPieGestures.Models
{
    public class ActionItem
    {
        public string Type { get; set; } = "Hotkey"; // "Launch", "Hotkey", "System"
        public string Name { get; set; } = "快捷动作"; // Name to show on the wheel sector
        public string Parameter { get; set; } = ""; // Executable path, hotkey string, or system preset
        public string Arguments { get; set; } = ""; // Optional arguments for launching
        public string IconKey { get; set; } = ""; // Vector icon key or emoji or empty
        public string CustomIconSvg { get; set; } = ""; // Custom SVG path geometry

        public override string ToString() => Name;
    }
}
