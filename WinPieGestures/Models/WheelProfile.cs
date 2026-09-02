using System.Collections.Generic;

namespace WinPieGestures.Models
{
    public class WheelProfile
    {
        public string ProcessName { get; set; } = "Global"; // e.g. "chrome.exe", "Global", or custom name
        public int SectorCount { get; set; } = 8; // 4, 8, or 12
        public List<ActionItem> Actions { get; set; } = new List<ActionItem>();

        public override string ToString() => ProcessName;
    }
}
