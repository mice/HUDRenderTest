[CmdletBinding()]
param(
    [ValidateSet('Build', 'PlayMode', 'Measure')][string]$Action = 'Measure',
    [int]$HolderCount = 256,
    [int]$SampleFrames = 600,
    [int]$WarmupFrames = 60,
    [switch]$UseSlim,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.53f1\Editor\Unity.exe',
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repoRoot 'HUDRenderTestSample'
$outputDir = Join-Path $projectPath 'Logs/HudRc'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
$logPath = Join-Path $outputDir "$Action-$stamp.log"
$resultPath = Join-Path $outputDir "$Action-$stamp.xml"
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
$dirty = @(& git -C $repoRoot status --porcelain).Count -gt 0
$sourcePaths = @(& git -C $repoRoot ls-files --cached --others --exclude-standard -- com.uidatarender HUDRenderTestSample/Assets Tools)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory source files.' }
$manifest = foreach ($relative in ($sourcePaths | Sort-Object -Unique)) {
    $absolute = Join-Path $repoRoot $relative
    if (Test-Path -LiteralPath $absolute -PathType Leaf) { "$((Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash)  $relative" }
}
$manifestPath = Join-Path $outputDir "$Action-$stamp-sources.sha256"
$manifest | Set-Content -LiteralPath $manifestPath -Encoding utf8
$fingerprint = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$previousCommit = $env:HUD_RC_SOURCE_COMMIT
$previousStatus = $env:HUD_RC_SOURCE_STATUS
$previousFingerprint = $env:HUD_RC_SOURCE_FINGERPRINT
$env:HUD_RC_SOURCE_COMMIT = $head
$env:HUD_RC_SOURCE_STATUS = if ($dirty) { 'dirty working tree; see source fingerprint' } else { 'clean' }
$env:HUD_RC_SOURCE_FINGERPRINT = $fingerprint
$arguments = @('-batchmode', '-projectPath', $projectPath, '-logFile', $logPath)
switch ($Action) {
    'Build' { $arguments += @('-executeMethod', 'HudRcSceneBuilder.BuildBatch') }
    'PlayMode' { $arguments += @('-runTests', '-testPlatform', 'PlayMode', '-assemblyNames', 'Tests.PlayMode', '-testResults', $resultPath) }
    'Measure' {
        $arguments += @('-executeMethod', 'HudRcSceneBuilder.RunBatch', '-hudRcCount', "$HolderCount", '-hudRcSamples', "$SampleFrames", '-hudRcWarmup', "$WarmupFrames")
        if ($UseSlim) { $arguments += '-hudRcSlim' }
    }
}

try {
    $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not $process.WaitForExit(1000)) {
        if ((Get-Date) -ge $deadline) {
            Stop-Process -Id $process.Id -Force
            throw "Unity watchdog timed out. Log: $logPath"
        }
    }
    if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)). Log: $logPath" }
    if ($Action -eq 'PlayMode') {
        [xml]$xml = Get-Content -Raw -LiteralPath $resultPath
        $run = $xml.'test-run'
        if ([int]$run.failed -gt 0 -or [int]$run.total -eq 0 -or $run.result -ne 'Passed') { throw "PlayMode failed: $resultPath" }
        Write-Output "PlayMode $($run.passed)/$($run.total) passed. XML: $resultPath"
    }
    if ($Action -eq 'Measure') {
        $line = Select-String -LiteralPath $logPath -Pattern '^HUD_RC_REPORT=(.+)$' | Select-Object -Last 1
        if ($null -eq $line) { throw "No completed report marker. Log: $logPath" }
        $reportPath = $line.Matches[0].Groups[1].Value.Trim()
        if (-not (Test-Path -LiteralPath $reportPath)) { throw "Missing report: $reportPath" }
        Write-Output "Report: $reportPath"
    }
    Write-Output "Log: $logPath"
    Write-Output "Source manifest: $manifestPath ($fingerprint)"
}
finally {
    $env:HUD_RC_SOURCE_COMMIT = $previousCommit
    $env:HUD_RC_SOURCE_STATUS = $previousStatus
    $env:HUD_RC_SOURCE_FINGERPRINT = $previousFingerprint
}
