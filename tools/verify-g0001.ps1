# G0001 本地开发门禁。仅运行锁定还原、Debug 构建、单元/Headless 测试、格式和差异检查。
# 设计思路：每条命令立即记录退出码；任一步失败立即停止，失败也写出结构化结果，防止旧的通过记录误导审核。
# 本脚本不调用 Windows CI、Release、ZIP、安装、部署、签名或 Host 发布门禁。
param([string]$Root = (Split-Path $PSScriptRoot -Parent))

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path -LiteralPath $Root).Path
$taskEvidence = Join-Path $Root 'TestResults/G0001/local-gate'
$taskSummaryPath = Join-Path $taskEvidence 'summary.json'
$taskSteps = [Collections.Generic.List[object]]::new()
$taskCounters = $null
$taskStarted = [DateTimeOffset]::UtcNow.ToString('O')
$taskOldScreenshotDirectory = [Environment]::GetEnvironmentVariable('AC_SCREENSHOT_DIR', 'Process')
New-Item -ItemType Directory -Force -Path $taskEvidence | Out-Null

function Write-GateSummary {
    param([string]$Status, [string]$Failure = '', $Counters = $null)
    [pscustomobject]@{
        status = $Status
        startedAtUtc = $taskStarted
        updatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        configuration = 'Debug'
        steps = $taskSteps.ToArray()
        tests = $Counters
        failure = $Failure
        realHostValidated = $false
        releaseGatesExecuted = $false
        windowsCiExecuted = $false
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskSummaryPath -Encoding utf8
}

function Invoke-DevelopmentCheck {
    param([string]$Name, [string]$Executable, [string[]]$Arguments)
    Write-Host "$Name : $Executable $($Arguments -join ' ')"
    $taskTimer = [Diagnostics.Stopwatch]::StartNew()
    & $Executable @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $taskEvidence "$Name.log")
    $taskExit = $LASTEXITCODE
    $taskSteps.Add([pscustomobject]@{ name = $Name; exitCode = $taskExit; elapsedMs = $taskTimer.ElapsedMilliseconds })
    if ($taskExit -ne 0) { throw "$Name 失败，退出码 $taskExit。详情见本地日志。" }
}

Write-GateSummary 'running'
Push-Location -LiteralPath $Root
try {
    $env:AC_SCREENSHOT_DIR = Join-Path $taskEvidence 'screenshots'
    Invoke-DevelopmentCheck 'restore' 'dotnet' @('restore', 'DataStructurePlugin.slnx', '--locked-mode')
    Invoke-DevelopmentCheck 'build' 'dotnet' @('build', 'DataStructurePlugin.slnx', '-c', 'Debug', '--no-restore', '-warnaserror')
    Invoke-DevelopmentCheck 'tests' 'dotnet' @('test', 'DataStructurePlugin.slnx', '-c', 'Debug', '--no-build', '--no-restore', '--logger', 'trx;LogFileName=tests.trx', '--results-directory', $taskEvidence)
    [xml]$taskTrx = Get-Content -Raw -LiteralPath (Join-Path $taskEvidence 'tests.trx')
    $taskCount = $taskTrx.TestRun.ResultSummary.Counters
    $taskCounters = [pscustomobject]@{
        total = [int]$taskCount.total
        passed = [int]$taskCount.passed
        failed = [int]$taskCount.failed
        skipped = [int]$taskCount.notExecuted
    }
    # 零测试或跳过测试不能作为完整门禁通过；TRX 计数还需与原生命令退出码一致。
    if ($taskCounters.total -eq 0 -or $taskCounters.passed -ne $taskCounters.total -or $taskCounters.failed -ne 0 -or $taskCounters.skipped -ne 0) {
        throw '测试计数不满足全部执行且全部通过的要求。'
    }
    Invoke-DevelopmentCheck 'format' 'dotnet' @('format', 'DataStructurePlugin.slnx', '--verify-no-changes', '--no-restore')
    Invoke-DevelopmentCheck 'diff-check' 'git' @('-c', 'core.safecrlf=false', 'diff', '--check')
    Write-GateSummary 'passed' '' $taskCounters
    Write-Host "G0001_LOCAL_GATE_OK $taskSummaryPath"
}
catch {
    Write-GateSummary 'failed' $_.Exception.Message $taskCounters
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
finally {
    [Environment]::SetEnvironmentVariable('AC_SCREENSHOT_DIR', $taskOldScreenshotDirectory, 'Process')
    Pop-Location
}
