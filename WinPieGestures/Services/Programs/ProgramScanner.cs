using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WinPieGestures.Services.Programs
{
    /// <summary>
    /// 已安装程序的扫描编排 (T06)：从旧 <c>ProgramPickerWindow</c> 原样迁出的八个来源——
    /// 系统自带工具、开始菜单 / 桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths
    /// 与 Uninstall、Program Files 顶层。存在性 / 扩展名 / 大小检查留在这里（IO 性质），
    /// 垃圾过滤、跨源去重与显示名升级委托 <see cref="ProgramCatalog"/> 纯函数。
    /// 按 ADR-0004 保持集成性质，不测。
    /// </summary>
    public static class ProgramScanner
    {
        /// <summary>扫描全部来源，按显示名排序返回去重后的候选程序（图标在此阶段补齐）。</summary>
        public static IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        {
            var candidates = new List<ProgramEntry>();

            // 1. Windows 系统自带工具
            AddSystemApps(candidates);

            // 2. 开始菜单快捷方式（公共与用户）
            ScanStartMenuShortcuts(candidates);

            // 3. 桌面快捷方式（公共与用户）
            ScanDesktopShortcuts(candidates);

            // 4. 用户 AppData\Local\Programs（VS Code、Discord、Spotify、Xmind 等）
            ScanUserAppDataPrograms(candidates);

            // 5. WindowsApps（Windows 10/11 UWP / 商店工具）
            ScanWindowsApps(candidates);

            // 6. 注册表 App Paths（64/32 位 HKLM、HKCU）
            ScanRegistryAppPaths(candidates);

            // 7. 注册表 Uninstall 项（64/32 位 HKLM、HKCU）
            ScanRegistryUninstall(candidates);

            // 8. Program Files 顶层目录（应用套件）
            ScanProgramFilesTopLevel(candidates);

            // 跨源去重 + 显示名升级（纯函数），再按显示名做自然排序
            var merged = ProgramCatalog.MergeSources(candidates);
            var list = merged
                .Select(e => e with { IconSource = IconAssets.GetIcon(e.Path) })
                .ToList();
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        /// <summary>候选准入门槛：文件存在、是 .exe、非空文件且非垃圾辅助项（IO 检查 + 纯规则）。</summary>
        private static bool IsValidCandidate(string displayName, string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return false;

            if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var fileInfo = new FileInfo(exePath);
                if (fileInfo.Length == 0) return false;
            }
            catch
            {
                return false;
            }

            return !ProgramCatalog.IsJunkExecutable(displayName, exePath);
        }

        /// <summary>规范化路径并按准入门槛收集候选；路径重复留给 <see cref="ProgramCatalog.MergeSources"/> 处理。</summary>
        private static void AddCandidate(List<ProgramEntry> candidates, string displayName, string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(exePath);
            }
            catch
            {
                normalizedPath = exePath;
            }

            if (!IsValidCandidate(displayName, normalizedPath))
                return;

            candidates.Add(new ProgramEntry(displayName, normalizedPath, normalizedPath, IconSource: null));
        }

        private static void AddSystemApps(List<ProgramEntry> candidates)
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

            AddCandidate(candidates, "文件资源管理器 (Explorer)", Path.Combine(winDir, "explorer.exe"));
            AddCandidate(candidates, "记事本 (Notepad)", Path.Combine(sysDir, "notepad.exe"));
            AddCandidate(candidates, "任务管理器 (Taskmgr)", Path.Combine(sysDir, "taskmgr.exe"));
            AddCandidate(candidates, "计算器 (Calculator)", Path.Combine(sysDir, "calc.exe"));
            AddCandidate(candidates, "截图工具 (SnippingTool)", Path.Combine(sysDir, "SnippingTool.exe"));
            AddCandidate(candidates, "命令提示符 (CMD)", Path.Combine(sysDir, "cmd.exe"));
            AddCandidate(candidates, "Windows PowerShell", Path.Combine(sysDir, @"WindowsPowerShell\v1.0\powershell.exe"));
            AddCandidate(candidates, "画图 (MSPaint)", Path.Combine(sysDir, "mspaint.exe"));
            AddCandidate(candidates, "注册表编辑器 (Regedit)", Path.Combine(winDir, "regedit.exe"));
            AddCandidate(candidates, "控制面板 (Control Panel)", Path.Combine(sysDir, "control.exe"));
        }

        private static void ScanStartMenuShortcuts(List<ProgramEntry> candidates)
        {
            var searchDirs = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs")
            };

            foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        // 解析目标路径并拒绝失效快捷方式
                        if (ShortcutResolver.ResolveShortcutTarget(file, out string targetPath, out _, out _))
                        {
                            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath) && targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                AddCandidate(candidates, name, targetPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to scan Start Menu in {dir}: {ex.Message}");
                }
            }
        }

        private static void ScanDesktopShortcuts(List<ProgramEntry> candidates)
        {
            var searchDirs = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    var files = Directory.GetFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        if (ShortcutResolver.ResolveShortcutTarget(file, out string targetPath, out _, out _))
                        {
                            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath) && targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                AddCandidate(candidates, name, targetPath);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static void ScanUserAppDataPrograms(List<ProgramEntry> candidates)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localPrograms = Path.Combine(localAppData, "Programs");
                if (Directory.Exists(localPrograms))
                {
                    foreach (string appDir in Directory.GetDirectories(localPrograms))
                    {
                        string appName = Path.GetFileName(appDir);
                        try
                        {
                            // 只搜应用目录顶层
                            foreach (string exe in Directory.GetFiles(appDir, "*.exe", SearchOption.TopDirectoryOnly))
                            {
                                string displayName = string.Equals(Path.GetFileNameWithoutExtension(exe), appName, StringComparison.OrdinalIgnoreCase)
                                    ? appName
                                    : $"{appName} ({Path.GetFileNameWithoutExtension(exe)})";

                                AddCandidate(candidates, displayName, exe);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void ScanWindowsApps(List<ProgramEntry> candidates)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string winApps = Path.Combine(localAppData, @"Microsoft\WindowsApps");
                if (Directory.Exists(winApps))
                {
                    foreach (string exe in Directory.GetFiles(winApps, "*.exe", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileNameWithoutExtension(exe);
                        AddCandidate(candidates, name, exe);
                    }
                }
            }
            catch { }
        }

        private static void ScanRegistryAppPaths(List<ProgramEntry> candidates)
        {
            var hives = new[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64),
                (RegistryHive.LocalMachine, RegistryView.Registry32),
                (RegistryHive.CurrentUser, RegistryView.Default)
            };

            foreach (var (hive, view) in hives)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var appPaths = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                    if (appPaths == null) continue;

                    foreach (string subKeyName in appPaths.GetSubKeyNames())
                    {
                        try
                        {
                            using var key = appPaths.OpenSubKey(subKeyName);
                            string? defaultVal = key?.GetValue("")?.ToString();
                            if (string.IsNullOrEmpty(defaultVal)) continue;

                            string exePath = Environment.ExpandEnvironmentVariables(defaultVal.Trim().Trim('"'));
                            if (!File.Exists(exePath)) continue;

                            string name = Path.GetFileNameWithoutExtension(subKeyName);
                            AddCandidate(candidates, name, exePath);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static void ScanRegistryUninstall(List<ProgramEntry> candidates)
        {
            var hives = new[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64),
                (RegistryHive.LocalMachine, RegistryView.Registry32),
                (RegistryHive.CurrentUser, RegistryView.Default)
            };

            foreach (var (hive, view) in hives)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (uninstall == null) continue;

                    foreach (string subKeyName in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using var key = uninstall.OpenSubKey(subKeyName);
                            if (key == null) continue;

                            // 跳过系统组件与更新
                            object? sysComponent = key.GetValue("SystemComponent");
                            if (sysComponent is int sc && sc == 1) continue;
                            if (key.GetValue("ParentKeyName") != null) continue;

                            string? displayName = key.GetValue("DisplayName")?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(displayName)) continue;

                            // 跳过 Windows 安全更新与运行库
                            if (displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                                displayName.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase) ||
                                displayName.StartsWith("Microsoft Visual C++", StringComparison.OrdinalIgnoreCase) ||
                                displayName.StartsWith("Windows Software Development Kit", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string? displayIcon = key.GetValue("DisplayIcon")?.ToString();
                            string? installLocation = key.GetValue("InstallLocation")?.ToString();

                            string exePath = "";
                            if (!string.IsNullOrEmpty(displayIcon))
                            {
                                string raw = displayIcon.Split(',')[0].Trim().Trim('"');
                                string expanded = Environment.ExpandEnvironmentVariables(raw);
                                if (File.Exists(expanded) && expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    exePath = expanded;
                                }
                            }

                            if (string.IsNullOrEmpty(exePath) && !string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                            {
                                try
                                {
                                    var exes = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                                    var mainExe = exes.FirstOrDefault(e => IsValidCandidate(displayName, e));
                                    if (mainExe != null) exePath = mainExe;
                                }
                                catch { }
                            }

                            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                            {
                                AddCandidate(candidates, displayName, exePath);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static void ScanProgramFilesTopLevel(List<ProgramEntry> candidates)
        {
            var programFilesDirs = new List<string>();
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (Directory.Exists(pf)) programFilesDirs.Add(pf);
            if (Directory.Exists(pf86) && !string.Equals(pf, pf86, StringComparison.OrdinalIgnoreCase)) programFilesDirs.Add(pf86);

            foreach (var rootPf in programFilesDirs)
            {
                try
                {
                    foreach (var vendorDir in Directory.GetDirectories(rootPf))
                    {
                        string vendorName = Path.GetFileName(vendorDir);
                        if (vendorName.Equals("Common Files", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Defender", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Mail", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Media Player", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows NT", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Photo Viewer", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("WindowsPowerShell", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // 只看顶层
                        try
                        {
                            foreach (var exe in Directory.GetFiles(vendorDir, "*.exe", SearchOption.TopDirectoryOnly))
                            {
                                AddCandidate(candidates, $"{vendorName} ({Path.GetFileNameWithoutExtension(exe)})", exe);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}
