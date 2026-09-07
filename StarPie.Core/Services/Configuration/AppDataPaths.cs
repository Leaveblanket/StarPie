using System;
using System.IO;

namespace StarPie.Services.Configuration
{
    /// <summary>
    /// 解析应用数据目录：正式实例使用 StarPie，dev 实例使用 StarPie-Dev。
    /// </summary>
    /// <remarks>
    /// dev/正式分支由 <see cref="IsDevInstance"/> 决定；该标记由宿主组合根在装配前回填，
    /// 应用代码只读不写。目录供 <see cref="JsonConfigService"/>（config.json）与
    /// <see cref="IconAssets"/>（自定义图标目录）使用。
    /// </remarks>
    public static class AppDataPaths
    {
        private const string ReleaseFolderName = "StarPie";
        private const string DevFolderName = "StarPie-Dev";

        /// <summary>dev 实例标记：为 true 时目录分支指向 StarPie-Dev 沙箱。</summary>
        /// <remarks>由宿主组合根在装配前按 dev 启动参数回填；应用代码只读不写。</remarks>
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
                // Dev 实例沙箱隔离到独立目录，保证绝不触碰正式版配置；
                // 首次运行时若正式版配置存在，则播种一份到沙箱目录。
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

            // 需要时自动从旧版 WinPieGestures 目录迁移（仅当新目录不存在且旧目录存在）。
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
