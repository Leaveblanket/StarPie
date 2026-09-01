using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace WinPieGestures.Services.Programs
{
    /// <summary>程序选择器的一条候选程序。T06 起 <c>ProgramItem</c> 提为不可变记录，
    /// 扫描候选阶段 <c>IconSource</c> 为 null，去重定名后再补图标。</summary>
    public sealed record ProgramEntry(string Name, string Path, string FriendlyPath, ImageSource? IconSource);

    /// <summary>
    /// 程序目录的纯规则函数 (T06, ADR-0004)：垃圾可执行判定、跨源去重、显示名升级与搜索过滤。
    /// 刻意不带 IO（文件存在性、注册表）——扫描 IO 由 <see cref="ProgramScanner"/> 编排并保持
    /// 集成性质不测；这里的规则全部是无副作用的字符串/路径判定，直接单测。
    /// </summary>
    public static class ProgramCatalog
    {
        /// <summary>
        /// 垃圾可执行判定（纯函数）：卸载器 / 安装器 / 更新器 / 诊断修复 / 内嵌框架 /
        /// Python 内部脚本 / 文档网页等辅助项一律排除。文件存在性、扩展名与大小检查
        /// 由扫描编排负责，不在此处。
        /// </summary>
        public static bool IsJunkExecutable(string displayName, string exePath)
        {
            string fileName = System.IO.Path.GetFileName(exePath).ToLowerInvariant();
            string lowerName = displayName.ToLowerInvariant();
            string combined = $"{lowerName} {fileName}";
            string lowerPath = exePath.ToLowerInvariant();

            // 1. 卸载器
            if (combined.Contains("uninstall") || combined.Contains("unins000") || combined.Contains("unins001") ||
                combined.Contains("uninst") || combined.Contains("卸载") || combined.Contains("remove") ||
                combined.Contains("deleter") || combined.Contains("cleanup"))
                return true;

            // 2. 安装器、安装向导与运行库
            if (combined.Contains("setup") || combined.Contains("installer") || combined.Contains("install_helper") ||
                combined.Contains("msiexec") || combined.Contains("vcredist") || combined.Contains("dxsetup") ||
                combined.Contains("dotnetfx") || combined.Contains("ndp4") || combined.Contains("vc_redist") ||
                combined.Contains("setup_helper") || combined.Contains("dpinst"))
                return true;

            // 3. 更新器、崩溃报告与反馈
            if (combined.Contains("update") || combined.Contains("updater") || combined.Contains("autoupdate") ||
                combined.Contains("patcher") || combined.Contains("crashpad") || combined.Contains("crash_report") ||
                combined.Contains("crashreporter") || combined.Contains("feedback") || combined.Contains("意见反馈") ||
                combined.Contains("bugreport"))
                return true;

            // 4. 诊断、修复与 CLI 辅助
            if (combined.Contains("diagnostic") || combined.Contains("repair") || combined.Contains("修复") ||
                combined.Contains("fix") || combined.Contains("troubleshoot") || combined.Contains("elevate") ||
                combined.Contains("helper") || combined.Contains("launcher_helper") || combined.Contains("nwjc") ||
                combined.Contains("chromedriver") || combined.Contains("geckodriver") || combined.Contains("phantomjs") ||
                combined.Contains("conhost") || combined.Contains("ffmpeg") || combined.Contains("ffprobe") ||
                combined.Contains("winpty") || combined.Contains("openconsole") || combined.Contains("rcedit") ||
                combined.Contains("language_server") || combined.Contains("webm_encoder") || combined.Contains("compil32") ||
                combined.Contains("iscc") || combined.Contains("islzma") || combined.Contains("iediag"))
                return true;

            // 5. 内嵌框架与 node 包
            if (lowerPath.Contains("\\resources\\") || lowerPath.Contains("\\node_modules\\") ||
                lowerPath.Contains("\\extensions\\") || lowerPath.Contains("\\site-packages\\") ||
                lowerPath.Contains("\\packages\\") || lowerPath.Contains("\\internal\\") ||
                lowerPath.Contains("\\temp\\") || lowerPath.Contains("\\tmp\\") ||
                lowerPath.Contains("\\cache\\") || lowerPath.Contains("\\plugins\\") ||
                lowerPath.Contains("\\sdk\\") || lowerPath.Contains("\\tcl\\") || lowerPath.Contains("\\scripts\\"))
                return true;

            // 6. Python 内部脚本（python.exe / pythonw.exe 除外）
            if (lowerPath.Contains("python") && (lowerPath.Contains("\\scripts\\") || lowerPath.Contains("\\site-packages\\") || lowerPath.Contains("\\tcl\\") || lowerPath.Contains("\\lib\\")))
            {
                if (fileName != "python.exe" && fileName != "pythonw.exe")
                    return true;
            }

            // 7. 文档与网页快捷方式
            if (combined.Contains("readme") || combined.Contains("license") || combined.Contains("changelog") ||
                combined.Contains("manual") || combined.Contains("使用说明") || combined.Contains("用户手册") ||
                combined.Contains("help") || combined.Contains("帮助") || combined.Contains("website") ||
                combined.Contains("官方网站") || combined.Contains("访问官网") || combined.Contains("homepage") ||
                combined.Contains("forum") || combined.Contains("bbs"))
                return true;

            return false;
        }

        /// <summary>
        /// 显示名升级判定（纯函数）：当前显示名只是裸 exe 名、而候选名更丰富（如带厂商前缀或
        /// 友好名）时允许升级；两者同为裸名或候选同样裸名时不升级。
        /// </summary>
        public static bool ShouldUpgradeDisplayName(string currentName, string candidateName, string path)
        {
            string rawExeName = System.IO.Path.GetFileNameWithoutExtension(path);
            return string.Equals(currentName, rawExeName, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(candidateName, rawExeName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 跨源去重（纯函数）：开始菜单 / 桌面 / 注册表 / Program Files 等来源会把同一 exe
        /// 以不同显示名重复上报；按路径（忽略大小写）合并，命中显示名升级规则时用更丰富的名字。
        /// 保持首次出现顺序。
        /// </summary>
        public static List<ProgramEntry> MergeSources(IEnumerable<ProgramEntry> candidates)
        {
            var merged = new List<ProgramEntry>();
            var indexByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                if (indexByPath.TryGetValue(candidate.Path, out var index))
                {
                    var existing = merged[index];
                    if (ShouldUpgradeDisplayName(existing.Name, candidate.Name, candidate.Path))
                    {
                        merged[index] = existing with { Name = candidate.Name };
                    }
                    continue;
                }

                indexByPath[candidate.Path] = merged.Count;
                merged.Add(candidate);
            }

            return merged;
        }

        /// <summary>
        /// 搜索过滤（纯函数）：按显示名、友好路径或 exe 文件名的忽略大小写包含匹配；
        /// 空过滤条件返回全部（保持原顺序）。
        /// </summary>
        public static List<ProgramEntry> FilterPrograms(IReadOnlyList<ProgramEntry> programs, string? filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return programs.ToList();
            }

            string lowerFilter = filter.Trim().ToLowerInvariant();
            return programs.Where(p =>
                p.Name.ToLowerInvariant().Contains(lowerFilter) ||
                p.FriendlyPath.ToLowerInvariant().Contains(lowerFilter) ||
                System.IO.Path.GetFileName(p.Path).ToLowerInvariant().Contains(lowerFilter))
                .ToList();
        }
    }
}
