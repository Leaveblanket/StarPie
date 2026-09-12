using StarPie.Manifest;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Discovery;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.State;

namespace StarPie.PluginRuntime.Diagnostics
{
    /// <summary>
    /// 插件启动扫描：读宿主状态 → 发现 → 解析/校验 → 签名校验 → 准入判定 → 状态刷新 → 启动报告落盘。
    /// </summary>
    /// <remarks>
    /// 只做发现、准入与状态记录，不装载任何插件代码。每次启动全量重扫，准入结果按当前准入环境
    /// （内置清单 / 审核清单 / 开发者模式）与当前清单的撤销状态重新判定；用户的启用停用意图与
    /// 隔离状态由宿主状态持有，扫描只刷新版本、路径、准入结果与签名主体，不覆盖前者。
    /// </remarks>
    public sealed class PluginStartupScanner
    {
        private readonly PluginDiscovery _discovery;
        private readonly PluginAdmissionPolicy _admissionPolicy;
        private readonly PluginStateStore _stateStore;
        private readonly PluginStartupReportWriter _reportWriter;
        private readonly IPluginSignatureVerifier? _signatureVerifier;

        /// <summary>构造扫描器：发现器、准入策略、宿主状态与报告写盘器均为显式依赖。</summary>
        /// <param name="discovery">包发现器。</param>
        /// <param name="admissionPolicy">准入策略。</param>
        /// <param name="stateStore">宿主状态。</param>
        /// <param name="reportWriter">启动报告写盘器。</param>
        /// <param name="signatureVerifier">
        /// 包签名校验器；缺省 null 时不做签名校验，签名可信判定一律失败（内置与开发者模式路径不受影响）。
        /// </param>
        public PluginStartupScanner(
            PluginDiscovery discovery,
            PluginAdmissionPolicy admissionPolicy,
            PluginStateStore stateStore,
            PluginStartupReportWriter reportWriter,
            IPluginSignatureVerifier? signatureVerifier = null)
        {
            _discovery = discovery;
            _admissionPolicy = admissionPolicy;
            _stateStore = stateStore;
            _reportWriter = reportWriter;
            _signatureVerifier = signatureVerifier;
        }

        /// <summary>执行一次启动扫描：刷新宿主状态并返回（同时落盘）启动报告。</summary>
        public PluginStartupReport Scan()
        {
            _stateStore.Load();
            PluginStateDocument state = _stateStore.Current;
            bool developerModeEnabled = state.DeveloperModeEnabled;

            var report = new PluginStartupReport
            {
                GeneratedAt = DateTimeOffset.Now,
                DeveloperModeEnabled = developerModeEnabled,
                InstallDirectory = _discovery.InstallDirectory,
                UserDirectory = _discovery.UserDirectory,
            };

            var scanned = _discovery.Discover()
                .Select(candidate => (Candidate: candidate, Parse: PluginManifestParser.Parse(candidate.ManifestJson)))
                .ToList();

            // 按 id 归并（清单不可用时回落目录名）：同 id 出现在两个来源目录即冲突。
            var groups = scanned
                .GroupBy(
                    item => string.IsNullOrWhiteSpace(item.Parse.Manifest?.Id)
                        ? item.Candidate.DirectoryName
                        : item.Parse.Manifest!.Id!,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                report.Plugins.Add(BuildEntry(group.Key, group.ToList(), developerModeEnabled, state));
            }

            report.AdmissionCounts = Enum.GetValues<PluginAdmission>()
                .ToDictionary(admission => admission, admission => report.Plugins.Count(entry => entry.Admission == admission));

            _stateStore.Save();
            _reportWriter.Write(report);
            return report;
        }

        /// <summary>
        /// 把同一 id 的候选包（正常一份；同 id 冲突两份）裁决成一条报告条目，并刷新宿主状态。
        /// </summary>
        private PluginStartupReportEntry BuildEntry(
            string pluginId,
            List<(PluginPackageCandidate Candidate, PluginManifestParseResult Parse)> packages,
            bool developerModeEnabled,
            PluginStateDocument state)
        {
            // 生效候选：安装目录优先。同 id 冲突时两侧都拒，此处只决定状态与报告里的主路径。
            (PluginPackageCandidate Candidate, PluginManifestParseResult Parse) primary = packages
                .OrderBy(package => package.Candidate.Origin == PluginPackageOrigin.Install ? 0 : 1)
                .First();

            bool hasConflict = packages.Count > 1;
            var violations = new List<string>();
            if (hasConflict)
            {
                violations.Add(
                    $"同 id 冲突：{string.Join(" 与 ", packages.Select(package => package.Candidate.DirectoryPath))}；两侧均不装载，请移除或改名其中一份");
            }

            foreach ((PluginPackageCandidate candidate, PluginManifestParseResult parse) in packages)
            {
                string prefix = hasConflict ? $"[{candidate.Origin} {candidate.DirectoryPath}] " : string.Empty;
                foreach (string violation in candidate.Violations)
                {
                    violations.Add(prefix + violation);
                }

                foreach (string error in parse.Errors)
                {
                    violations.Add(prefix + error);
                }

                foreach (string error in PluginManifestValidator.Validate(parse.Manifest, candidate.DirectoryPath))
                {
                    violations.Add(prefix + error);
                }
            }

            PluginManifest? manifest = primary.Parse.Manifest;
            PluginSignatureCheck? signature = violations.Count > 0
                ? null
                : VerifyEntrySignature(primary.Candidate, manifest);
            PluginAdmissionDecision decision = violations.Count > 0
                ? new PluginAdmissionDecision(PluginAdmission.Rejected, string.Join("；", violations))
                : _admissionPolicy.Decide(pluginId, manifest?.Version ?? string.Empty, developerModeEnabled, signature);

            PluginStateEntry stateEntry = state.GetOrCreate(pluginId);
            stateEntry.Version = string.IsNullOrWhiteSpace(manifest?.Version) ? null : manifest!.Version;
            stateEntry.PackagePath = primary.Candidate.DirectoryPath;
            stateEntry.Admission = decision.Status;
            stateEntry.AdmissionReason = decision.Reason;
            stateEntry.SignatureSubject = signature?.Subject;

            return new PluginStartupReportEntry
            {
                PluginId = pluginId,
                Name = manifest?.Name,
                Version = stateEntry.Version,
                HasUi = manifest?.Ui is not null,
                Admission = decision.Status,
                AdmissionReason = decision.Reason,
                SignatureSubject = signature?.Subject,
                Enabled = stateEntry.Enabled,
                Quarantine = stateEntry.Quarantine,
                PackagePaths = packages.Select(package => package.Candidate.DirectoryPath).ToList(),
                Violations = violations,
            };
        }

        /// <summary>
        /// 校验生效候选包的入口程序集签名（签名与清单属同一道闸）；
        /// 清单未声明入口程序集或文件缺席时不校验（返回 null，按不可信处理）。
        /// </summary>
        private PluginSignatureCheck? VerifyEntrySignature(
            PluginPackageCandidate candidate, PluginManifest? manifest)
        {
            if (_signatureVerifier is null || string.IsNullOrWhiteSpace(manifest?.EntryAssembly))
            {
                return null;
            }

            string entryPath = Path.Combine(candidate.DirectoryPath, manifest.EntryAssembly);
            return File.Exists(entryPath) ? _signatureVerifier.Verify(entryPath) : null;
        }
    }
}
