using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using StarPie.Kernel.Configuration;

namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 开机自启的两种形态读写：HKCU Run 注册表项（普通权限自启）与 Windows 任务计划程序任务
    /// （<c>/rl highest</c> 提权自启——触发时由任务计划程序服务完成提权，**不弹 UAC**）。
    /// 两种形态互斥（见 <see cref="ResolvePlacement"/>）：同一时刻只落位一种——两条路径的触发
    /// 时机相同，同时落位会让两个实例抢单实例闸门。
    /// dev 实例绝不改写正式版自启项——dev 判定读 <see cref="AppDataPaths.IsDevInstance"/>（编译期定死），
    /// 且 dev 的提权自启任务用独立名字，绝不与正式版共用。
    /// 与 <c>MemoryOptimizer</c> 同属无状态系统调用静态工具，经委托由组合根接线进
    /// 通用分区 ViewModel（可测缝是 ViewModel 的注入委托，不是注册表/任务计划程序本身）。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class AutostartRegistry
    {
        /// <summary>提权自启的计划任务名；dev 实例带独立后缀，绝不与正式版共用。</summary>
        public static string AdminTaskName
            => AppDataPaths.IsDevInstance ? "StarPie_AdminAutoStart_Dev" : "StarPie_AdminAutoStart";

        /// <summary>注册表形态（普通权限）的开机自启是否已注册；StarPie 或 legacy WinPieGestures
        /// 任一存在即是。提权形态见 <see cref="IsAdminAutoStartEnabled"/>——两条路线互斥，
        /// 界面的总开关按两者取或。</summary>
        public static bool IsAutoStartEnabled()
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

        /// <summary>
        /// 提权自启的计划任务是否已注册（以任务计划程序的实况为准，不读配置）。
        /// 用户可在任务计划程序里手工删除该任务，故界面状态一律以此为准。
        /// </summary>
        public static bool IsAdminAutoStartEnabled()
        {
            // 只认退出码（0 = 任务存在），不解析输出：schtasks 的输出随系统语言变化。
            // 查询不需要提权，故不弹 UAC。
            return RunSchtasks(BuildAdminTaskQueryArguments(AdminTaskName), elevate: false) == 0;
        }

        /// <summary>
        /// 自启落位的形态决策（纯函数）：两条权限路线互斥，同一时刻只落位一种自启形态。
        /// 提权形态下注册表 Run 键必须**缺位**——两条自启路径的触发时机相同（都在登录时），
        /// 同时落位会让非提权实例与提权实例抢单实例互斥体，谁先抢到谁活；非提权那个赢了就等于
        /// 「以管理员身份开机自启」白开，而界面读计划任务实况仍会显示已开启。
        /// </summary>
        /// <param name="enable">总开关「开机自启」。</param>
        /// <param name="asAdmin">「以管理员身份开机自启」（蕴含总开关）。</param>
        public static (bool WriteRunKey, bool WantAdminTask) ResolvePlacement(bool enable, bool asAdmin)
            => (WriteRunKey: enable && !asAdmin, WantAdminTask: enable && asAdmin);

        /// <summary>
        /// 自启形态落位：按 <see cref="ResolvePlacement"/> 的决策写入——两条路线互斥，
        /// 提权形态下注册表 Run 键被删除（不留兜底），普通形态下计划任务被删除。
        /// </summary>
        /// <returns>提权形态是否按请求落位（注册表读写失败不影响该返回值）。</returns>
        public static bool ApplyAutoStart(bool enable, bool asAdmin)
        {
            (bool writeRunKey, bool wantAdminTask) = ResolvePlacement(enable, asAdmin);
            SetRegistryAutoStart(writeRunKey);
            return wantAdminTask ? EnableAdminTask() : DisableAdminTask();
        }

        /// <summary>建/更新提权自启任务的 schtasks 参数（纯字符串构造，供单测锁定形状）。</summary>
        /// <remarks>
        /// <c>/rl highest</c> 让任务以最高权限运行——提权由任务计划程序服务在触发时完成，不弹 UAC；
        /// <c>/sc onlogon</c> 登录即起；<c>/delay 0000:00</c> 消掉任务计划程序默认的登录延迟；
        /// <c>/f</c> 覆盖同名任务（可执行文件搬家/升级后刷新路径）。
        /// 任务命令行不带额外参数：与注册表自启形态完全一致，只差权限级别。
        /// </remarks>
        public static string BuildAdminTaskCreateArguments(string exePath, string taskName)
            => $"/create /tn \"{taskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /delay 0000:00 /rl highest /f";

        /// <summary>删除提权自启任务的 schtasks 参数。</summary>
        public static string BuildAdminTaskDeleteArguments(string taskName)
            => $"/delete /tn \"{taskName}\" /f";

        /// <summary>查询提权自启任务的 schtasks 参数（退出码 0 = 存在）。</summary>
        public static string BuildAdminTaskQueryArguments(string taskName)
            => $"/query /tn \"{taskName}\"";

        /// <summary>按需触发提权自启任务的 schtasks 参数（纯字符串构造，供单测锁定形状）。</summary>
        /// <remarks>
        /// <c>/run</c> 无 run level 选项：权限级别是任务自身的属性（<c>/rl highest</c>），触发方只请求运行
        /// ——这正是"即时提权"复用的东西。带 <c>/tn</c> 指名任务，不带 <c>/i</c>（交互式会话已由任务本身指定）。
        /// </remarks>
        public static string BuildAdminTaskRunArguments(string taskName)
            => $"/run /tn \"{taskName}\"";

        /// <summary>
        /// 按需触发提权自启任务——路线 B 的**即时形态**：非提权进程触发它，任务计划程序服务拉起一个
        /// High 完整性级别、同一交互会话的实例，**全程不弹 UAC**（实测见 #167）。触发本身不需要提权
        /// （建/删任务才需要），故这里不经 <c>runas</c>。
        /// </summary>
        /// <remarks>
        /// 返回值**只表示"任务被受理"**，不表示提权实例已就绪——实测动作为空的任务被 <c>/run</c> 时
        /// 同样返回成功，而进程根本没启动。就绪判据只认"单实例互斥体已可取得"
        /// （见 <see cref="InstanceHandover.WaitForSingleInstanceRelease"/>）。
        /// </remarks>
        public static bool RunAdminTask()
            => RunSchtasks(BuildAdminTaskRunArguments(AdminTaskName), elevate: false) == 0;

        /// <summary>
        /// 「立即以管理员身份重启」入口的可见性与可点性（纯决策，托盘与设置页两处同源）。
        /// </summary>
        /// <remarks>
        /// 入口就是提权自启那颗任务的即时触发，故两条口径都从它直接推出：提权态下入口不出现
        /// （对提权实例没有意义）；任务不存在时不可点——没有可复用的任务就没有这条路，
        /// 即时提权不是第三条权限路线，也不做"临时提权、用完删任务"的第三种形态。
        /// </remarks>
        /// <param name="elevated">当前实例是否以管理员身份运行。</param>
        /// <param name="adminTaskExists">提权自启的计划任务是否已注册。</param>
        public static (bool Visible, bool Enabled) ResolveAdminRestartEntry(bool elevated, bool adminTaskExists)
            => (Visible: !elevated, Enabled: adminTaskExists);

        /// <summary>注册表形态的落位；失败静默（Debug 输出），不抛出。</summary>
        private static void SetRegistryAutoStart(bool enable)
        {
            // dev 实例不得把正式自启项指向 dev 可执行文件
            if (AppDataPaths.IsDevInstance) return;

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    key.SetValue("StarPie", $"\"{CurrentExecutablePath()}\"");
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
                Debug.WriteLine($"Failed to set autostart: {ex.Message}");
            }
        }

        private static bool EnableAdminTask()
            => RunSchtasks(
                BuildAdminTaskCreateArguments(CurrentExecutablePath(), AdminTaskName),
                elevate: true) == 0;

        private static bool DisableAdminTask()
        {
            // 无任务可删时直接成功：不为一次注定的空操作白弹一次 UAC。
            if (!IsAdminAutoStartEnabled()) return true;
            return RunSchtasks(BuildAdminTaskDeleteArguments(AdminTaskName), elevate: true) == 0;
        }

        /// <summary>
        /// 跑一次 schtasks 并返回退出码；启动失败或超时返回 -1。
        /// <paramref name="elevate"/> 为真且当前未提权时经 <c>runas</c> 触发一次 UAC
        /// ——建/删 <c>/rl highest</c> 的任务本身需要管理员；用户取消会以
        /// <c>Win32Exception(1223 ERROR_CANCELLED)</c> 抛出，此处按失败返回。
        /// </summary>
        private static int RunSchtasks(string arguments, bool elevate)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = SchtasksPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                };

                if (elevate && !ProcessElevation.IsRunningAsAdministrator())
                {
                    startInfo.UseShellExecute = true;
                    startInfo.Verb = "runas";
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }
                else
                {
                    // 已提权（或本就无需提权）：直接跑，不经 UAC 一跳。
                    startInfo.UseShellExecute = false;
                }

                using Process? process = Process.Start(startInfo);
                if (process == null) return -1;
                if (!process.WaitForExit(SchtasksTimeoutMs))
                {
                    try { process.Kill(); } catch { }
                    return -1;
                }

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"[Autostart] schtasks 退出码 {process.ExitCode}: {arguments}");
                }

                return process.ExitCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Autostart] schtasks 调用失败: {ex.Message}");
                return -1;
            }
        }

        private static string CurrentExecutablePath()
            => Environment.ProcessPath
               ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");

        /// <summary>schtasks 全路径：不依赖 PATH，避免工作目录被劫持。</summary>
        private static string SchtasksPath => Path.Combine(Environment.SystemDirectory, "schtasks.exe");

        /// <summary>任务创建可能弹 UAC 等用户操作，超时给宽；查询是本地调用，给窄。</summary>
        private const int SchtasksTimeoutMs = 60_000;
    }
}
