namespace StarPie.Abstractions.Ui
{
    /// <summary>
    /// UI 插件入口契约：宿主解析清单 <c>ui.entryType</c> 指向的类型后，在 UI 线程调用一次
    /// <see cref="RegisterUi"/> 完成全部注册。
    /// </summary>
    /// <remarks>
    /// 入口类型必须实现本接口且有公开无参构造函数；<see cref="RegisterUi"/> 抛出异常即判定装载失败。
    /// 本接口与 <c>StarPie.Abstractions.IPlugin</c> 分离：UI 入口只在宿主 UI 线程执行，注册产物一律
    /// 交给 <see cref="IPluginUiContext"/> 记账，插件不得自行创建或合并全局 WPF 对象。
    /// </remarks>
    public interface IPluginUiModule
    {
        /// <summary>注册本插件的 UI 资产（只注册不创建）；宿主在 UI 线程调用，正常返回即注册完成。</summary>
        /// <param name="context">宿主交给本插件的 UI 上下文（插件全部 UI 可达面）。</param>
        void RegisterUi(IPluginUiContext context);
    }
}
