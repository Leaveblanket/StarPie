using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;
using SharpHook.Simulation;
using StarPie.Host.WheelInteraction;
using StarPie.Sdk.Models;

namespace StarPie.Ui.Services.Input
{
    /// <summary>
    /// 输入栈的捕获侧：全局鼠标钩子把触发键的按下 / 移动 / 抬起喂入 <see cref="WheelInteractionEngine"/>，
    /// 按引擎决策同步抑制事件，并把引擎交付的副作用（补发点击 / 执行动作）卸载到 UI 线程。
    /// 轮盘交互决策全部在引擎内，本类只做事件捕获、抑制与副作用投放。
    /// </summary>
    /// <remarks>
    /// 线程模型：钩子独占专用线程（<c>RunAsync(..., useBackgroundThread: true)</c>），
    /// 线程上只做「喂坐标 + 拿抑制结论」——抑制必须与钩子同线程同步设置（SharpHook 只有
    /// <c>SimpleGlobalHook</c> 满足），其余工作一律卸载到 UI 线程：低级钩子回调超时会被
    /// 系统静默移除，不能把窗口创建与渲染留在钩子路径上。
    /// 自注入识别走回放窗口（<see cref="ReplayWindow"/>），不按模拟标记整体过滤——
    /// 外部注入（e2e 的 pywin32 注入）必须仍能触发轮盘交互。
    /// 看门狗（<see cref="HookWatchdog"/>）周期探测静默失效，并就地在新的专用线程上重注册。
    /// </remarks>
    public sealed class MouseInputHook : IDisposable
    {
        /// <summary>模拟器注册名（Linux 虚拟设备命名用；Windows 后端不使用）。</summary>
        private const string SimulatorApplicationName = "StarPie";

        /// <summary>一次运行的收尾等待上限：Stop 是异步生效（钩子线程收到退出请求才收尾）。</summary>
        private static readonly TimeSpan RunStopTimeout = TimeSpan.FromSeconds(2);

        // 钩子线程读、UI 线程写（托盘暂停/恢复），须 volatile 保可见性。
        private volatile bool _isPaused;

        // 生命周期串行化：UI 线程（常驻壳层启停）与看门狗线程（就地重注册）可能并发相遇。
        private readonly object _lifecycleLock = new();

        private readonly IGlobalHook _hook;
        private readonly WheelInteractionEngine _engine;
        private readonly IActionExecutorService _actionExecutor;
        private readonly Action<Action> _postToUiThread;
        private readonly MouseButton _triggerButton;
        private readonly Func<IEventSimulator> _simulatorFactory;
        private readonly ReplayWindow _replayWindow = new();
        private readonly HookWatchdog _watchdog;

        private IEventSimulator? _simulator;
        private Task? _runTask;
        private bool _isRunning;

        /// <param name="hook">全局钩子实现（生产为 <c>SimpleGlobalHook</c>，测试为 TestGlobalHook）。</param>
        /// <param name="engine">轮盘交互引擎（纯决策）。</param>
        /// <param name="actionExecutor">动作执行器（副作用落地）。</param>
        /// <param name="postToUiThread">调度接缝：副作用回 UI 线程执行（本类不引 UI 框架类型）。</param>
        /// <param name="triggerButton">触发键（默认右键；栈内参数化，不暴露配置面）。</param>
        /// <param name="simulatorFactory">注入模拟器工厂（默认创建 SharpHook 模拟器；交出所有权，由本类释放）。</param>
        /// <param name="cursorProbe">看门狗的光标探针（默认读系统光标位置）。</param>
        /// <param name="watchdogPeriod">看门狗探针周期（默认 3 秒，ADR-0052 口径）。</param>
        public MouseInputHook(
            IGlobalHook hook,
            WheelInteractionEngine engine,
            IActionExecutorService actionExecutor,
            Action<Action> postToUiThread,
            MouseButton triggerButton = MouseButton.Button2,
            Func<IEventSimulator>? simulatorFactory = null,
            Func<ScreenPoint?>? cursorProbe = null,
            TimeSpan? watchdogPeriod = null)
        {
            ArgumentNullException.ThrowIfNull(hook);
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(actionExecutor);
            ArgumentNullException.ThrowIfNull(postToUiThread);

            _hook = hook;
            _engine = engine;
            _actionExecutor = actionExecutor;
            _postToUiThread = postToUiThread;
            _triggerButton = triggerButton;
            _simulatorFactory = simulatorFactory ?? (() => EventSimulator.Create(SimulatorApplicationName));
            _watchdog = new HookWatchdog(
                watchdogPeriod ?? HookWatchdog.DefaultPeriod,
                cursorProbe ?? SystemCursor.TryGetPosition,
                Restart);

            _hook.MousePressed += OnMousePressed;
            _hook.MouseReleased += OnMouseReleased;
            _hook.MouseMoved += OnMouseMoved; //为看门狗计数，触发键按住期间，移动在钩子层是 MouseDragged 而非 MouseMoved（掩码含按键）。
            _hook.MouseDragged += OnMouseMoved;  // 触发键按住期间，移动在钩子层是 MouseDragged 而非 MouseMoved（掩码含按键）。
        }

        /// <summary>暂停开关：暂停只是放行全部事件，钩子仍挂着（托盘"暂停/恢复"）。</summary>
        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        /// <summary>启动捕获：钩子独占专用线程跑起来，看门狗随之起探；重复调用无副作用。</summary>
        public void Start()
        {
            lock (_lifecycleLock)
            {
                if (_isRunning) return;

                _isRunning = true;
                try
                {
                    _runTask = _hook.RunAsync(GlobalHookType.Mouse, useBackgroundThread: true);
                }
                catch
                {
                    _isRunning = false;
                    throw;
                }

                // 运行期故障（如钩子安装失败）落在任务上而不是启动调用里：记调试日志，
                // 别让它成为无人观察的异常；重注册交给看门狗。
                _ = _runTask.ContinueWith(
                    static task => Debug.WriteLine($"Mouse hook run failed: {task.Exception?.GetBaseException().Message}"),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                _watchdog.Start();
            }
        }

        /// <summary>停止捕获：先停探针，再请求钩子退出并等本次运行收尾（重注册前必须等干净）。</summary>
        public void Stop()
        {
            lock (_lifecycleLock)
            {
                if (!_isRunning) return;

                _isRunning = false;
                _watchdog.Stop();
                _hook.Stop();

                Task? run = _runTask;
                _runTask = null;
                if (run is null) return;

                try
                {
                    if (!run.Wait(RunStopTimeout))
                    {
                        // 收尾没等到：钩子线程卡住（比如回放注入被目标会话拖住）。不阻塞退出，
                        // 但留一条可诊断的痕迹。
                        Debug.WriteLine("Mouse hook did not finish stopping within the timeout.");
                    }
                }
                catch (AggregateException)
                {
                    // 运行期故障已由续接记录，这里只等收尾。
                }
            }
        }

        /// <summary>看门狗的就地重注册：停旧运行（等收尾）再起新运行；失败只记调试日志（与旧实现同口径）。</summary>
        public void Restart()
        {
            try
            {
                Stop();
                Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to re-register mouse hook: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _simulator?.Dispose();
            _simulator = null;
        }

        private void OnMousePressed(object? sender, MouseHookEventArgs e)
        {
            _watchdog.CountEvent();

            if (_isPaused) return;
            if (e.Data.Button != _triggerButton) return;
            if (_replayWindow.TryConsume(e.IsEventSimulated)) return;

            // 引擎决定按下是否被轮盘交互接管（接管即抑制；未成轮盘交互时松手补发点击）。
            if (_engine.OnTriggerDown(new(e.Data.X, e.Data.Y)))
            {
                e.SuppressEvent = true;
            }
        }

        private void OnMouseReleased(object? sender, MouseHookEventArgs e)
        {
            _watchdog.CountEvent();

            if (_isPaused) return;
            if (e.Data.Button != _triggerButton) return;
            if (_replayWindow.TryConsume(e.IsEventSimulated)) return;

            WheelInteractionReleaseResult result = _engine.OnTriggerUp(new(e.Data.X, e.Data.Y));
            if (!result.Handled) return;

            if (result.ShouldReplayClick)
            {
                // 补发不在钩子回调栈内做：注入的事件要等本次回调返回后才回到捕获侧。
                _postToUiThread(ReplayTriggerClick);
            }
            else if (result.ActionToExecute is { } action)
            {
                _postToUiThread(() => _actionExecutor.Execute(action));
            }

            e.SuppressEvent = true;
        }

        private void OnMouseMoved(object? sender, MouseHookEventArgs e)
        {
            _watchdog.CountEvent();

            if (_isPaused) return;

            _engine.OnTriggerMove(new(e.Data.X, e.Data.Y));
        }

        /// <summary>补发一次完整的触发键点击，并先开回放窗口——注入事件回到捕获侧时被放行。</summary>
        private void ReplayTriggerClick()
        {
            // 必须是 fire-and-forget 投放里唯一不许抛异常的路径,因为这里是 UI 线程，抛了就挂了。
            // 模拟器创建或注入失败只记调试日志，不能让 UI 线程上的这条续接把进程带走。
            try
            {
                _replayWindow.Open();

                IEventSimulator simulator = _simulator ??= _simulatorFactory();
                // 位置无关：在系统当前光标处补发。
                UioHookResult press = simulator.SimulateMousePress(_triggerButton);
                UioHookResult release = simulator.SimulateMouseRelease(_triggerButton);
                if (press != UioHookResult.Success || release != UioHookResult.Success)
                {
                    Debug.WriteLine($"Replay trigger click failed: press={press}, release={release}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Replay trigger click failed: {ex.Message}");
            }
        }
    }
}
