namespace StarPie.Manifest
{
    /// <summary>
    /// 清单里的一条能力引用：能力 id + 该能力接口的 ABI 号。
    /// </summary>
    public sealed class PluginCapabilityReference
    {
        /// <summary>能力 id（如 <c>program-source</c>）。</summary>
        public string? Id { get; set; }

        /// <summary>能力接口 ABI 号（从 1 起）。</summary>
        public int Abi { get; set; }
    }
}
