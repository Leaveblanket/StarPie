using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using StarPie.Services.Icons;
using StarPie.Services.Programs;

namespace StarPie.Programs
{
    /// <summary>
    /// 内置程序来源：系统自带工具与开始菜单 / 桌面快捷方式。文件存在性 / 扩展名 / 大小检查在此
    /// 进行（IO 性质），垃圾过滤、跨源去重与显示名升级委托 <see cref="ProgramCatalog"/> 纯函数。
    /// </summary>
    /// <remarks>
    /// 更深的来源（用户 AppData、WindowsApps、注册表 App Paths 与 Uninstall、Program Files 顶层）
    /// 由随包插件提供，与内置来源同走「程序来源」能力契约（见 <see cref="ProgramSourceCapability"/>）；
    /// 插件缺席或停用时本来源仍在，程序选择器不会空转——这是该扩展点的降级行为。
    /// .lnk 解析经注入的 <see cref="IShortcutTargetResolver"/> 完成；返回条目不含图标
    /// （纯数据，图标由 UI 消费方按路径装配）。本类保持集成性质，不做单元测试。
    /// 扫描来源依赖开始菜单等 Windows 设施，运行时仅限 Windows；
    /// 宿主内核为平台中立 TFM（零 WPF 的编译期保证），故在此标注平台支持范围。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class ProgramScanner : IProgramScanner
    {
        private readonly IShortcutTargetResolver _shortcutResolver;

        /// <summary>构造注入扫描所需的 .lnk 解析契约。</summary>
        public ProgramScanner(IShortcutTargetResolver shortcutResolver)
        {
            _shortcutResolver = shortcutResolver ?? throw new ArgumentNullException(nameof(shortcutResolver));
        }

        /// <summary>扫描内置来源，按显示名排序返回去重后的候选程序
        /// （.lnk 解析经构造注入的契约完成）。</summary>
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        {
            var candidates = new List<ProgramEntry>();

            // 1. Windows 系统自带工具
            AddSystemApps(candidates);

            // 2. 开始菜单快捷方式（公共与用户）
            ScanStartMenuShortcuts(candidates, _shortcutResolver);

            // 3. 桌面快捷方式（公共与用户）
            ScanDesktopShortcuts(candidates, _shortcutResolver);

            // 跨源去重 + 显示名升级（纯函数），再按显示名做自然排序
            var list = ProgramCatalog.MergeSources(candidates);
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

            candidates.Add(new ProgramEntry(displayName, normalizedPath, normalizedPath));
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

        private static void ScanStartMenuShortcuts(List<ProgramEntry> candidates, IShortcutTargetResolver shortcutResolver)
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
                        if (shortcutResolver.ResolveShortcutTarget(file, out string targetPath, out _, out _))
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

        private static void ScanDesktopShortcuts(List<ProgramEntry> candidates, IShortcutTargetResolver shortcutResolver)
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

                        if (shortcutResolver.ResolveShortcutTarget(file, out string targetPath, out _, out _))
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
    }
}
