namespace StarPie.Services.Configuration
{
    /// <summary>
    /// 配置服务接缝：负责 config.json 的加载与保存，以及配置方案查找
    /// （前台进程匹配，未命中回退 Global 方案）。
    /// </summary>
    /// <remarks>
    /// 实现方独占文件 I/O，调用方通过构造函数注入获得本接口。
    /// 导入/导出能力保留在具体实现上，直到有消费方需要经由本接缝使用它们。
    /// </remarks>
    public interface IConfigService
    {
        /// <summary>当前运行态配置；加载失败或从未加载时为默认配置——永不为 null。</summary>
        AppConfig Current { get; }

        /// <summary>从磁盘加载配置：文件缺失时播种默认配置，JSON 损坏时回退默认值（不触碰文件）。</summary>
        void Load();

        /// <summary>把当前配置写回磁盘；失败仅输出 Debug 日志，绝不抛异常。</summary>
        void Save();

        /// <summary>返回前台进程对应的配置方案；进程名为空/未知时回退 Global 方案。</summary>
        WheelProfile GetProfileForProcess(string processName);

        /// <summary>返回 Global 方案；缺失时在 Profiles 头部重建一个空的 Global 方案。</summary>
        WheelProfile GetGlobalProfile();
    }
}
