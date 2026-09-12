using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// Authenticode 签名校验（WinVerifyTrust）：可信链 → <see cref="PluginSignatureStatus.Valid"/>；
    /// 有签名但链不可信（自签名/不受信根）→ Invalid，签名主体与发布者指纹仍提取，
    /// 供「受 pin 的发布者证书」路径判定；无签名/不可解析 → Unsigned；
    /// 内容摘要与签名不符（被篡改）→ Invalid 且 <see cref="PluginSignatureCheck.ContentMismatch"/> 为真。
    /// </summary>
    /// <remarks>
    /// 本类只回答"这个文件的签名可不可信"，不做任何准入决策；Authenticode 只证明"谁签的"，
    /// 不证明"写着安全"——是否装载仍由准入策略按清单与开发者模式裁决。
    /// </remarks>
    public sealed class WinTrustSignatureVerifier : IPluginSignatureVerifier
    {
        private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11D0-821A-00A24CDB6A0C");

        private const int TrustENoSignature = unchecked((int)0x800B0100);
        private const int TrustEBadDigest = unchecked((int)0x80096010);
        private const int TrustEFailure = unchecked((int)0x800B010B);

        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionIgnore = 0;

        /// <inheritdoc/>
        public PluginSignatureCheck Verify(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new PluginSignatureCheck(PluginSignatureStatus.Unsigned, null, null, ContentMismatch: false, "文件不存在，无签名可校验");
            }

            int hresult = RunWinVerifyTrust(filePath);
            (string? subject, string? publisherHash) = TryExtractCertificate(filePath);
            if (hresult == 0)
            {
                return new PluginSignatureCheck(PluginSignatureStatus.Valid, subject, publisherHash, ContentMismatch: false, null);
            }

            if (publisherHash is null)
            {
                // 提取不到任何签名证书 = 实质无签名（含损坏到无法解析的文件）。
                return new PluginSignatureCheck(
                    PluginSignatureStatus.Unsigned, null, null, ContentMismatch: false, "文件无数字签名");
            }

            return new PluginSignatureCheck(
                PluginSignatureStatus.Invalid,
                subject,
                publisherHash,
                ContentMismatch: hresult == TrustEBadDigest,
                Detail: hresult == TrustENoSignature
                    ? "存在签名但未通过可信链校验"
                    : hresult == TrustEBadDigest
                        ? "内容摘要与签名不符：文件在签名后被篡改"
                        : $"Authenticode 校验失败：0x{hresult:X8}");
        }

        private static int RunWinVerifyTrust(string filePath)
        {
            var fileInfo = new WinTrustFileInfo
            {
                CbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                PcwszFilePath = filePath,
            };
            var data = new WinTrustData
            {
                CbStruct = (uint)Marshal.SizeOf<WinTrustData>(),
                DwUIChoice = WtdUiNone,
                FdwRevocationChecks = WtdRevokeNone,
                DwUnionChoice = WtdChoiceFile,
                DwStateAction = WtdStateActionIgnore,
            };
            try
            {
                data.PFile = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
                Marshal.StructureToPtr(fileInfo, data.PFile, fDeleteOld: false);
                return WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, ref data);
            }
            catch (Exception exception) when (exception is Win32Exception or DllNotFoundException or EntryPointNotFoundException)
            {
                // WinVerifyTrust 缺席或调用环境异常时按不可信处理，不阻断启动扫描。
                return TrustEFailure;
            }
            finally
            {
                if (data.PFile != IntPtr.Zero)
                {
                    // 路径字符串由 StructureToPtr 单独分配，须先销毁结构再释放缓冲。
                    Marshal.DestroyStructure<WinTrustFileInfo>(data.PFile);
                    Marshal.FreeHGlobal(data.PFile);
                }
            }
        }

        private static (string? Subject, string? PublisherHash) TryExtractCertificate(string filePath)
        {
            try
            {
                using X509Certificate2 certificate = new(X509Certificate.CreateFromSignedFile(filePath));
                return (certificate.Subject, certificate.GetCertHashString(HashAlgorithmName.SHA256));
            }
            catch (Exception exception) when (
                exception is CryptographicException or IOException or Win32Exception)
            {
                // 文件无签名或证书不可解析：pin 路径无法使用，返回空主体。
                return (null, null);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint CbStruct;
            public string PcwszFilePath;
            public IntPtr HFile;
            public IntPtr PgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustData
        {
            public uint CbStruct;
            public IntPtr PPolicyCallbackData;
            public IntPtr PSIPClientData;
            public uint DwUIChoice;
            public uint FdwRevocationChecks;
            public uint DwUnionChoice;
            public IntPtr PFile; // union 的 CHOICE_FILE 分支
            public uint DwStateAction;
            public IntPtr HWVTStateData;
            public IntPtr PwszURLReference;
            public uint DwProvFlags;
            public uint DwUIContext;
            public IntPtr PSignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true)]
        private static extern int WinVerifyTrust(
            IntPtr hWnd,
            [MarshalAs(UnmanagedType.LPStruct)] Guid policyActionId,
            ref WinTrustData data);
    }
}
