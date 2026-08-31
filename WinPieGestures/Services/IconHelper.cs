using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace WinPieGestures.Services
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    internal interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
    }

    public class VectorIconItem
    {
        public string Key { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SvgData { get; set; } = "";
    }

    public static class IconHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #region Vector Icon Catalogue

        public static readonly List<VectorIconItem> VectorIconList = new List<VectorIconItem>
        {
            // Edit & Clipboard
            new VectorIconItem { Key = "Copy", Category = "编辑与剪贴板", DisplayName = "复制 (Copy)", SvgData = "M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z" },
            new VectorIconItem { Key = "Paste", Category = "编辑与剪贴板", DisplayName = "粘贴 (Paste)", SvgData = "M19,20H5V4H7V7H17V4H19M12,2A1,1 0 0,1 13,3A1,1 0 0,1 12,4A1,1 0 0,1 11,3A1,1 0 0,1 12,2M19,2H14.82C14.4,0.84 13.3,0 12,0C10.7,0 9.6,0.84 9.18,2H5A2,2 0 0,0 3,4V20A2,2 0 0,0 5,22H19A2,2 0 0,0 21,20V4A2,2 0 0,0 19,2Z" },
            new VectorIconItem { Key = "Cut", Category = "编辑与剪贴板", DisplayName = "剪切 (Cut)", SvgData = "M9.64,7.64C9.87,7.14 10,6.59 10,6A4,4 0 0,0 6,2A4,4 0 0,0 2,6A4,4 0 0,0 6,10C6.59,10 7.14,9.87 7.64,9.64L10,12L7.64,14.36C7.14,14.13 6.59,14 6,14A4,4 0 0,0 2,18A4,4 0 0,0 6,22A4,4 0 0,0 10,18C10,17.41 9.87,16.86 9.64,16.36L12,14L19,21H22L13.5,12.5L16.36,9.64C16.86,9.87 17.41,10 18,10A4,4 0 0,0 22,6A4,4 0 0,0 18,2A4,4 0 0,0 14,6C14,6.59 14.13,7.14 14.36,7.64L12,10L9.64,7.64M6,4A2,2 0 0,1 8,6A2,2 0 0,1 6,8A2,2 0 0,1 4,6A2,2 0 0,1 6,4M6,16A2,2 0 0,1 8,18A2,2 0 0,1 6,20A2,2 0 0,1 4,18A2,2 0 0,1 6,16M18,4A2,2 0 0,1 20,6A2,2 0 0,1 18,8A2,2 0 0,1 16,6A2,2 0 0,1 18,4Z" },
            new VectorIconItem { Key = "Undo", Category = "编辑与剪贴板", DisplayName = "撤销 (Undo)", SvgData = "M12.5,8C9.85,8 7.45,8.97 5.6,10.6L2,7V16H11L7.38,12.38C8.77,11.22 10.54,10.5 12.5,10.5C16.04,10.5 19.05,12.81 20.1,16L22.47,15.22C21.08,11.03 17.15,8 12.5,8Z" },
            new VectorIconItem { Key = "Redo", Category = "编辑与剪贴板", DisplayName = "重做 (Redo)", SvgData = "M18.4,10.6C16.55,8.97 14.15,8 11.5,8C6.85,8 2.92,11.03 1.53,15.22L3.9,16C4.95,12.81 7.96,10.5 11.5,10.5C13.46,10.5 15.23,11.22 16.62,12.38L13,16H22V7L18.4,10.6Z" },
            new VectorIconItem { Key = "Save", Category = "编辑与剪贴板", DisplayName = "保存 (Save)", SvgData = "M15,9H5V5H15M12,19A3,3 0 0,1 9,16A3,3 0 0,1 12,13A3,3 0 0,1 15,16A3,3 0 0,1 12,19M17,3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3Z" },
            new VectorIconItem { Key = "Search", Category = "编辑与剪贴板", DisplayName = "搜索查找 (Search)", SvgData = "M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5C7,5 5,7 5,9.5C5,12 7,14 9.5,14C12,14 14,12 14,9.5C14,7 12,5 9.5,5Z" },

            // Window Management
            new VectorIconItem { Key = "CloseWindow", Category = "窗口管理", DisplayName = "关闭当前窗口 (Close)", SvgData = "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z" },
            new VectorIconItem { Key = "Minimize", Category = "窗口管理", DisplayName = "最小化窗口 (Minimize)", SvgData = "M20,14H4V10H20" },
            new VectorIconItem { Key = "Maximize", Category = "窗口管理", DisplayName = "最大化/还原 (Maximize)", SvgData = "M4,4H20V20H4V4M6,6V18H18V6H6Z" },
            new VectorIconItem { Key = "SnapLeft", Category = "窗口管理", DisplayName = "左半屏贴靠 (Snap Left)", SvgData = "M4,4H20A2,2 0 0,1 22,6V18A2,2 0 0,1 20,20H4A2,2 0 0,1 2,18V6A2,2 0 0,1 4,4M4,6V18H11V6H4M13,6V18H20V6H13Z" },
            new VectorIconItem { Key = "SnapRight", Category = "窗口管理", DisplayName = "右半屏贴靠 (Snap Right)", SvgData = "M4,4H20A2,2 0 0,1 22,6V18A2,2 0 0,1 20,20H4A2,2 0 0,1 2,18V6A2,2 0 0,1 4,4M4,6V18H11V6H4M13,6V18H20V6H13Z" },
            new VectorIconItem { Key = "TaskView", Category = "窗口管理", DisplayName = "任务视图/多任务 (Task View)", SvgData = "M4,4H10V10H4V4M14,4H20V10H14V4M4,14H10V20H4V14M14,14H20V20H14V14Z" },
            new VectorIconItem { Key = "PrevDesktop", Category = "窗口管理", DisplayName = "上一虚拟桌面 (Prev Desktop)", SvgData = "M4,2H20A2,2 0 0,1 22,4V16A2,2 0 0,1 20,18H14V20H16V22H8V20H10V18H4A2,2 0 0,1 2,16V4A2,2 0 0,1 4,2M13,6L8,10L13,14V11H17V9H13V6Z" },
            new VectorIconItem { Key = "NextDesktop", Category = "窗口管理", DisplayName = "下一虚拟桌面 (Next Desktop)", SvgData = "M4,2H20A2,2 0 0,1 22,4V16A2,2 0 0,1 20,18H14V20H16V22H8V20H10V18H4A2,2 0 0,1 2,16V4A2,2 0 0,1 4,2M11,6V9H7V11H11V14L16,10L11,6Z" },
            new VectorIconItem { Key = "ShowDesktop", Category = "窗口管理", DisplayName = "显示桌面 (Desktop)", SvgData = "M4,2A2,2,0,0,0,2,4V16A2,2,0,0,0,4,18H10V20H8V22H16V20H14V18H20A2,2,0,0,0,22,16V4A2,2,0,0,0,20,2H4ZM4,4H20V16H4V4Z" },
            new VectorIconItem { Key = "FullScreen", Category = "窗口管理", DisplayName = "全屏切换 (Full Screen)", SvgData = "M5,5H10V7H7V10H5V5M14,5H19V10H17V7H14V5M17,14H19V19H14V17H17V14M10,17V19H5V14H7V17H10Z" },
            new VectorIconItem { Key = "Screenshot", Category = "窗口管理", DisplayName = "屏幕截图 (Screenshot)", SvgData = "M4,4H7L9,2H15L17,4H20A2,2,0,0,1,22,6V18A2,2,0,0,1,20,20H4A2,2,0,0,1,2,18V6A2,2,0,0,1,4,4ZM12,7A5,5,0,1,0,17,12A5,5,0,0,0,12,7ZM12,9A3,3,0,1,1,9,12A3,3,0,0,1,12,9Z" },

            // Browser & Navigation
            new VectorIconItem { Key = "Back", Category = "网页浏览", DisplayName = "后退 (Back)", SvgData = "M20,11H7.83L13.42,5.41L12,4L4,12L12,20L13.41,18.59L7.83,13H20V11Z" },
            new VectorIconItem { Key = "Forward", Category = "网页浏览", DisplayName = "前进 (Forward)", SvgData = "M12,4L10.59,5.41L16.17,11H4V13H16.17L10.59,18.59L12,20L20,12L12,4Z" },
            new VectorIconItem { Key = "Refresh", Category = "网页浏览", DisplayName = "刷新 (Refresh)", SvgData = "M17.65,6.35C16.2,4.9 14.21,4 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20C15.73,20 18.84,17.45 19.73,14H17.65C16.83,16.33 14.61,18 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6C13.66,6 15.14,6.69 16.22,7.78L13,11H20V4L17.65,6.35Z" },
            new VectorIconItem { Key = "NewTab", Category = "网页浏览", DisplayName = "新建标签页 (New Tab)", SvgData = "M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z" },
            new VectorIconItem { Key = "CloseTab", Category = "网页浏览", DisplayName = "关闭标签页 (Close Tab)", SvgData = "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z" },
            new VectorIconItem { Key = "ReopenTab", Category = "网页浏览", DisplayName = "恢复标签页 (Reopen Tab)", SvgData = "M13,3A9,9 0 0,0 4,12H1L4.89,15.89L4.96,16.03L9,12H6A7,7 0 0,1 13,5A7,7 0 0,1 20,12A7,7 0 0,1 13,19C11.07,19 9.32,18.21 8.06,16.94L6.64,18.36C8.27,20 10.5,21 13,21A9,9 0 0,0 22,12A9,9 0 0,0 13,3Z" },
            new VectorIconItem { Key = "ZoomIn", Category = "网页浏览", DisplayName = "页面放大 (Zoom In)", SvgData = "M15.5,14H14.71L14.44,13.73C15.41,12.59 16,11.11 16,9.5A6.5,6.5 0 1,0 9.5,16C11.11,16 12.59,15.41 13.73,14.44L14.71,14H15.5L20.5,19L19,20.5L14,15.5M9.5,14C7,14 5,12 5,9.5C5,7 7,5 9.5,5C12,5 14,7 14,9.5C14,12 12,14 9.5,14M12,10H10V12H9V10H7V9H9V7H10V9H12V10Z" },
            new VectorIconItem { Key = "ZoomOut", Category = "网页浏览", DisplayName = "页面缩小 (Zoom Out)", SvgData = "M15.5,14H14.71L14.44,13.73C15.41,12.59 16,11.11 16,9.5A6.5,6.5 0 1,0 9.5,16C11.11,16 12.59,15.41 13.73,14.44L14.71,14H15.5L20.5,19L19,20.5L14,15.5M9.5,14C7,14 5,12 5,9.5C5,7 7,5 9.5,5C12,5 14,7 14,9.5C14,12 12,14 9.5,14M7,9H12V10H7V9Z" },

            // Media & System
            new VectorIconItem { Key = "VolumeUp", Category = "多媒体与系统", DisplayName = "音量增加 (Volume Up)", SvgData = "M3,9V15H7L12,20V4L7,9H3ZM14,3.23V5.29C16.89,6.15 19,8.83 19,12C19,15.17 16.89,17.85 14,18.71V20.77C18.01,19.86 21,16.28 21,12C21,7.72 18.01,4.14 14,3.23ZM14,8.83V15.17C15.14,14.6 16,13.4 16,12C16,10.6 15.14,9.4 14,8.83Z" },
            new VectorIconItem { Key = "VolumeDown", Category = "多媒体与系统", DisplayName = "音量减小 (Volume Down)", SvgData = "M3,9V15H7L12,20V4L7,9H3ZM14,8.83V15.17C15.14,14.6 16,13.4 16,12C16,10.6 15.14,9.4 14,8.83ZM14,3.23V5.29C16.89,6.15 19,8.83 19,12C19,15.17 16.89,17.85 14,18.71V20.77C18.01,19.86 21,16.28 21,12Z" },
            new VectorIconItem { Key = "VolumeMute", Category = "多媒体与系统", DisplayName = "静音切换 (Mute)", SvgData = "M3,9V15H7L12,20V4L7,9H3ZM16.5,12L14,9.5L15.5,8L18,10.5L20.5,8L22,9.5L19.5,12L22,14.5L20.5,16L18,13.5L15.5,16L14,14.5L16.5,12Z" },
            new VectorIconItem { Key = "PlayPause", Category = "多媒体与系统", DisplayName = "播放/暂停 (Play/Pause)", SvgData = "M19,19H13V5H19M11,5V19L2,12" },
            new VectorIconItem { Key = "NextTrack", Category = "多媒体与系统", DisplayName = "下一曲 (Next)", SvgData = "M16,18H18V6H16M6,18L14.5,12L6,6V18Z" },
            new VectorIconItem { Key = "PrevTrack", Category = "多媒体与系统", DisplayName = "上一曲 (Prev)", SvgData = "M6,6H8V18H6M9.5,12L18,18V6L9.5,12Z" },
            new VectorIconItem { Key = "Lock", Category = "多媒体与系统", DisplayName = "锁定电脑 (Lock)", SvgData = "M18,8H17V6A5,5,0,0,0,7,6V8H6A2,2,0,0,0,4,10V20A2,2,0,0,0,6,22H18A2,2,0,0,0,20,20V10A2,2,0,0,0,18,8ZM9,6A3,3,0,0,1,15,6V8H9ZM18,20H6V10H18Z" },
            new VectorIconItem { Key = "Settings", Category = "多媒体与系统", DisplayName = "控制面板/设置 (Settings)", SvgData = "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z" },
            new VectorIconItem { Key = "Sleep", Category = "多媒体与系统", DisplayName = "系统睡眠 (Sleep)", SvgData = "M12.3,2A10,10 0 0,0 1.9,14.42C2.5,14.78 3.18,15 3.9,15A8.1,8.1 0 0,0 12,6.9C12,6.18 11.78,5.5 11.42,4.9A10,10 0 0,0 12.3,2Z" },
            new VectorIconItem { Key = "Restart", Category = "多媒体与系统", DisplayName = "重启电脑 (Restart)", SvgData = "M12,4V1L8,5L12,9V6A6,6 0 0,1 18,12A6,6 0 0,1 12,18A6,6 0 0,1 6.34,14H4.26A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4Z" },
            new VectorIconItem { Key = "Shutdown", Category = "多媒体与系统", DisplayName = "关闭电脑 (Shutdown)", SvgData = "M16.56,5.44L15.11,6.89C16.84,8.14 18,10.16 18,12.5A6,6 0 0,1 12,18.5A6,6 0 0,1 6,12.5C6,10.16 7.16,8.14 8.89,6.89L7.44,5.44C5.36,6.99 4,9.59 4,12.5A8,8 0 0,0 12,20.5A8,8 0 0,0 20,12.5C20,9.59 18.64,6.99 16.56,5.44M13,3H11V13H13V3Z" },

            // Productivity & Tools
            new VectorIconItem { Key = "TaskManager", Category = "生产力工具", DisplayName = "任务管理器 (Task Manager)", SvgData = "M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M7,10H9V17H7V10M11,7H13V17H11V7M15,13H17V17H15V13Z" },
            new VectorIconItem { Key = "Explorer", Category = "生产力工具", DisplayName = "文件资源管理器 (Explorer)", SvgData = "M19,20H5A2,2 0 0,1 3,18V6A2,2 0 0,1 5,4H10L12,6H19A2,2 0 0,1 21,8V18A2,2 0 0,1 19,20M5,8V18H19V8H5Z" },
            new VectorIconItem { Key = "Folder", Category = "生产力工具", DisplayName = "打开文件夹 (Folder)", SvgData = "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z" },
            new VectorIconItem { Key = "ClipboardHistory", Category = "生产力工具", DisplayName = "剪贴板历史 (Clipboard History)", SvgData = "M19,3H14.82C14.4,1.84 13.3,1 12,1C10.7,1 9.6,1.84 9.18,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M12,3A1,1 0 0,1 13,4A1,1 0 0,1 12,5A1,1 0 0,1 11,4A1,1 0 0,1 12,3M7,7H17V9H7V7M7,11H17V13H7V11M7,15H14V17H7V15Z" },
            new VectorIconItem { Key = "RunDialog", Category = "生产力工具", DisplayName = "运行窗口 (Run Dialog)", SvgData = "M20,4H4A2,2 0 0,0 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V6A2,2 0 0,0 20,4M20,18H4V8H20V18M6,14L10,11L6,8V14M11,15H17V13H11V15Z" },
            new VectorIconItem { Key = "Terminal", Category = "生产力工具", DisplayName = "命令行终端 (Terminal)", SvgData = "M20,4H4A2,2 0 0,0 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V6A2,2 0 0,0 20,4M20,18H4V8H20V18M6,10L10,13L6,16V10M11,16H17V14H11V16Z" },
            new VectorIconItem { Key = "Code", Category = "生产力工具", DisplayName = "代码编程 (Code)", SvgData = "M14.6,16.6L19.2,12L14.6,7.4L16,6L22,12L16,18L14.6,16.6M9.4,16.6L4.8,12L9.4,7.4L8,6L2,12L8,18L9.4,16.6Z" },
            new VectorIconItem { Key = "Calculator", Category = "生产力工具", DisplayName = "计算器 (Calculator)", SvgData = "M19,3H5C3.9,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.9 20.1,3 19,3M19,7H5V5H19V7M7,9H9V11H7V9M11,9H13V11H11V9M15,9H17V11H15V9M7,13H9V15H7V13M11,13H13V15H11V13M15,13H17V15H15V13M7,17H9V19H7V17M11,17H13V19H11V17M15,17H17V19H15V17Z" }
        };

        private static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static IconHelper()
        {
            foreach (var item in VectorIconList)
            {
                IconMap[item.Key] = item.SvgData;
            }
        }

        public static string? GetSvgPathByKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (IconMap.TryGetValue(key, out string? svg)) return svg;
            return null;
        }

        #endregion

        #region Geometry Creation Helpers for Advanced Shapes

        public static Geometry CreateAdvancedSectorGeometry(
            double cx, double cy, 
            double startAngle, double endAngle, 
            double innerR, double outerR, 
            string shape, double gap = 0, double cornerRadius = 0)
        {
            double midAngle = (startAngle + endAngle) / 2.0;
            double midAngleRad = midAngle * (Math.PI / 180.0);
            double layoutR = (innerR + outerR) / 2.0;
            double lx = cx + Math.Cos(midAngleRad) * layoutR;
            double ly = cy + Math.Sin(midAngleRad) * layoutR;
            double sectorAngleSpan = Math.Abs(endAngle - startAngle);
            double sectorHalfSpanRad = (sectorAngleSpan / 2.0) * (Math.PI / 180.0);

            // Distance between adjacent sector centers along the chord
            double chord = 2.0 * layoutR * Math.Sin(sectorHalfSpanRad);
            double radialSpan = Math.Max(12.0, (outerR - innerR) - gap);
            double tangentialSpan = Math.Max(12.0, chord - gap);

            if (shape == "Circle")
            {
                double diameter = Math.Max(12.0, Math.Min(radialSpan, tangentialSpan) * 0.94);
                return new EllipseGeometry(new Point(lx, ly), diameter / 2.0, diameter / 2.0);
            }
            else if (shape == "HexagonHive")
            {
                double hexRadius = Math.Max(8.0, Math.Min(radialSpan / 2.0, (tangentialSpan / Math.Sqrt(3.0))) * 0.94);
                var hexGeom = CreateHexagonGeometry(lx, ly, hexRadius);
                hexGeom.Transform = new RotateTransform(midAngle, lx, ly);
                return hexGeom;
            }
            else if (shape == "RoundedCapsule" || shape == "FloatingCapsules" || shape == "RoundedRect" || shape == "Capsule")
            {
                double w = Math.Max(16.0, radialSpan * 0.96);
                double h = Math.Max(16.0, Math.Min(w * 0.82, tangentialSpan * 0.88));
                double r = cornerRadius > 0 ? Math.Min(h / 2.0, cornerRadius + 2.0) : Math.Min(h / 2.0, 10.0);
                var rectGeom = new RectangleGeometry(new Rect(lx - w / 2.0, ly - h / 2.0, w, h), r, r);
                rectGeom.Transform = new RotateTransform(midAngle, lx, ly);
                return rectGeom;
            }
            else
            {
                // Standard or Optical Gap/Fillet Sector
                double effStartAngle = startAngle;
                double effEndAngle = endAngle;

                if (gap > 0 && layoutR > 0)
                {
                    double angularGap = (gap / layoutR) * (180.0 / Math.PI);
                    if (angularGap < sectorAngleSpan * 0.6)
                    {
                        effStartAngle += angularGap / 2.0;
                        effEndAngle -= angularGap / 2.0;
                    }
                }

                return CreateStandardSectorGeometry(cx, cy, effStartAngle, effEndAngle, innerR, outerR);
            }
        }

        private static Geometry CreateHexagonGeometry(double cx, double cy, double radius)
        {
            var figure = new PathFigure { IsClosed = true, IsFilled = true };
            for (int i = 0; i < 6; i++)
            {
                double a = i * 60.0 * (Math.PI / 180.0);
                Point pt = new Point(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a));
                if (i == 0) figure.StartPoint = pt;
                else figure.Segments.Add(new LineSegment(pt, true));
            }
            var geom = new PathGeometry();
            geom.Figures.Add(figure);
            return geom;
        }

        private static Geometry CreateStandardSectorGeometry(double cx, double cy, double startAngle, double endAngle, double innerRadius, double outerRadius)
        {
            double startRad = startAngle * (Math.PI / 180.0);
            double endRad = endAngle * (Math.PI / 180.0);

            Point p1 = new Point(cx + Math.Cos(startRad) * outerRadius, cy + Math.Sin(startRad) * outerRadius);
            Point p2 = new Point(cx + Math.Cos(endRad) * outerRadius, cy + Math.Sin(endRad) * outerRadius);
            Point p3 = new Point(cx + Math.Cos(endRad) * innerRadius, cy + Math.Sin(endRad) * innerRadius);
            Point p4 = new Point(cx + Math.Cos(startRad) * innerRadius, cy + Math.Sin(startRad) * innerRadius);

            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180.0;

            var figure = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
            figure.Segments.Add(new ArcSegment(p2, new Size(Math.Max(1.0, outerRadius), Math.Max(1.0, outerRadius)), 0, isLargeArc, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(p3, true));
            figure.Segments.Add(new ArcSegment(p4, new Size(Math.Max(1.0, innerRadius), Math.Max(1.0, innerRadius)), 0, isLargeArc, SweepDirection.Counterclockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        #endregion

        #region Center Core Icon Geometries

        public static Geometry GetCoreIconGeometry(string? coreIconType, string? customKey = null, string? customSvg = null)
        {
            string type = string.IsNullOrEmpty(coreIconType) ? "Exit" : coreIconType;

            switch (type)
            {
                case "Crosshair":
                    return Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M11,6V11H6V13H11V18H13V13H18V11H13V6H11Z");

                case "Windows":
                    return Geometry.Parse("M3,12V6.75L9,5.92V12M20,3V12H10V5.78L20,3M3,13H9V18.08L3,17.25M10,13H20V21L10,18.22");

                case "Dot":
                case "Bullseye":
                    return Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7Z");

                case "Home":
                    return Geometry.Parse("M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z");

                case "Power":
                    return Geometry.Parse("M16.56,5.44L15.11,6.89C16.84,8.14 18,10.16 18,12.5A6,6 0 0,1 12,18.5A6,6 0 0,1 6,12.5C6,10.16 7.16,8.14 8.89,6.89L7.44,5.44C5.36,6.99 4,9.59 4,12.5A8,8 0 0,0 12,20.5A8,8 0 0,0 20,12.5C20,9.59 18.64,6.99 16.56,5.44M13,3H11V13H13V3Z");

                case "Compass":
                    return Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M14.19,14.19L6,18L9.81,9.81L18,6L14.19,14.19M12,10.9A1.1,1.1 0 0,0 10.9,12A1.1,1.1 0 0,0 12,13.1A1.1,1.1 0 0,0 13.1,12A1.1,1.1 0 0,0 12,10.9Z");

                case "CatPaw":
                    return Geometry.Parse("M12,14.5C10.5,14.5 9,15.5 8.5,17C8,18.5 9,20 10.5,20.5C11.5,20.8 12.5,20.8 13.5,20.5C15,20 16,18.5 15.5,17C15,15.5 13.5,14.5 12,14.5M6,12.5A2,2 0 0,0 4,14.5A2,2 0 0,0 6,16.5A2,2 0 0,0 8,14.5A2,2 0 0,0 6,12.5M18,12.5A2,2 0 0,0 16,14.5A2,2 0 0,0 18,16.5A2,2 0 0,0 20,14.5A2,2 0 0,0 18,12.5M9.5,8.5A2,2 0 0,0 7.5,10.5A2,2 0 0,0 9.5,12.5A2,2 0 0,0 11.5,10.5A2,2 0 0,0 9.5,8.5M14.5,8.5A2,2 0 0,0 12.5,10.5A2,2 0 0,0 14.5,12.5A2,2 0 0,0 16.5,10.5A2,2 0 0,0 14.5,8.5Z");

                case "Custom":
                    if (!string.IsNullOrEmpty(customSvg))
                    {
                        try { return Geometry.Parse(customSvg); } catch { }
                    }
                    if (!string.IsNullOrEmpty(customKey))
                    {
                        string? data = GetSvgPathByKey(customKey);
                        if (!string.IsNullOrEmpty(data))
                        {
                            try { return Geometry.Parse(data); } catch { }
                        }
                    }
                    return Geometry.Parse("M12,2L15.09,8.26L22,9.27L17,14.14L18.18,21.02L12,17.77L5.82,21.02L7,14.14L2,9.27L8.91,8.26L12,2Z"); // Star

                case "Exit":
                default:
                    return Geometry.Parse("M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z");
            }
        }

        #endregion

        #region Shortcut Resolution & Pure Clean Icon Extraction

        /// <summary>
        /// Resolves a Windows shortcut (.lnk) to its real target path and icon location.
        /// </summary>
        public static bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex)
        {
            targetPath = "";
            iconPath = "";
            iconIndex = 0;

            if (string.IsNullOrEmpty(lnkPath) || !File.Exists(lnkPath))
                return false;

            try
            {
                var shellLink = new ShellLink();
                var persistFile = (IPersistFile)shellLink;
                persistFile.Load(lnkPath, 0);

                var link = (IShellLinkW)shellLink;

                // 1. Check custom icon location from the shortcut
                var iconBuf = new StringBuilder(260);
                link.GetIconLocation(iconBuf, iconBuf.Capacity, out iconIndex);
                string rawIcon = Environment.ExpandEnvironmentVariables(iconBuf.ToString().Trim());
                if (!string.IsNullOrEmpty(rawIcon) && (File.Exists(rawIcon) || Directory.Exists(rawIcon)))
                {
                    iconPath = rawIcon;
                }

                // 2. Get target executable/file path
                var pathBuf = new StringBuilder(260);
                link.GetPath(pathBuf, pathBuf.Capacity, IntPtr.Zero, 0);
                string rawTarget = Environment.ExpandEnvironmentVariables(pathBuf.ToString().Trim());
                if (!string.IsNullOrEmpty(rawTarget))
                {
                    targetPath = rawTarget;
                }

                return !string.IsNullOrEmpty(targetPath) || !string.IsNullOrEmpty(iconPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to resolve shortcut '{lnkPath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves a pure, clean, high-resolution application icon without Windows shortcut arrow badge overlays.
        /// </summary>
        public static BitmapSource? GetIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                string resolvedPath = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

                // 1. If it's a shortcut (.lnk), resolve to the actual target executable or icon file
                if (resolvedPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    if (ResolveShortcutTarget(resolvedPath, out string targetPath, out string iconPath, out int iconIndex))
                    {
                        // Priority A: Custom icon file specified in shortcut
                        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                        {
                            var icon = ExtractPureIconFromFile(iconPath, iconIndex);
                            if (icon != null) return icon;
                        }

                        // Priority B: Target executable pure icon
                        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                        {
                            var icon = ExtractPureIconFromFile(targetPath, 0);
                            if (icon != null) return icon;
                        }
                    }
                }

                // 2. Extract from standard file or directory path
                if (File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
                {
                    var icon = ExtractPureIconFromFile(resolvedPath, 0);
                    if (icon != null) return icon;
                }

                // 3. Fallback: extract via file attributes if file doesn't physically exist
                SHFILEINFO shinfoAttr = new SHFILEINFO();
                IntPtr hImgAttr = SHGetFileInfo(resolvedPath, 256, ref shinfoAttr, (uint)Marshal.SizeOf(shinfoAttr), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
                if (shinfoAttr.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            shinfoAttr.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(shinfoAttr.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to extract icon for '{path}': {ex.Message}");
            }

            return null;
        }

        private static BitmapSource? ExtractPureIconFromFile(string filePath, int iconIndex)
        {
            try
            {
                // First try ExtractIconEx to get large unbadged icon
                uint count = ExtractIconEx(filePath, iconIndex, out IntPtr hIconLarge, out IntPtr hIconSmall, 1);
                if (count > 0 && hIconLarge != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            hIconLarge,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(hIconLarge);
                        if (hIconSmall != IntPtr.Zero) DestroyIcon(hIconSmall);
                    }
                }
                else if (count > 0 && hIconSmall != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            hIconSmall,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(hIconSmall);
                    }
                }

                // Fallback to SHGetFileInfo
                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion

        #region Custom User Icons (SVG & Raster Image Import)

        public class CustomIconItem
        {
            public string Key { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string FilePath { get; set; } = "";
            public string SvgData { get; set; } = "";
            public bool IsSvg => !string.IsNullOrEmpty(SvgData);
        }

        private static List<CustomIconItem>? _cachedCustomIcons;

        public static string GetCustomIconsDirectory()
        {
            // T16：应用数据目录经 AppDataPaths 解析（dev 沙箱语义不变），不再依赖静态配置门面。
            string dir = Path.Combine(AppDataPaths.GetAppDataFolder(), "CustomIcons");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static List<CustomIconItem> GetCustomIcons()
        {
            if (_cachedCustomIcons != null) return _cachedCustomIcons;

            var list = new List<CustomIconItem>();
            try
            {
                string dir = GetCustomIconsDirectory();
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir);
                    foreach (var file in files)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        string key = "custom:" + fileName;

                        if (ext == ".svg")
                        {
                            try
                            {
                                string text = File.ReadAllText(file);
                                string pathData = ExtractSvgPathData(text);
                                list.Add(new CustomIconItem
                                {
                                    Key = key,
                                    DisplayName = fileName,
                                    FilePath = file,
                                    SvgData = pathData
                                });
                            }
                            catch { }
                        }
                        else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".ico" || ext == ".bmp" || ext == ".webp")
                        {
                            list.Add(new CustomIconItem
                            {
                                Key = key,
                                DisplayName = fileName,
                                FilePath = file,
                                SvgData = ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetCustomIcons Error]: {ex.Message}");
            }

            _cachedCustomIcons = list;
            return list;
        }

        public static string ExtractSvgPathData(string svgContent)
        {
            if (string.IsNullOrWhiteSpace(svgContent)) return "";

            // Check if it's already a pure path string
            if (svgContent.Trim().StartsWith("M", StringComparison.OrdinalIgnoreCase) && !svgContent.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            {
                return svgContent.Trim();
            }

            try
            {
                int dIndex = svgContent.IndexOf(" d=\"", StringComparison.OrdinalIgnoreCase);
                if (dIndex < 0) dIndex = svgContent.IndexOf(" d='", StringComparison.OrdinalIgnoreCase);
                if (dIndex >= 0)
                {
                    char quote = svgContent[dIndex + 3];
                    int start = dIndex + 4;
                    int end = svgContent.IndexOf(quote, start);
                    if (end > start)
                    {
                        return svgContent.Substring(start, end - start).Trim();
                    }
                }
            }
            catch { }

            return "";
        }

        public static CustomIconItem? ImportCustomIcon(string sourceFilePath, string? customName = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                return null;

            try
            {
                string dir = GetCustomIconsDirectory();
                string ext = Path.GetExtension(sourceFilePath).ToLower();
                string safeName = string.IsNullOrWhiteSpace(customName) 
                    ? Path.GetFileNameWithoutExtension(sourceFilePath)
                    : customName.Trim();

                // Clean illegal chars
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }

                string targetFileName = $"{safeName}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                string targetPath = Path.Combine(dir, targetFileName);

                File.Copy(sourceFilePath, targetPath, true);

                _cachedCustomIcons = null; // Invalidate cache
                var all = GetCustomIcons();
                return all.FirstOrDefault(i => i.FilePath == targetPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportCustomIcon Error]: {ex.Message}");
                return null;
            }
        }

        public static CustomIconItem? ImportCustomSvgData(string svgPathData, string iconName)
        {
            if (string.IsNullOrWhiteSpace(svgPathData)) return null;

            try
            {
                string dir = GetCustomIconsDirectory();
                string safeName = string.IsNullOrWhiteSpace(iconName) ? "Vector" : iconName.Trim();
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }

                string targetFileName = $"{safeName}_{DateTime.Now:yyyyMMddHHmmss}.svg";
                string targetPath = Path.Combine(dir, targetFileName);

                File.WriteAllText(targetPath, svgPathData.Trim());

                _cachedCustomIcons = null;
                var all = GetCustomIcons();
                return all.FirstOrDefault(i => i.FilePath == targetPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportCustomSvgData Error]: {ex.Message}");
                return null;
            }
        }

        public static bool DeleteCustomIcon(string key)
        {
            try
            {
                var item = GetCustomIcons().FirstOrDefault(i => i.Key == key);
                if (item != null && File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                    _cachedCustomIcons = null;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static ImageSource? GetCustomImageSource(string iconKeyOrPath)
        {
            if (string.IsNullOrWhiteSpace(iconKeyOrPath)) return null;

            try
            {
                string path = iconKeyOrPath;
                if (iconKeyOrPath.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                {
                    var item = GetCustomIcons().FirstOrDefault(i => i.Key == iconKeyOrPath);
                    if (item != null) path = item.FilePath;
                }

                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLower();
                    if (ext != ".svg")
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = new Uri(path, UriKind.Absolute);
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion

    }
}
