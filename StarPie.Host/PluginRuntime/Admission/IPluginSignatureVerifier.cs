namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 包签名校验缝：对包内单个文件给出签名校验结论。
    /// </summary>
    /// <remarks>
    /// 生产实现走 Windows Authenticode（WinVerifyTrust）；测试以替身注入判定行为，
    /// 不在测试进程里做真实 Authenticode 签发。
    /// </remarks>
    public interface IPluginSignatureVerifier
    {
        /// <summary>校验指定文件的签名；文件缺席按 Unsigned 处理，不抛异常。</summary>
        PluginSignatureCheck Verify(string filePath);
    }
}
