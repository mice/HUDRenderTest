[CmdletBinding()]
param(
    [string]$Device = '791d02af',
    [Parameter(Mandatory)][string]$VisualEvidencePath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string[]]$ClipNames = @('Static128_2','Static128_5','Static128_8'),
    [ValidateSet('DrawOnly','FullLifecycle')][string]$Phase = 'DrawOnly',
    [ValidateSet('FixedSequence','Realtime')][string]$Mode = 'FixedSequence',
    [switch]$Float16Comparison,
    [switch]$NativeGpuTiming,
    [string]$NativeGpuCalibrationPath,
    [string]$ApkPath = 'Builds/DamageTextComparison/f1b5fb5/DamageTextComparison.apk',
    [ValidateRange(3000, 100000)][int]$SampleFrames = 3600,
    [ValidateRange(0, 10000)][int]$WarmupFrames = 60
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = 'com.DefaultCompany.HUDRenderTest'
$remote = "/sdcard/Android/data/$package/files/DamageTextBenchmark"
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
if (Test-Path -LiteralPath $outputRoot) { throw 'Use a new output directory; prior samples are never overwritten.' }
New-Item -ItemType Directory -Path $outputRoot | Out-Null
function Invoke-Device([string[]]$Arguments) {
    $result = @(& adb -s $Device @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw ($result -join "`n") }
    $result
}
function Get-Manifests {
    @(Invoke-Device @('shell','ls','-1',$remote) | ForEach-Object { "$_".Trim() } | Where-Object { $_.EndsWith('.manifest.json') })
}
function Assert-BenchmarkForeground([switch]$AllowLauncher) {
    $activity=(Invoke-Device @('shell','dumpsys activity activities | grep mResumedActivity') -join "`n")
    $own=$activity.Contains($package+'/')
    $launcher=$AllowLauncher -and $activity.Contains('com.miui.home/')
    if(-not $own -and -not $launcher){throw 'Device foreground is occupied by another app; preserve it and wait for exclusive device access.'}
}
function Save-DeviceText([string]$Path, [string[]]$Arguments) {
    $lines = @(Invoke-Device $Arguments)
    [IO.File]::WriteAllLines($Path,[string[]]$lines,[Text.UTF8Encoding]::new($false))
    $lines
}
$visualPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $VisualEvidencePath))
$visual = Get-Content -LiteralPath $visualPath -Raw | ConvertFrom-Json
if (-not $visual.passed -or $visual.graphicsApi -ne 'Vulkan') { throw 'Accepted source-bound Vulkan visual evidence is required.' }
$visualHash = (Get-FileHash -LiteralPath $visualPath).Hash
Assert-BenchmarkForeground -AllowLauncher
$calibrationHash = ''
if ($NativeGpuTiming) {
    if (-not $NativeGpuCalibrationPath) { throw 'Native timing requires accepted calibration evidence.' }
    $NativeGpuCalibrationPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $NativeGpuCalibrationPath))
    $calibrationHash = (Get-FileHash -LiteralPath $NativeGpuCalibrationPath).Hash
    . (Join-Path $PSScriptRoot 'NativeGpuTiming/Validate-NativeGpuReport.ps1')
}
$apkAbsolute = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ApkPath))
$apkHash = (Get-FileHash -LiteralPath $apkAbsolute).Hash
$installedPaths = @(Invoke-Device @('shell','pm','path',$package) | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match '^package:/[A-Za-z0-9_./~+=-]+\.apk$' })
if ($installedPaths.Count -ne 1) { throw 'Expected one installed APK path, independent of ADB daemon diagnostics.' }
$installedPath = $installedPaths[0].Substring(8)
$hashLines = @(Invoke-Device @('shell','sha256sum',$installedPath) | Where-Object { "$_" -match '^[0-9A-Fa-f]{64}\s+' })
if ($hashLines.Count -ne 1) { throw 'Installed APK SHA256 response is missing or ambiguous.' }
$installedHash = ($hashLines[0] -split '\s+')[0]
if ($installedHash -ne $apkHash) { throw 'Installed APK differs from the registered artifact.' }
$settings = [ordered]@{}
foreach ($key in @('accelerometer_rotation','user_rotation','screen_brightness_mode','screen_brightness')) {
    $settings[$key] = (Invoke-Device @('shell','settings','get','system',$key) | Out-String).Trim()
}
$stayAwake = (Invoke-Device @('shell','settings','get','global','stay_on_while_plugged_in') | Out-String).Trim()
$orientationDump = (Invoke-Device @('shell','dumpsys','window','displays') | Out-String)
$fixedRotationEnabled = $orientationDump -match 'mFixedToUserRotation=true'
@{fixedToUserRotationEnabled=$fixedRotationEnabled;controlledPolicy='Android 11 WindowManager set-fix-to-user-rotation enabled; set-user-rotation lock 1 before each launch';expectedWidth=2340;expectedHeight=1080} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'orientation-policy.json') -Encoding utf8
$settings | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'original-settings.json') -Encoding utf8
$runs = @(); $ordinal = 0
foreach ($clip in $ClipNames) {
    foreach ($slot in @('A1','B1','B2','A2')) {
        $firstScheme = $slot.StartsWith('A')
        $maximumRealtime = if ($NativeGpuTiming) { [Math]::Max(4000,$SampleFrames) } else { 36000 }
        $plan = [ordered]@{Mode=1;Phase=0;Geometry=0;PositionFormat=0;WarmupFrames=$WarmupFrames;SampleFrames=$SampleFrames;MaximumRealtimeFrames=$maximumRealtime;Checkpoint=0.12;PreparationStep=0.016666668;BurstWorkload=($clip.StartsWith('Bursts') -and $Phase -eq 'FullLifecycle')}
        if ($Mode -eq 'Realtime') { $plan.Mode=2 }
        if ($Phase -eq 'FullLifecycle') { $plan.Phase=1 }
        if ($Float16Comparison) { if (-not $firstScheme) { $plan.PositionFormat=1 } }
        elseif ($firstScheme) { $plan.Geometry=1 }
        $runId = '{0:D2}-{1}-{2}-{3}-{4}' -f (++$ordinal),$clip,$Phase,$Mode,$slot
        $request = [ordered]@{schema='damage-text-capture-request-v1';runId=$runId;clipName=$clip;sourceFingerprint=$visual.sourceFingerprint;visualEvidenceSHA256=$visualHash;plan=$plan}
        if ($NativeGpuTiming) { $request.captureNativeGpu=$true; $request.gpuCalibrationSHA256=$calibrationHash }
        $requestPath = Join-Path $outputRoot ($runId + '.request.json')
        $request | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $requestPath -Encoding utf8
        $runs += [ordered]@{ordinal=$ordinal;slot=$slot;clip=$clip;id=$runId;requestPath=$requestPath;requestSHA256=(Get-FileHash -LiteralPath $requestPath).Hash}
    }
}
$protocol = [ordered]@{schema='damage-device-capture-protocol-v1';registeredUtc=[DateTime]::UtcNow.ToString('o');deviceSerial=$Device;apkSHA256=$apkHash;sourceFingerprint=$visual.sourceFingerprint;visualEvidenceSHA256=$visualHash;driverSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;phase=$Phase;mode=$Mode;frames=$SampleFrames;warmupFrames=$WarmupFrames;order='ABBA per Clip';minimumSamples=3000;pollSeconds=3;cooldownSeconds=10;startMaximumBatteryTemperatureTenths=350;afterMaximumBatteryTemperatureTenths=380;thermalStatusRequired=0;samplePolicy='Keep all raw frames and tails. No padding, trimming or automatic reruns. Reject invalid cohorts.';gpuScope='Registered APK does not supply a validated GPU counter; no GPU ranking';runs=$runs}
$protocol | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $outputRoot 'protocol.json') -Encoding utf8
if ($NativeGpuTiming) {
    $protocol.gpuScope='HUD-camera-instrumented; native timestamps including forced pass boundaries/query overhead; not uninstrumented whole-frame'
    $protocol.calibrationSHA256=$calibrationHash
    $protocol.nativeDrainFrames=16
    $protocol | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $outputRoot 'protocol.json') -Encoding utf8
}
$completed = @()
try {
    Invoke-Device @('shell','settings','put','system','accelerometer_rotation','0') | Out-Null
    Invoke-Device @('shell','settings','put','system','user_rotation','1') | Out-Null
    Invoke-Device @('shell','settings','put','system','screen_brightness_mode','0') | Out-Null
    Invoke-Device @('shell','settings','put','system','screen_brightness',$settings.screen_brightness) | Out-Null
    Invoke-Device @('shell','svc','power','stayon','usb') | Out-Null
    foreach ($run in $runs) {
        Assert-BenchmarkForeground -AllowLauncher
        $name = $run.id
        Invoke-Device @('shell','am','force-stop',$package) | Out-Null
        Invoke-Device @('shell','wm','set-fix-to-user-rotation','enabled') | Out-Null
        Invoke-Device @('shell','wm','set-user-rotation','lock','1') | Out-Null
        $battery = @(Save-DeviceText (Join-Path $outputRoot ($name + '.battery-before.txt')) @('shell','dumpsys','battery'))
        $thermal = @(Save-DeviceText (Join-Path $outputRoot ($name + '.thermal-before.txt')) @('shell','dumpsys','thermalservice'))
        $temperature = [int]([regex]::Match(($battery -join "`n"),'temperature:\s*(\d+)').Groups[1].Value)
        if ($temperature -gt 350 -or ($thermal -join "`n") -notmatch 'Thermal Status: 0') { throw "Environment gate failed before $name; no segment started." }
        $before = @(Get-Manifests)
        Invoke-Device @('push',$run.requestPath,"$remote/capture-request.json") | Out-Null
        Invoke-Device @('shell','input','keyevent','KEYCODE_WAKEUP') | Out-Null
        $started = [DateTime]::UtcNow
        Invoke-Device @('shell','am','start','-W','-n',"$package/com.unity3d.player.UnityPlayerActivity") | Out-Null
        Assert-BenchmarkForeground
        Write-Output "[$($run.ordinal)/$($runs.Count)] Sampling $name"
        $found = @(); $nextHeartbeat = [DateTime]::UtcNow.AddSeconds(20)
        while ([DateTime]::UtcNow -lt $started.AddMinutes(10)) {
            Start-Sleep -Seconds 3
            Assert-BenchmarkForeground
            $found = @(Get-Manifests | Where-Object { $_ -notin $before })
            if ($found.Count -gt 0) { break }
            $processId = (Invoke-Device @('shell','pidof',$package) | Out-String).Trim()
            if (-not $processId) { throw "App exited before export: $name" }
            if ([DateTime]::UtcNow -ge $nextHeartbeat) { Write-Output "Sampling continues: $name"; $nextHeartbeat=[DateTime]::UtcNow.AddSeconds(20) }
        }
        if ($found.Count -ne 1) { throw "Expected one completed export for $name; observed $($found.Count). Preserve state and inspect before any rerun." }
        $stem = $found[0] -replace '\.manifest\.json$',''
        foreach ($suffix in @('.manifest.json','.report.json','.frames.csv')) {
            Invoke-Device @('pull',"$remote/$stem$suffix",(Join-Path $outputRoot ($name + $suffix))) | Out-Null
        }
        $reportPath = Join-Path $outputRoot ($name + '.report.json')
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $manifest = Get-Content -LiteralPath (Join-Path $outputRoot ($name + '.manifest.json')) -Raw | ConvertFrom-Json
        if ((Get-FileHash -LiteralPath $reportPath).Hash -ne $manifest.reportSHA256 -or (Get-FileHash -LiteralPath (Join-Path $outputRoot ($name + '.frames.csv'))).Hash -ne $manifest.csvSHA256) { throw "Export hash mismatch: $name" }
        if ($report.captureRunId -ne $name -or $report.captureRequestSHA256 -ne $run.requestSHA256 -or $report.sourceFingerprint -ne $visual.sourceFingerprint -or
            $report.graphicsApi -ne 'Vulkan' -or $report.backend -ne 'IL2CPP' -or $report.executionMode -ne 'Player' -or $report.buildType -ne 'Release' -or
            $report.aborted -or $report.samples -lt 3000 -or -not $report.completeEventInterval -or $report.width -ne 2340 -or $report.height -ne 1080 -or
            $report.requestedFormat -ne $report.effectiveFormat) { throw "Invalid registered segment: $name" }
        if ($NativeGpuTiming) {
            $native=Test-DamageNativeGpuReport $report $NativeGpuCalibrationPath
            if ($report.gpuStatus -ne 'validated-native-timestamps' -or -not $native.valid) { throw "Native GPU gate failed: $name; $($native.errors -join ';')" }
        }
        $batteryAfter = @(Save-DeviceText (Join-Path $outputRoot ($name + '.battery-after.txt')) @('shell','dumpsys','battery'))
        $thermalAfter = @(Save-DeviceText (Join-Path $outputRoot ($name + '.thermal-after.txt')) @('shell','dumpsys','thermalservice'))
        $afterTemperature = [int]([regex]::Match(($batteryAfter -join "`n"),'temperature:\s*(\d+)').Groups[1].Value)
        if ($afterTemperature -gt 380 -or ($thermalAfter -join "`n") -notmatch 'Thermal Status: 0') { throw "Environment gate failed after $name; preserve and reject this cohort." }
        $row = [ordered]@{id=$name;startedUtc=$started.ToString('o');finishedUtc=[DateTime]::UtcNow.ToString('o');originalManifest=$found[0];reportSHA256=$manifest.reportSHA256;samples=$report.samples;scheme=$report.scheme;buildGuid=$report.buildGuid;consumed=$report.consumedEvents;completedWindows=$report.completedWindows;temperatureBefore=$temperature;temperatureAfter=$afterTemperature;scope='Completed registered timing segment; no screenshots or heavy profiling during sampling'}
        $row | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot ($name + '.capture.json')) -Encoding utf8
        $completed += $row
        Write-Output "Verified $name : $($report.samples) frames, $($report.consumedEvents) requests, $($report.scheme)."
        Invoke-Device @('shell','am','force-stop',$package) | Out-Null
        Start-Sleep -Seconds 10
    }
    $completed | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputRoot 'completed.json') -Encoding utf8
    Write-Output "Completed $($completed.Count) registered segments: $outputRoot"
}
finally {
    try {
        Invoke-Device @('shell','am','force-stop',$package) | Out-Null
        foreach ($key in $settings.Keys) { Invoke-Device @('shell','settings','put','system',$key,$settings[$key]) | Out-Null }
        $fixedMode = if ($fixedRotationEnabled) { 'enabled' } else { 'disabled' }
        Invoke-Device @('shell','wm','set-fix-to-user-rotation',$fixedMode) | Out-Null
        Invoke-Device @('shell','settings','put','global','stay_on_while_plugged_in',$stayAwake) | Out-Null
    }
    catch { Write-Output "Device cleanup needs inspection: $_; original settings are saved in $outputRoot" }
}
