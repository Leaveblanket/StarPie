using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures.Services.Programs
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

    /// <summary>
    /// 程序快捷方式解析出口（模块 M3「程序扫描与目录」，R6/ADR-0015 三分）：把 Windows 快捷方式
    /// （.lnk）解析为真实目标路径与图标位置。消费方 ProgramScanner/ProgramPickerViewModel 自
    /// T3c/#67 起直连本出口；T3d/#68 起旧入口删除。
    /// </summary>
    public static class ShortcutResolver
    {
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
    }
}
