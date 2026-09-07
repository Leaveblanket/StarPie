using System.Collections.Generic;

namespace StarPie.Models
{
    /// <summary>
    /// 轮盘配置方案：绑定到某个前台进程（或 "Global" 全局方案）的一组扇区与动作，
    /// 前台进程匹配到对应方案时轮盘按该方案呈现。
    /// </summary>
    public class WheelProfile
    {
        /// <summary>关联的进程名（如 "chrome.exe"）、"Global" 全局方案或自定义名称。</summary>
        public string ProcessName { get; set; } = "Global";

        /// <summary>扇区数量（4、8 或 12）。</summary>
        public int SectorCount { get; set; } = 8;

        /// <summary>按扇区顺序排列的动作列表。</summary>
        public List<ActionItem> Actions { get; set; } = new List<ActionItem>();

        /// <summary>列表展示时返回方案名（进程名）。</summary>
        public override string ToString() => ProcessName;
    }
}
