using System;
using System.IO;

namespace WinPieGestures.Services.Configuration
{
    /// <summary>
    /// 应用数据目录解析 (T16 自静态配置门面收编，ADR-0002)：dev 实例沙箱隔离与
    /// legacy 目录迁移，供组合根构造 <see cref="JsonConfigService"/> 与共享图标资产（S1）
    /// <see cref="IconAssets"/> 自定义图标目录使用。与 DevInstance 同类的环境派生静态工具：
    /// 无运行态状态、分支仅取决于
    /// 环境变量与启动参数，无测试缝需要 mock。
    /// </summary>
    internal static class AppDataPaths
    {
        /// <summary>
        /// 返回应用数据目录：dev 实例（--dev）隔离进 StarPie-Dev 子目录（首次从正式版配置
        /// 播种），正式版使用 StarPie 目录并自动从 legacy WinPieGestures 目录迁移配置。
        /// </summary>
        internal static string GetAppDataFolder()
        {
            string baseFolder = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOCALAPPDATA"))
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetEnvironmentVariable("LOCALAPPDATA")!;

            if (DevInstance.IsActive)
            {
                // Dev instances sandbox into their own folder so the installed release's
                // config is never touched; seed it once from the real config if present.
                string devFolder = Path.Combine(baseFolder, DevInstance.FolderName);
                try
                {
                    string devConfig = Path.Combine(devFolder, "config.json");
                    string releaseConfig = Path.Combine(baseFolder, "StarPie", "config.json");
                    if (!File.Exists(devConfig) && File.Exists(releaseConfig))
                    {
                        Directory.CreateDirectory(devFolder);
                        File.Copy(releaseConfig, devConfig);
                    }
                }
                catch { }
                return devFolder;
            }

            string starPieFolder = Path.Combine(baseFolder, DevInstance.FolderName);
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
