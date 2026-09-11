<#
.SYNOPSIS
    从内核 Strings.resx（zh-CN 中性）再生成设计期字符串字典 DesignTimeStrings.xaml。

.DESCRIPTION
    ADR-0025：设计期字符串字典是签入生成物，定位为设计期投影（非运行时第二数据源）。
    新增/修改文案键后必须重跑本脚本并提交生成的 XAML；一致性由
    StarPie.Tests/DesignTimeStringsConsistencyTests.cs 锁“键集一致 + zh-CN 值与 resx 一致”。

    源 resx：StarPie.Host/Kernel/Localization/Strings.resx（本脚本所在目录由设计期投影
    与生成物共用，运行时本地化实现与四语言 resx 在宿主内核）。
    输出文件：本目录 DesignTimeStrings.xaml（Page 编译进 StarPie.Core 的惰性 BAML，
    各 UI 工程 Properties/DesignTimeResources.xaml 以 pack URI 在设计期合并）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File StarPie.Core/Services/Localization/GenerateDesignTimeStrings.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $scriptDir))
$resxPath = Join-Path $repoRoot 'StarPie.Host\Kernel\Localization\Strings.resx'
$xamlPath = Join-Path $scriptDir 'DesignTimeStrings.xaml'

if (-not (Test-Path -LiteralPath $resxPath)) {
    throw "未找到源 resx: $resxPath"
}

$presentationNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
$xamlNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml'
$xmlNamespace = 'http://www.w3.org/XML/1998/namespace'
$sysNamespace = 'clr-namespace:System;assembly=mscorlib'

[xml]$resx = Get-Content -LiteralPath $resxPath -Raw -Encoding utf8
$entries = @($resx.root.data) | Sort-Object -Property name

$doc = New-Object System.Xml.XmlDocument
$root = $doc.CreateElement('ResourceDictionary', $presentationNamespace)
$root.SetAttribute('xmlns', $presentationNamespace)
$root.SetAttribute('xmlns:x', $xamlNamespace)
$root.SetAttribute('xmlns:sys', $sysNamespace)
$doc.AppendChild($root) | Out-Null

foreach ($entry in $entries) {
    $value = [string]$entry.value
    $element = $doc.CreateElement('sys', 'String', $sysNamespace)
    $element.SetAttribute('Key', $xamlNamespace, [string]$entry.name)
    $element.SetAttribute('space', $xmlNamespace, 'preserve')
    $element.InnerText = $value
    $root.AppendChild($element) | Out-Null
}

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.IndentChars = '    '
$settings.NewLineChars = "`n"
$settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$settings.OmitXmlDeclaration = $true

$writer = [System.Xml.XmlWriter]::Create($xamlPath, $settings)
try {
    $doc.Save($writer)
}
finally {
    $writer.Dispose()
}

Write-Host "已再生成 DesignTimeStrings.xaml：$($entries.Count) 键 -> $xamlPath"
