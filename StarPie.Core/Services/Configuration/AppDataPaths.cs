using System;
using System.IO;

namespace StarPie.Services.Configuration
{
    /// <summary>
    /// 应用数据目录解析 (T16 自静态配置门面收编，ADR-0002)：dev 实例沙箱隔离与
    /// legacy 目录迁移，供组合根构造 <see cref="JsonConfigService"/> 与共享图标资产（S1）
    /// <see cref="IconAssets"/> 自定义图标目录使用。B2/#75 Core 抽取后随 S2 迁入共享内核：
    /// dev 分支依赖的 H1 <c>DevInstance</c> 不能反向引用，故改为宿主组合根在装配前回填
    /// <see cref="IsDevInstance"/>（见 <c>Composition</c>）；本类除该进程级回填标记外无运行态状态，
    /// 分支仅取决于环境变量与回填标记，无测试缝需要 mock。
    /// </summary>
    public static class AppDataPaths
    {
        private const string ReleaseFolderName = "StarPie";
        private const string DevFolderName = "StarPie-Dev";

        /// <summary>
        /// dev 实例标记（跨程序集回填缝，B2/#75）：宿主组合根以
        /// <c>DevInstance.IsActive</c> 回填；此后目录分支与 <see cref="FolderName"/> 一致。
        /// </summary>
        public static bool IsDevInstance { get; set; }

        /// <summary>应用数据目录名（dev 沙箱 StarPie-Dev / 正式 StarPie）——目录名的单一来源。</summary>
        public static string FolderName => IsDevInstance ? DevFolderName : ReleaseFolderName;

        /// <summary>
        /// 返回应用数据目录：dev 实例（--dev）隔离进 StarPie-Dev 子目录（首次从正式版配置
        /// 播种），正式版使用 StarPie 目录并自动从 legacy WinPieGestures 目录迁移配置。
        /// </summary>
        public static string GetAppDataFolder()
        {
            string baseFolder = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOCALAPPDATA"))
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetEnvironmentVariable("LOCALAPPDATA")!;

            if (IsDevInstance)
            {
                // Dev instances sandbox into their own folder so the installed release's
                // config is never touched; seed it once from the real config if present.
                string devFolder = Path.Combine(baseFolder, FolderName);
                try
                {
                    string devConfig = Path.Combine(devFolder, "config.json");
                    string releaseConfig = Path.Combine(baseFolder, ReleaseFolderName, "config.json");
                    if (!File.Exists(devConfig) && File.Exists(releaseConfig))
                    {
                        Directory.CreateDirectory(devFolder);
                        File.Copy(releaseConfig, devConfig);
                    }
                }
                catch { }
                return devFolder;
            }

            string starPieFolder = Path.Combine(baseFolder, FolderName);
            string legacyFolder = Path.Combine(baseFolder, "WinPieGestures");

            // Auto migrate from legacy folder if needed
            if (!Directory.Exists(starPieFolder) && Directory.Exists(legacyFolder))
            {
                try
                {
                    Directory.CreateDirectory(starPieFolder);
                    string legacyConfig = Path.Combine(legacyFolder, "config.json");
                    string starPieConfig = Path.Combine(starPieFolder, "config.json");
                    if (File.Exists(legacyConfig) && !File.Exists(starPieConfig))
                    {
                        File.Copy(legacyConfig, starPieConfig);
                    }
                }
                catch { }
            }
            return starPieFolder;
        }
    }
}
