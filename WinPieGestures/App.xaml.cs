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
        private AppHost? _appHost;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 测试运行器显式指定时允许绕过单实例互斥
            string cmdLine = Environment.CommandLine;
            bool isTestMode = cmdLine.Contains("--allow-multiple", StringComparison.OrdinalIgnoreCase) ||
                              cmdLine.Contains("--test-instance", StringComparison.OrdinalIgnoreCase);

            if (!isTestMode)
            {
                bool isNewInstance;
                try
                {
                    _singleInstanceMutex = new Mutex(true, DevInstance.MutexName, out isNewInstance);
                }
                catch
                {
                    isNewInstance = true;
                }

                if (!isNewInstance)
                {
                    // 已有实例在运行：若设置窗口已打开则将其置前
                    try
                    {
                        IntPtr hWnd = FindWindow(null, "StarPie 设置控制台 (Preferences)" + DevInstance.Suffix);
                        if (hWnd != IntPtr.Zero)
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
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
                // 手动组合根 + 应用宿主：Composition 装配对象图（无 StartupUri），
                // AppHost 执行启动编排。
                _composition = new Composition();

                // 经注入的配置服务加载配置
                _composition.Config.Load();

                _appHost = _composition.CreateAppHost();
                _appHost.Run();

                // 启动后做一次内存整理
                MemoryOptimizer.TrimMemory(true);
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

            // 托盘、鼠标钩子与壳层 VM 的生命周期归 AppHost；DI 容器由组合根最后释放
            _appHost?.Dispose();
            _appHost = null;
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
