using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace StarPie
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _singleInstanceMutex;
        private Composition? _composition;
        private ShellHost? _shellHost;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        // 单实例闸门：全机命名互斥，dev 与正式实例同闸（不并行运行，后启动方按已有实例路径置前退出）。
        private const string SingleInstanceMutexName = @"Global\StarPie_SingleInstance_Mutex_9B8A7C";

        protected override void OnStartup(StartupEventArgs e)
        {
            // e2e/测试运行器经显式参数绕过单实例闸门：用例冷启动需要并行多实例，
            // 且测试实例不持有全机互斥——否则 e2e 运行期间会挡住用户正常启动。
            string cmdLine = Environment.CommandLine;
            bool allowMultipleInstances = cmdLine.Contains("--allow-multiple", StringComparison.OrdinalIgnoreCase) ||
                                          cmdLine.Contains("--test-instance", StringComparison.OrdinalIgnoreCase);

            if (!allowMultipleInstances)
            {
                bool isNewInstance;
                try
                {
                    _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out isNewInstance);
                }
                catch
                {
                    isNewInstance = true;
                }

                if (!isNewInstance)
                {
                    // 已有实例在运行：向它的常驻消息窗口投递恢复消息——接收端驻常驻壳层，
                    // 设置台关着时也会创建设置台并显示（纯外部 ShowWindow 不更新 WPF 的
                    // IsVisible 状态，恢复序列不会触发，故必须经本消息驱动）。
                    // 按窗口名查找：托盘消息窗口是常驻 HWND（进程存活期内恒在），设置台是瞬态窗口。
                    try
                    {
                        IntPtr hWnd = FindWindow(null, TrayIconManager.WindowName);
                        if (hWnd != IntPtr.Zero)
                        {
                            SingleInstanceRestore.Send(hWnd);
                        }
                    }
                    catch { }

                    // 不初始化钩子/托盘，立即结束当前进程
                    Shutdown(0);
                    return;
                }
            }

            base.OnStartup(e);

            // 注册全局未处理异常处理，防止进程意外崩溃
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // 手动组合根 + 常驻壳层：Composition 装配对象图（无 StartupUri），
                // ShellHost 执行启动编排。
                _composition = new Composition();

                // 经注入的配置服务加载配置
                _composition.Config.Load();

                // 静默形态（--background）：窗口屏内左上角、不可激活、点击穿透、不进任务栏，托盘保留，
                // 全局鼠标钩子不启动——e2e 在用户同机工作时无打扰驱动（见 docs/adr/0032）。
                bool isBackground = cmdLine.Contains("--background", StringComparison.OrdinalIgnoreCase);
                _shellHost = _composition.CreateShellHost(isBackground);
                _shellHost.Run();
                // 启动兜底内存整理（含 Debug 构建的堆硬顶生效值日志）在 ShellHost 启动编排末尾执行（预热之后，#150）
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化 StarPie 失败:\n{ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Console.Error.WriteLine($"[App Dispatcher Exception]: {e.Exception}");
            Debug.WriteLine($"[App Dispatcher Exception]: {e.Exception}");
            e.Handled = true; // 标记已处理，避免应用崩溃
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.Error.WriteLine($"[App Domain Exception]: {e.ExceptionObject}");
            Debug.WriteLine($"[App Domain Exception]: {e.ExceptionObject}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 退出时自动持久化最新配置
            try
            {
                _composition?.Config.Save();
            }
            catch { }

            // 托盘、鼠标钩子与设置台的生命周期归 ShellHost；DI 容器由组合根最后释放
            _shellHost?.Dispose();
            _shellHost = null;
            _composition?.Dispose();
            _composition = null;

            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }
}
