using System.ComponentModel;
using StarPie.Abstractions.Ui;

namespace StarPie.Plugin.SampleUi
{
    /// <summary>
    /// 宿主签发定时器的回调目标：Tick 计数，展示"回调不落在已释放资源上"——
    /// 宿主停止定时器并摘除句柄后，本对象随页面 VM 一起可回收。
    /// </summary>
    public sealed class SampleUiHeartbeat : INotifyPropertyChanged
    {
        private int _ticks;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>定时器已触发的次数（绑定面：页面视图实时显示）。</summary>
        public int Ticks
        {
            get => _ticks;
            private set
            {
                if (_ticks == value)
                {
                    return;
                }

                _ticks = value;
                Raise(nameof(Ticks));
            }
        }

        /// <summary>定时器回调入口：推进计数并通知绑定。</summary>
        public void Tick() => Ticks++;

        private void Raise(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 插件页 VM：插件不得引用宿主 MVVM 框架基类（导航契约只要求
    /// <see cref="INotifyPropertyChanged"/>），这里直接手写通知面。
    /// </summary>
    public sealed class SampleUiPageViewModel : INotifyPropertyChanged
    {
        private string _message = "来自 UI 示例插件";

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>构造页面 VM（宿主在 UI 线程调用工厂创建；工厂可重复调用，无注册副作用）。</summary>
        /// <param name="context">插件 UI 上下文（开窗经宿主契约，不自行创建窗口）。</param>
        /// <param name="heartbeat">宿主签发定时器的心跳源。</param>
        public SampleUiPageViewModel(IPluginUiContext context, SampleUiHeartbeat heartbeat)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Heartbeat = heartbeat ?? throw new ArgumentNullException(nameof(heartbeat));
            OpenWindowCommand = new SampleUiRelayCommand(() => Context.ShowWindow(SampleUiUiModule.WindowKey));
            SampleUiProbes.Track("page-vm", this);
            SampleUiProbes.Track("page-open-window-command", OpenWindowCommand);
        }

        private IPluginUiContext Context { get; }

        /// <summary>宿主签发定时器的心跳源（视图绑定 Ticks 实时显示）。</summary>
        public SampleUiHeartbeat Heartbeat { get; }

        /// <summary>示例文本（视图双向绑定：编辑框回写、回显行实时更新）。</summary>
        public string Message
        {
            get => _message;
            set
            {
                if (_message == value)
                {
                    return;
                }

                _message = value;
                Raise(nameof(Message));
            }
        }

        /// <summary>经宿主契约打开本插件注册的窗口。</summary>
        public System.Windows.Input.ICommand OpenWindowCommand { get; }

        private void Raise(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 插件设置区块 VM：设置区由宿主页面按区块呈现，视图经插件资源字典里的
    /// DataTemplate 按 VM 类型映射。
    /// </summary>
    public sealed class SampleUiSettingsViewModel : INotifyPropertyChanged
    {
        private string _greeting = "UI 示例设置区块";

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>示例设置文案（区块视图绑定）。</summary>
        public string Greeting
        {
            get => _greeting;
            set
            {
                if (_greeting == value)
                {
                    return;
                }

                _greeting = value;
                Raise(nameof(Greeting));
            }
        }

        /// <summary>构造即自登记为泄漏探针（settings-vm），供卸载矩阵断言回收。</summary>
        public SampleUiSettingsViewModel()
            => SampleUiProbes.Track("settings-vm", this);

        private void Raise(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>插件自带的极简命令（不引宿主 MVVM 框架；Execute 委托由探针跟踪）。</summary>
    public sealed class SampleUiRelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;

        /// <summary>构造命令；执行体在 <see cref="Execute"/> 调用时同步运行。</summary>
        /// <param name="execute">执行体（非 null）。</param>
        public SampleUiRelayCommand(Action execute)
            => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

        /// <summary>恒可用（示例无需禁用态）。</summary>
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc/>
        public bool CanExecute(object? parameter) => true;

        /// <inheritdoc/>
        public void Execute(object? parameter) => _execute();
    }
}
