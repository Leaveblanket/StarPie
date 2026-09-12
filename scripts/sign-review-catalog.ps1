#Requires -Version 7
<#
.SYNOPSIS
  插件审核清单签名工具（清单更新通道，ADR-0029 / plugins.md §11）。

.DESCRIPTION
  用首方私钥对 review-catalog.json 做分离 RSA-SHA256 签名，产出 <catalog>.sig（base64）。
  宿主侧只认 StarPie.Host 插件运行时里 pin 的公钥（SignedPluginReviewCatalog.FirstPartyPublicKeyPem）；
  私钥不入仓库，维护者本机默认 ~/.starpie-keys/review-catalog-private.pem。
  清单更新流程：编辑 review-catalog.json → 本脚本重签 → 替换宿主安装目录 plugins/ 下的文件对 → 重启宿主。

.PARAMETER CatalogPath
  清单 JSON 路径（默认仓库根 plugins/review-catalog.json）。

.PARAMETER KeyPath
  私钥 PEM 路径（默认 ~/.starpie-keys/review-catalog-private.pem）。
#>
[CmdletBinding()]
param(
    [string]$CatalogPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'plugins/review-catalog.json'),
    [string]$KeyPath = (Join-Path $HOME '.starpie-keys/review-catalog-private.pem')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $CatalogPath)) {
    Write-Warning "清单不存在：$CatalogPath"
    exit 2
}
if (-not (Test-Path $KeyPath)) {
    Write-Warning "私钥不存在：$KeyPath（生成方式见 docs/plugin-dev-handbook.md 的「审核清单维护」一节）"
    exit 2
}

$rsa = [System.Security.Cryptography.RSA]::Create()
try {
    try {
        $rsa.ImportFromPem((Get-Content $KeyPath -Raw))
    }
    catch {
        Write-Warning "私钥无法导入（$KeyPath）：$($_.Exception.Message)"
        exit 2
    }

    $document = [System.IO.File]::ReadAllBytes($CatalogPath)
    $signature = $rsa.SignData(
        $document,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    Set-Content -Path "$CatalogPath.sig" -Value ([Convert]::ToBase64String($signature)) -Encoding Ascii

    # 重签后自检：用刚写入的签名回验一次，防「签了但签错」静默失败。
    $verify = $rsa.VerifyData(
        [System.IO.File]::ReadAllBytes($CatalogPath),
        [Convert]::FromBase64String((Get-Content "$CatalogPath.sig" -Raw)),
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    if (-not $verify) {
        Write-Warning "自检失败：刚写入的签名回验不通过，请检查密钥对与文件编码"
        exit 2
    }
    Write-Host "已签名并自检通过：$CatalogPath.sig"
}
finally {
    $rsa.Dispose()
}
