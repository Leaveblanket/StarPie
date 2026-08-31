using System;

namespace WinPieGestures.Services
{
    /// <summary>
    /// 开机自启注册表读写 (T16 自静态配置门面收编，ADR-0002)：HKCU Run 键的
    /// StarPie 值维护（含 legacy WinPieGestures 键清理）。dev 实例绝不改写正式版自启项。
    /// 与 MemoryOptimizer 同类的无状态系统调用静态工具，经委托由组合根接线进
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

        /// <summary>注册/注销开机自启；失败静默（Debug 输出），与迁移前语义一致。</summary>
        internal static void SetAutoStart(bool enable)
        {
            // Dev instances must not repoint the real autostart entry at the dev executable
            if (DevInstance.IsActive) return;

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");
                    key.SetValue("StarPie", $"\"{exePath}\"");
                    // Clean up legacy key if present
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
