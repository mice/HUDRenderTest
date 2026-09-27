[CmdletBinding()]
param(
    [ValidateSet('Build','Install')][string]$Action = 'Build',
    [string]$Device = '791d02af',
    [string]$ApkPath = 'Builds/HudRc/Xiaomi10/HUDRenderTest.apk',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.53f1\Editor\Unity.exe',
    [string]$Adb = 'adb',
    [switch]$Development,
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repoRoot 'HUDRenderTestSample'
$apkAbsolute = [IO.Path]::GetFullPath((Join-Path $repoRoot $ApkPath))
$logDir = Join-Path $projectPath 'Logs/HudRcAndroid'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

if ($Action -eq 'Build') {
    $head = (& git -C $repoRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
    $dirty = @(& git -C $repoRoot status --porcelain).Count -gt 0
    $sourcePaths = @(& git -C $repoRoot ls-files --cached --others --exclude-standard -- com.uidatarender HUDRenderTestSample/Assets Tools)
    $manifestPath = Join-Path $logDir ('Build-' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '-sources.sha256')
    $manifest = foreach ($relative in ($sourcePaths | Sort-Object -Unique)) {
        $absoluteSource = Join-Path $repoRoot $relative
        if (Test-Path -LiteralPath $absoluteSource -PathType Leaf) { "$((Get-FileHash -LiteralPath $absoluteSource -Algorithm SHA256).Hash)  $relative" }
    }
    $manifest | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $env:HUD_RC_SOURCE_COMMIT = $head
    $env:HUD_RC_SOURCE_STATUS = if ($dirty) { 'dirty working tree; see source fingerprint' } else { 'clean' }
    $env:HUD_RC_SOURCE_FINGERPRINT = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    $logPath = Join-Path $logDir ('Build-' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.log')
    $args = @('-batchmode','-quit','-projectPath',$projectPath,'-executeMethod','HudRcSceneBuilder.BuildAndroidBatch','-hudRcAndroidOutput',$apkAbsolute,'-logFile',$logPath)
    if ($Development) { $args += '-hudRcDevelopment' }
    $process = Start-Process -FilePath $UnityExe -ArgumentList $args -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { Stop-Process -Id $process.Id -Force; throw "Unity Android build timed out. Log: $logPath" }
    if ($process.ExitCode -ne 0) { throw "Unity Android build failed ($($process.ExitCode)). Log: $logPath" }
    if (-not (Test-Path $apkAbsolute)) { throw "APK was not produced: $apkAbsolute" }
    Write-Output "APK: $apkAbsolute"
    exit 0
}

& $Adb devices -l
& $Adb -s $Device wait-for-device
& $Adb -s $Device install -r $apkAbsolute
if ($LASTEXITCODE -ne 0) { throw "ADB install failed for $Device" }
Write-Output "Installed $apkAbsolute on $Device"
