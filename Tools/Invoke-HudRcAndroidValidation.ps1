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
