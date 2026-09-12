using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using StarPie.Services.Programs;

namespace StarPie.Plugin.Programs
{
    /// <summary>
    /// 插件侧的程序扫描：用户 AppData\Programs、WindowsApps、注册表 App Paths 与 Uninstall、
    /// Program Files 顶层——宿主内置来源（系统工具 + 快捷方式）之外的全部已安装程序来源。
    /// </summary>
    /// <remarks>
    /// 只依赖 BCL 与注册表（不引宿主实现、不做 .lnk 解析）：候选准入门槛（存在性 / 扩展名 / 大小 /
    /// 垃圾过滤）在此，跨源去重与显示名升级委托 SDK 的 <see cref="ProgramCatalog"/> 纯函数。
    /// 应用数据目录按宿主同一约定解析（<c>LOCALAPPDATA</c> 环境变量优先，其次已知文件夹），
    /// dev 实例与 e2e 沙箱因此对本插件同样生效。
    /// 候选收集与宿主内置来源的实现同形但有意各留一份：插件只经 SDK 与宿主交互，
    /// 共享实现会把插件拖进宿主内部依赖，违反包内只带 SDK 的边界。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal sealed class InstalledProgramScanner
    {
        /// <summary>扫描全部插件来源，按显示名排序返回去重后的候选程序。</summary>
        internal IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        {
            var candidates = new List<ProgramEntry>();

            // 1. 用户 AppData\Local\Programs（VS Code、Discord、Spotify、Xmind 等）
            ScanUserAppDataPrograms(candidates);

            // 2. WindowsApps（Windows 10/11 UWP / 商店工具）
            ScanWindowsApps(candidates);

            // 3. 注册表 App Paths（64/32 位 HKLM、HKCU）
            ScanRegistryAppPaths(candidates);

            // 4. 注册表 Uninstall 项（64/32 位 HKLM、HKCU）
            ScanRegistryUninstall(candidates);

            // 5. Program Files 顶层目录（应用套件）
            ScanProgramFilesTopLevel(candidates);

            var list = ProgramCatalog.MergeSources(candidates);
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        /// <summary>应用数据目录下的本地程序目录（宿主同约定：环境变量优先，其次已知文件夹）。</summary>
        private static string LocalAppDataDirectory
        {
            get
            {
                string? fromEnvironment = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                return string.IsNullOrEmpty(fromEnvironment)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    : fromEnvironment;
            }
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

        /// <summary>规范化路径并按准入门槛收集候选；路径重复交给 <see cref="ProgramCatalog.MergeSources"/> 处理。</summary>
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

            candidates.Add(new ProgramEntry(displayName, normalizedPath, normalizedPath));
        }

        private static void ScanUserAppDataPrograms(List<ProgramEntry> candidates)
        {
            try
            {
                string localPrograms = Path.Combine(LocalAppDataDirectory, "Programs");
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
                string winApps = Path.Combine(LocalAppDataDirectory, @"Microsoft\WindowsApps");
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
