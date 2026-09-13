using System;
using System.IO;

namespace StarPie.Kernel.Configuration
{
    /// <summary>
    /// 解析应用数据目录：正式实例使用 StarPie，dev 实例使用 StarPie-Dev。
    /// </summary>
    /// <remarks>
    /// dev/正式分支由 <see cref="IsDevInstance"/> 决定；该标记由进程环境变量在类型初始化时
    /// 一次性求值，应用代码只读不写。目录供 <see cref="JsonConfigService"/>（config.json）
    /// 与图标资产服务（自定义图标目录）使用。
    /// </remarks>
    public static class AppDataPaths
    {
        private const string ReleaseFolderName = "StarPie";
        private const string DevFolderName = "StarPie-Dev";

        /// <summary>dev 实例环境变量名：launchSettings 的 StarPie Dev profile 注入值 "dev"。</summary>
        public const string DevEnvVariable = "STARPIE_INSTANCE";

        /// <summary>dev 实例标记：为 true 时目录分支指向 StarPie-Dev 沙箱。</summary>
        /// <remarks>类型初始化时读环境变量一次性求值并缓存；宿主入口求值后即把变量
        /// 移出进程环境（见 App.OnStartup），动作执行器启动的子进程不再继承。</remarks>
        public static bool IsDevInstance { get; } = string.Equals(
            Environment.GetEnvironmentVariable(DevEnvVariable), "dev", StringComparison.OrdinalIgnoreCase);

        /// <summary>应用数据目录名（dev 沙箱 StarPie-Dev / 正式 StarPie）——目录名的单一来源。</summary>
        public static string FolderName => IsDevInstance ? DevFolderName : ReleaseFolderName;

        /// <summary>
        /// 返回应用数据目录：dev 实例隔离进 StarPie-Dev 子目录（首次从正式版配置
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
