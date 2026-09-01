using System;
using CommunityToolkit.Mvvm.Messaging;

namespace WinPieGestures.Services.Configuration
{
    /// <summary>
    /// 落盘编排订阅者 (T19)：住组合根，经 <see cref="IMessenger"/> 汇聚各页面 VM 的落盘请求——
    /// 防抖请求经注入的 <see cref="ISaveDebouncer"/> 折叠为一次延迟落盘；立即请求取消挂起防抖后
    /// 即刻落盘。冲刷时机与 T17 等价：防抖到期（本类）、显式保存/关窗隐藏/退出（
    /// <see cref="FlushPendingSave"/>）、导入前（组合根在导入委托里先冲刷）。
    /// 取代 RootSettingsViewModel 的保存编排（该聚合根已随 T19 删除）；UI 无关，可独立单测
    /// （Spec 预定缝②：页面 VM → IMessenger → 本订阅者 → 防抖器/配置服务）。
    /// </summary>
    public sealed class SettingsSaveOrchestrator
    {
        /// <summary>防抖自动保存间隔：连续设置变更折叠为一次落盘（T17 语义原样）。</summary>
        public static readonly TimeSpan AutoSaveDelay = TimeSpan.FromMilliseconds(400);

        private readonly IConfigService _config;
        private readonly ISaveDebouncer _debouncer;

        public SettingsSaveOrchestrator(IConfigService config, ISaveDebouncer debouncer, IMessenger messenger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _debouncer = debouncer ?? throw new ArgumentNullException(nameof(debouncer));

            if (messenger == null) throw new ArgumentNullException(nameof(messenger));
            messenger.Register<DebouncedSaveRequestedMessage>(this, (_, _) => ScheduleAutosave());
            messenger.Register<ImmediateSaveRequestedMessage>(this, (_, _) => SaveNow());
        }

        /// <summary>把运行态配置落盘。</summary>
        public void SaveConfig() => _config.Save();

        /// <summary>防抖落盘：经防抖器折叠连续变更，延迟到期统一落盘一次。</summary>
        private void ScheduleAutosave() => _debouncer.Schedule(SaveConfig, AutoSaveDelay);

        /// <summary>立即落盘：取消挂起的防抖后即刻落盘，覆盖"改完立刻关窗/导入"窗口。</summary>
        public void SaveNow()
        {
            _debouncer.CancelPending();
            SaveConfig();
        }

        /// <summary>冲刷挂起的自动保存并立即落盘（退出/关窗/导入前的兜底保存点）。</summary>
        public void FlushPendingSave() => SaveNow();
    }
}
