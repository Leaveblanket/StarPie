using System.Diagnostics;

namespace StarPie.PluginRuntime.Discovery
{
    /// <summary>
    /// 插件包发现：按「安装目录 → 用户目录」顺序扫描两处的候选包目录，并做发现期包内容检查。
    /// </summary>
    /// <remarks>
    /// 候选包 = 目录下存在 <c>plugin.json</c> 的子目录；目录缺失（插件缺席）是正常态，返回空列表。
    /// 本层只做发现与包内容检查，不解析清单、不做准入判定，也不裁决同 id 冲突——发现结果保留全部
    /// 候选，由上层按 id 归并并拒绝冲突双方。
    /// </remarks>
    public sealed class PluginDiscovery
    {
        /// <summary>清单文件名。</summary>
        private const string ManifestFileName = "plugin.json";

        /// <summary>
        /// 包内禁止出现的宿主程序集文件名：共享契约与宿主实现不随包分发，
        /// 随包分发会让同一契约在进程内出现第二份类型身份。
        /// 内核不引用 WPF 契约面，故共享契约名在本层按文件名判定。
        /// </summary>
        private static readonly string[] ForbiddenAssemblyFileNames =
        {
            "StarPie.Sdk.dll",
            "StarPie.Sdk.Wpf.dll",
            "StarPie.Host.dll",
            "StarPie.dll",
        };

        /// <summary>构造发现器：两个目录都为绝对路径，顺序即发现优先级。</summary>
        public PluginDiscovery(string installDirectory, string userDirectory)
        {
            InstallDirectory = installDirectory;
            UserDirectory = userDirectory;
        }

        /// <summary>随包插件目录（发现顺序在前）。</summary>
        public string InstallDirectory { get; }

        /// <summary>用户插件目录（发现顺序在后）。</summary>
        public string UserDirectory { get; }

        /// <summary>扫描两个目录；返回按来源顺序排列的候选包（同一目录内按目录名稳定序）。</summary>
        public IReadOnlyList<PluginPackageCandidate> Discover()
        {
            var candidates = new List<PluginPackageCandidate>();
            Collect(InstallDirectory, PluginPackageOrigin.Install, candidates);
            Collect(UserDirectory, PluginPackageOrigin.User, candidates);
            return candidates;
        }

        private static void Collect(string rootDirectory, PluginPackageOrigin origin, List<PluginPackageCandidate> candidates)
        {
            if (!Directory.Exists(rootDirectory))
            {
                return;
            }

            string[] packageDirectories;
            try
            {
                packageDirectories = Directory.GetDirectories(rootDirectory);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to enumerate plugin directory '{rootDirectory}': {ex.Message}");
                return;
            }

            // 文件系统枚举顺序不稳定；排序保证报告与状态条目可复现。
            Array.Sort(packageDirectories, StringComparer.OrdinalIgnoreCase);

            foreach (string packageDirectory in packageDirectories)
            {
                string manifestPath = Path.Combine(packageDirectory, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    // 无清单的目录不是插件包（可能是插件数据、日志等）。
                    continue;
                }

                var violations = new List<string>();
                string? manifestJson = null;
                try
                {
                    manifestJson = File.ReadAllText(manifestPath);
                }
                catch (Exception ex)
                {
                    violations.Add($"plugin.json 读取失败：{ex.Message}");
                }

                foreach (string forbidden in FindForbiddenAssemblies(packageDirectory))
                {
                    violations.Add($"包内含宿主/SDK 程序集副本 {forbidden}：共享契约不随包分发");
                }

                candidates.Add(new PluginPackageCandidate(
                    Path.GetFileName(packageDirectory),
                    packageDirectory,
                    origin,
                    manifestJson,
                    violations));
            }
        }

        /// <summary>递归查找包内禁止出现的宿主程序集副本；去重后按名排序。</summary>
        private static List<string> FindForbiddenAssemblies(string packageDirectory)
        {
            var found = new List<string>();
            try
            {
                foreach (string file in Directory.EnumerateFiles(packageDirectory, "*.dll", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    if (Array.Exists(ForbiddenAssemblyFileNames, name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        found.Add(fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to scan plugin package '{packageDirectory}': {ex.Message}");
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
