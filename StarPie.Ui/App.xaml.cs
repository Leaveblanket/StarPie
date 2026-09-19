using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace StarPie.Ui
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _singleInstanceMutex;
        // 首实例标记句柄：必须持有到进程结束（句柄一关，标记对象即销毁），与单实例互斥体同生共死。
        private static EventWaitHandle? _ownerMarker;
        // "提权未生效"事件句柄：同上——对象归首实例所有，提权新实例只是打开同一个对象并置位。
        private static EventWaitHandle? _elevationFailedEvent;
        private Composition? _composition;
        private ResidentShell? _residentShell;

        // 单实例闸门：全机命名互斥，dev 与正式实例同闸（不并行运行，后启动方按已有实例路径置前退出）。
        private const string SingleInstanceMutexName = @"Global\StarPie_SingleInstance_Mutex_9B8A7C";

        protected override void OnStartup(StartupEventArgs e)
        {
            // e2e/测试运行器经显式参数声明测试实例：绕过单实例闸门（用例冷启动需要并行多实例，
            // 且测试实例不持有全机互斥——否则 e2e 运行期间会挡住用户正常启动），
            // 并让常驻壳层受理测试实例退出消息（见 TestInstanceExit；正式实例不受理）。
            string cmdLine = Environment.CommandLine;
            bool testInstance = TestInstanceSwitches.IsTestInstance(cmdLine);

            if (!testInstance)
            {
                bool isNewInstance;
                Mutex? mutex = null;
                try
                {
                    mutex = new Mutex(true, SingleInstanceMutexName, out isNewInstance);
                }
                catch (UnauthorizedAccessException)
                {
                    // 互斥体已存在且由更高完整性级别（提权）实例持有：强制完整性标签使本次打开拿不到
                    // 写访问，"拿不到"本身即说明实例已存在——按已有实例走恢复消息路径。
                    // 若在此退回新实例，会得到两个托盘图标与两条全局鼠标钩子。
                    isNewInstance = false;
                }
                catch
                {
                    // 其它失败（互斥体被异常占用等）保守退回新实例，不让进程无声不启动。
                    isNewInstance = true;
                }

                if (isNewInstance)
                {
                    _singleInstanceMutex = mutex;
                }
                else
                {
                    // 处置决策是纯函数（真值表见 SingleInstanceGateTests），闸门只在这一处做该判定。
                    // 已有实例的形态与权限态由它发布的标记事件读得；读不到同形态标记（互斥体被另一
                    // 形态的实例持有，或对方尚未发布）即按置前退出走——不冒险，也不空等。
                    (bool sameInstanceKind, bool ownerElevated) = InstanceHandover.ProbeOwner();
                    SingleInstanceGateDecision decision = sameInstanceKind
                        ? SingleInstanceGate.Resolve(
                            newInstanceElevated: ProcessElevation.IsRunningAsAdministrator(),
                            existingInstanceElevated: ownerElevated)
                        : SingleInstanceGateDecision.ForegroundAndExit;

                    if (decision == SingleInstanceGateDecision.RequestHandover)
                    {
                        // 请求置位成功不等于让位成立：就绪判据只有"互斥体已可取得"这一个来源
                        // （见 InstanceHandover），等不到就让本次提权作废。
                        bool released = mutex != null
                                        && InstanceHandover.RequestYield()
                                        && InstanceHandover.WaitForSingleInstanceRelease(mutex, InstanceHandover.YieldTimeout);
                        decision = SingleInstanceGate.ApplyHandoverOutcome(decision, released);
                    }

                    switch (decision)
                    {
                        case SingleInstanceGateDecision.RequestHandover:
                            // 已接替：互斥体归本实例，按首实例继续启动（下同——发布标记、建托盘与钩子）。
                            _singleInstanceMutex = mutex;
                            break;

                        case SingleInstanceGateDecision.ExitAndNotifyElevationFailed:
                            // 本实例即将退出，无处呈现——把"这次提权没成"经握手信道留给首实例，
                            // 由它用既有气泡通道给用户一句明确的话（成功路径绝不置位这一信号）。
                            _ = InstanceHandover.NotifyElevationNotApplied();
                            mutex?.Dispose();
                            Shutdown(0);
                            return;

                        case SingleInstanceGateDecision.ForegroundAndExit:
                            // 已有实例在运行：向它的常驻消息窗口投递恢复消息——接收端驻常驻壳层，
                            // 设置台关着时也会创建设置台并显示（纯外部 ShowWindow 不更新 WPF 的
                            // IsVisible 状态，恢复序列不会触发，故必须经本消息驱动）。
                            // 按窗口名查找：托盘消息窗口是常驻 HWND（进程存活期内恒在），设置台是瞬态窗口。
                            try
                            {
                                HWND hWnd = PInvoke.FindWindow(null, TrayIconManager.WindowName);
                                if (!hWnd.IsNull)
                                {
                                    SingleInstanceRestore.Send(hWnd);
                                }
                            }
                            catch { }

                            mutex?.Dispose();
                            // 不初始化钩子/托盘，立即结束当前进程
                            Shutdown(0);
                            return;
                    }
                }

                // 首实例（含接管成功者）发布"我在此"的标记与"提权未生效"事件：后启动的实例由此读得
                // 既有实例的形态与权限态，接管没成时也有地方留话。两者与单实例互斥体同生共死——
                // 一同在 App.OnExit 释放。
                _ownerMarker = InstanceHandover.PublishOwnerMarker();
                _elevationFailedEvent = InstanceHandover.PublishElevationFailedEvent();
            }

            base.OnStartup(e);

            // 注册全局未处理异常处理，防止进程意外崩溃
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // 手动组合根 + 常驻壳层：Composition 装配对象图（无 StartupUri），
                // ResidentShell 执行启动编排。
                _composition = new Composition();

                // 经注入的配置服务加载配置
                _composition.Config.Load();

                _residentShell = _composition.CreateResidentShell(testInstance);
                _residentShell.Run();
                // 启动兜底内存整理（含 Debug 构建的堆硬顶生效值日志）在 ResidentShell 启动编排末尾执行（预热之后）
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

            // 托盘、鼠标钩子与设置台的生命周期归 ResidentShell；DI 容器由组合根最后释放
            _residentShell?.Dispose();
            _residentShell = null;
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

            // 标记与互斥体同生共死：释放互斥体之后再关标记句柄——声明"我不再是首实例"与"我让出了锁"
            // 的先后顺序对第二个实例无影响（它看标记决定要不要请求让位，看互斥体决定接管是否成立）。
            _ownerMarker?.Dispose();
            _ownerMarker = null;
            _elevationFailedEvent?.Dispose();
            _elevationFailedEvent = null;

            base.OnExit(e);
        }
    }
}
