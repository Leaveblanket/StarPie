#Requires -Version 7
<#
.SYNOPSIS
  StarPie pywinauto e2e 运行器（默认静默后台形态）。

.DESCRIPTION
  默认：被测应用以 --background 启动（屏幕左上角、不可激活、点击穿透、不进任务栏、不启全局钩子，
  托盘可见），键鼠不被打扰、不移动物理光标、不抢前台；pytest 输出落
  artifacts/e2e/last-run.log，junitxml 落 artifacts/e2e/last-run.xml，
  运行状态落 artifacts/e2e/status.json。

  -OnScreen   调试用：不加 --background，窗口正常显示（Save 会弹系统提示框，
              用例内的对话框关闭分支此时生效）。
  -NoBuild    跳过 dotnet build（默认先 build Release）。
  -NoWait     不阻塞：后台启动 pytest 后立即返回（用 -Status 查结果；status.json 记 detached=true）。
  -Status     只查状态/结果，不跑测试；退出码：0=最近一次通过，非 0=最近一次失败，
              2=最近一次 build 失败，3=仍在运行/未产出结果，4=无运行记录。

  解释器：默认用仓库内隔离 venv（.venv，依赖锁定在 tests/requirements.txt）；
  解析顺序为 -Python 显式指定 > .venv > PATH 的 python（回退 PATH 时会警告"解释器未锁定"）。
  失败截图：静默形态窗口在屏内被 DWM 合成，失败时用 PrintWindow 抓真实内容；
  缺 pillow 时 status.json 的 screenshotAvailable=false + screenshotNote 说明，-Status 可见。

  并发保护：同一时间只允许一个 e2e（命名 Mutex），避免两个运行互抢桌面对话框与沙盒。
  详见 docs/architecture/host.md 与 docs/adr/0031-e2e-silent-background-run.md。
#>
[CmdletBinding()]
param(
    [switch]$OnScreen,
    [switch]$NoBuild,
    [switch]$NoWait,
    [switch]$Status,
    [string]$Python = '',
    [string]$TestPath = 'tests/test_settings.py'
)

$ErrorActionPreference = 'Stop'

# -NoWait 的分离子进程由环境标记识别（外层启动时置位并随进程继承，不对外暴露参数）；
# 它决定 Mutex 是等待接管还是立即失败，以及 status.json 的 detached 口径。
$script:isDetached = ($env:STARPIE_E2E_DETACHED -eq '1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'artifacts/e2e'
$logPath = Join-Path $outDir 'last-run.log'
$errPath = Join-Path $outDir 'last-run.err.log'
$xmlPath = Join-Path $outDir 'last-run.xml'
$statusPath = Join-Path $outDir 'status.json'
$mutexName = 'Global\StarPie_E2E_Runner'

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 解释器解析：优先 -Python 显式指定；其次仓库内隔离 venv（.venv，依赖见 tests/requirements.txt）；
# 最后回退 PATH 上的 python——回退时显式警告"解释器未锁定"（静默回退正是 #136 的病根）。
$venvPython = Join-Path $repoRoot '.venv\Scripts\python.exe'
if (-not $Python) {
    if (Test-Path $venvPython) {
        $Python = $venvPython
    }
    elseif (Get-Command python -ErrorAction SilentlyContinue) {
        Write-Warning "未找到仓库 .venv（$venvPython），回退 PATH 的 python（解释器未锁定）。按 CONTRIBUTING 建立：python -m venv .venv; .venv\Scripts\python -m pip install -r tests/requirements.txt"
        $Python = 'python'
    }
    else {
        Write-Warning '未找到 Python：请用 -Python 指定解释器，或按 CONTRIBUTING 建立仓库 .venv。'
        exit 2
    }
}

# 失败截图口径：静默形态窗口在屏内，截图可用；仅 PIL 缺件时标记不可用并写明原因。
$screenshotAvailable = $true
$screenshotNote = ''
& $Python -c "import PIL" 2>$null
if ($LASTEXITCODE -ne 0) {
    $screenshotAvailable = $false
    $screenshotNote = '缺 pillow（pip install -r tests/requirements.txt 恢复）'
}

function Write-Status {
    param(
        [string]$State,
        [int]$ExitCode = -1,
        [string]$Note = '',
        [bool]$ScreenshotAvailable = $true,
        [string]$ScreenshotNote = ''
    )
    $status = [ordered]@{
        state      = $State
        pid        = $script:runnerPid
        exitCode   = $ExitCode
        updatedAt  = (Get-Date).ToString('s')
        onScreen   = [bool]$OnScreen
        detached   = [bool]$script:isDetached
        screenshotAvailable = $ScreenshotAvailable
        screenshotNote = $ScreenshotNote
        log        = $logPath
        report     = $xmlPath
        note       = $Note
    }
    $status | ConvertTo-Json | Set-Content -Path $statusPath -Encoding utf8
}

if ($Status) {
    if (-not (Test-Path $statusPath)) { Write-Host '尚无运行记录'; exit 4 }
    $s = Get-Content $statusPath -Raw | ConvertFrom-Json
    $alive = $false
    if ($s.pid) {
        $alive = [bool](Get-Process -Id $s.pid -ErrorAction SilentlyContinue)
    }
    $detached = $false
    if ($s.PSObject.Properties['detached']) { $detached = [bool]$s.detached }
    Write-Host ("state={0} pid={1} alive={2} exitCode={3} detached={4} updatedAt={5}" -f `
        $s.state, $s.pid, $alive, $s.exitCode, $detached, $s.updatedAt)
    $shot = $true
    if ($s.PSObject.Properties['screenshotAvailable']) { $shot = [bool]$s.screenshotAvailable }
    if (-not $shot) {
        $shotNote = '缺 pillow（pip install -r tests/requirements.txt 恢复）'
        if ($s.PSObject.Properties['screenshotNote'] -and $s.screenshotNote) { $shotNote = $s.screenshotNote }
        Write-Host ("screenshot: 不可用（{0}）" -f $shotNote)
    }
    if ((Test-Path $xmlPath) -and -not $alive -and $s.state -eq 'finished') {
        [xml]$x = Get-Content $xmlPath -Raw
        # pytest 的 junit 根为 <testsuites>，嵌套一层 <testsuite>
        $suite = if ($x.testsuites) { $x.testsuites.testsuite } else { $x.testsuite }
        Write-Host ("report: tests={0} failures={1} errors={2} skipped={3} time={4}s" -f $suite.tests, $suite.failures, $suite.errors, $suite.skipped, $suite.time)
        if ([int]$suite.failures + [int]$suite.errors -gt 0) {
            @($suite.testcase) | Where-Object { $_.failure -or $_.error } | ForEach-Object {
                Write-Host ("  FAIL {0}.{1}" -f $_.classname, $_.name)
            }
        }
    }

    # 退出码反映最近一次结果，供脚本/agent 一条命令判定"上次 e2e 过没过"。
    $code = -1
    if ($s.PSObject.Properties['exitCode']) { $code = [int]$s.exitCode }
    if ($alive -or $s.state -eq 'running') { exit 3 }
    if ($s.state -eq 'blocked') { exit 3 }
    if ($s.state -eq 'build-failed') { exit 2 }
    if ($s.state -eq 'finished' -and $code -ge 0) { exit $code }
    exit 3
}

$mtx = New-Object System.Threading.Mutex($false, $mutexName)
# -NoWait 的分离子进程启动时外层还持有 Mutex，故允许它等待接管；前台运行仍是立即失败。
$mutexWaitMs = if ($script:isDetached) { 60000 } else { 0 }
$acquired = $mtx.WaitOne($mutexWaitMs)
if (-not $acquired) {
    Write-Warning "另一个 e2e 正在运行（Mutex $mutexName 被占用）。等它结束后重试，或跑 -Status 查看。"
    $script:runnerPid = $PID
    Write-Status -State 'blocked' -ExitCode 3 -Note 'Mutex 被占用' `
        -ScreenshotAvailable $screenshotAvailable -ScreenshotNote $screenshotNote
    exit 3
}

try {
    if (-not $NoBuild) {
        Write-Host 'build (Release)...'
        & dotnet build (Join-Path $repoRoot 'StarPie.slnx') -c Release -v q --nologo
        if ($LASTEXITCODE -ne 0) {
            $script:runnerPid = $PID
            Write-Status -State 'build-failed' -ExitCode 2 -Note 'build 失败，e2e 未启动' `
                -ScreenshotAvailable $screenshotAvailable -ScreenshotNote $screenshotNote
            Write-Warning "build 失败（exit $LASTEXITCODE），e2e 不启动。"
            exit 2
        }
    }

    if ($OnScreen) { $env:STARPIE_E2E_ONSCREEN = '1' } else { $env:STARPIE_E2E_ONSCREEN = '0' }

    $pytestArgs = @(
        '-m', 'pytest', '-v',
        (Join-Path $repoRoot $TestPath),
        "--junitxml=$xmlPath"
    )

    if ($NoWait) {
        # 让被拉起的运行自己持有 Mutex：以阻塞模式重新进入本脚本，外层立即返回。
        # 若直接 Start-Process pytest，外层 finally 会先释放 Mutex，-NoWait 的并发保护就失效了。
        $innerArgs = @('-NoProfile', '-File', $PSCommandPath)
        if ($NoBuild) { $innerArgs += '-NoBuild' }
        if ($OnScreen) { $innerArgs += '-OnScreen' }
        $innerArgs += @('-TestPath', $TestPath)
        $innerArgs += @('-Python', $Python)
        # 外层此刻仍持有 Mutex：置分离标记让子进程等它退出后接管，避免抢跑失败（静默不跑）。
        $env:STARPIE_E2E_DETACHED = '1'
        $script:isDetached = $true
        $proc = Start-Process -FilePath 'pwsh' -ArgumentList $innerArgs -PassThru -WindowStyle Hidden
        # 子进程已继承标记；从当前进程环境移除，避免同会话再次调用时误判为分离运行。
        Remove-Item Env:STARPIE_E2E_DETACHED -ErrorAction SilentlyContinue
        $script:runnerPid = $proc.Id
        Write-Status -State 'running' -Note 'detached (-NoWait)' `
            -ScreenshotAvailable $screenshotAvailable -ScreenshotNote $screenshotNote
        Write-Host ("e2e 已后台启动：pid={0}；日志 {1}；状态用 -Status 查" -f $proc.Id, $logPath)
        exit 0
    }

    $script:runnerPid = $PID
    Write-Status -State 'running' -Note 'foreground' `
        -ScreenshotAvailable $screenshotAvailable -ScreenshotNote $screenshotNote
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $Python @pytestArgs 2>&1 | Tee-Object -FilePath $logPath
    $code = $LASTEXITCODE
    $sw.Stop()

    Write-Status -State 'finished' -ExitCode $code -Note ("{0:n0}s" -f $sw.Elapsed.TotalSeconds) `
        -ScreenshotAvailable $screenshotAvailable -ScreenshotNote $screenshotNote
    Write-Host ("e2e 结束：exit={0} 用时 {1:n0}s；日志 {2}" -f $code, $sw.Elapsed.TotalSeconds, $logPath)
    exit $code
}
finally {
    if ($mtx) {
        try { $mtx.ReleaseMutex() } catch { }
        $mtx.Dispose()
    }
}
