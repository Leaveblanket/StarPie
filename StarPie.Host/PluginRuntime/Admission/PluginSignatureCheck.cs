namespace StarPie.PluginRuntime.Admission
{
    /// <summary>包签名校验结论三态。</summary>
    public enum PluginSignatureStatus
    {
        /// <summary>文件无数字签名。</summary>
        Unsigned,

        /// <summary>有签名但不可信：链校验失败或自签名；发布者指纹仍在，供受 pin 的发布者路径判定。</summary>
        Invalid,

        /// <summary>Authenticode 可信链校验通过。</summary>
        Valid,
    }

    /// <summary>
    /// 一个包文件的签名校验结果：状态、签名主体、发布者指纹（SHA-256 证书哈希，pin 用）
    /// 与内容完整性结论。
    /// </summary>
    /// <param name="Status">校验结论三态。</param>
    /// <param name="Subject">签名主体（X.500 名称；取不到时为 null）。</param>
    /// <param name="PublisherHash">发布者指纹：签名证书的 SHA-256 哈希十六进制串（取不到时为 null）。</param>
    /// <param name="ContentMismatch">内容摘要与签名不符（文件被篡改）；此时发布者可信不等于文件可信。</param>
    /// <param name="Detail">可读校验说明（失败时含 HRESULT；成功时为 null）。</param>
    public sealed record PluginSignatureCheck(
        PluginSignatureStatus Status,
        string? Subject,
        string? PublisherHash,
        bool ContentMismatch,
        string? Detail);
}
