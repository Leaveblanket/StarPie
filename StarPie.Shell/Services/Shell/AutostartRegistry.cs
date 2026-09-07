using System;
using StarPie.Services.Configuration;

namespace StarPie.Services.Shell
{
    /// <summary>
    /// 开机自启注册表读写：维护 HKCU Run 键的 StarPie 值（含旧 WinPieGestures 键清理）。
    /// dev 实例绝不改写正式版自启项——dev 判定读 <see cref="AppDataPaths.IsDevInstance"/>，
    /// 该标记由组合根在装配前以 DevInstance.IsActive 回填。
    /// 与 <c>MemoryOptimizer</c> 同属无状态系统调用静态工具，经委托由组合根接线进
    /// 通用分区 ViewModel（可测缝是 ViewModel 的注入委托，不是注册表本身）。
    /// </summary>
    internal static class AutostartRegistry
    {
        /// <summary>当前是否已注册开机自启（StarPie 或 legacy WinPieGestures 任一存在即是）。</summary>
        internal static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("StarPie") != null || key?.GetValue("WinPieGestures") != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>注册/注销开机自启；失败静默（Debug 输出），不抛出。</summary>
        internal static void SetAutoStart(bool enable)
        {
            // dev 实例不得把正式自启项指向 dev 可执行文件
            if (AppDataPaths.IsDevInstance) return;

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");
                    key.SetValue("StarPie", $"\"{exePath}\"");
                    // 若存在旧键则清理
                    try { key.DeleteValue("WinPieGestures", false); } catch { }
                }
                else
                {
                    key.DeleteValue("StarPie", false);
                    try { key.DeleteValue("WinPieGestures", false); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set autostart: {ex.Message}");
            }
        }
    }
}
