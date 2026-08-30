using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace WinPieGestures
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _singleInstanceMutex;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public static MouseHook? MainMouseHook { get; private set; }
        private GestureController? _gestureController;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Allow bypassing mutex for automated test runners if explicitly specified
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
                    // Existing instance is running, try to bring settings window to front if open
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

                    // Terminate current process immediately without initializing hooks or tray
                    Shutdown(0);
                    return;
                }
            }

            base.OnStartup(e);

            // Register global unhandled exception handlers to prevent unexpected process crashes
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // Initialize configuration
                ConfigManager.LoadConfig();

                // Initialize mouse hook
                MainMouseHook = new MouseHook();
                MainMouseHook.Start();

                // Initialize gesture controller
                _gestureController = new GestureController(MainMouseHook);

                // Initial memory optimization after startup
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
            e.Handled = true; // Mark as handled to prevent app crash
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.Error.WriteLine($"[App Domain Exception]: {e.ExceptionObject}");
            Debug.WriteLine($"[App Domain Exception]: {e.ExceptionObject}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Auto-persist latest configuration on application exit
            try
            {
                ConfigManager.SaveConfig();
            }
            catch { }

            // Unregister mouse hook on exit
            MainMouseHook?.Stop();

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
