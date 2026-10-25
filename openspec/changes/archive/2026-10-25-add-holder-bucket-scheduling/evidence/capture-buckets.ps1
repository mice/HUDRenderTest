param([string]$Device = '791d02af', [int]$FirstRun = 1, [int]$LastRun = 7)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$package = 'com.DefaultCompany.HUDRenderTest'
$remoteDirectory = "/sdcard/Android/data/$package/files"
$outputDirectory = Join-Path $repo 'Builds/HudRc/Xiaomi10/bucket-reports'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
function Invoke-Adb([string[]]$AdbArguments) {
    $result = @(& adb -s $Device @AdbArguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw ("ADB failed: " + ($result -join "`n")) }
    return $result
}
function Get-Reports {
    return @(Invoke-Adb @('shell', "ls $remoteDirectory/hud_rc_*.json 2>/dev/null || true") |
        ForEach-Object { "$_".Trim() } | Where-Object { $_ -like "$remoteDirectory/hud_rc_*.json" })
}
$variants = @('bucket-single','bucket-same','bucket-rotating','bucket-rotating','bucket-same','bucket-single','bucket-legacy')
$strategies = @{ 'bucket-single'=2; 'bucket-same'=1; 'bucket-rotating'=0 }
$originalStayAwake = (Invoke-Adb @('shell','settings','get','global','stay_on_while_plugged_in') | Out-String).Trim()
try {
    Invoke-Adb @('shell','svc','power','stayon','true') | Out-Null
    for ($index = $FirstRun; $index -le $LastRun; $index++) {
        $variant = $variants[$index - 1]
        $apk = Join-Path $repo "Builds/HudRc/Xiaomi10/$variant.apk"
        if (-not (Test-Path -LiteralPath $apk)) { throw "Missing APK: $apk" }
        Write-Output "[$index/7] Installing $variant"
        Invoke-Adb @('shell','am','force-stop',$package) | Out-Null
        Invoke-Adb @('install','-r',$apk) | Out-Null
        $before = @(Get-Reports)
        $batteryBefore = @(Invoke-Adb @('shell','dumpsys','battery') | Where-Object { "$_" -match 'level:|temperature:|status:' })
        $thermalBefore = @(Invoke-Adb @('shell','dumpsys','thermalservice') | Where-Object { "$_" -match 'Thermal Status:|mStatus=' })
        Invoke-Adb @('shell','input','keyevent','KEYCODE_WAKEUP') | Out-Null
        $started = [DateTime]::UtcNow
        Invoke-Adb @('shell','am','start','-n',"$package/com.unity3d.player.UnityPlayerActivity") | Out-Null
        $deadline = [DateTime]::UtcNow.AddSeconds(360)
        $newReports = @()
        while ([DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Seconds 3
            $newReports = @(Get-Reports | Where-Object { $_ -notin $before })
            if ($newReports.Count -gt 0) { break }
        }
        if ($newReports.Count -ne 1) { throw "Expected one completed report for $variant; found $($newReports.Count)" }
        # JSON is written before CSV. Wait for the exporter to finish before pulling.
        Start-Sleep -Seconds 3
        $remoteJson = $newReports[0]
        $localName = '{0:D2}-{1}' -f $index, $variant
        $localJson = Join-Path $outputDirectory ($localName + '.json')
        Invoke-Adb @('pull',$remoteJson,$localJson) | Out-Null
        Invoke-Adb @('pull',($remoteJson -replace '\.json$','.csv'),(Join-Path $outputDirectory ($localName + '.csv'))) | Out-Null
        if ($variant -ne 'bucket-legacy') {
            Invoke-Adb @('pull',($remoteJson -replace '\.json$','-buckets.csv'),(Join-Path $outputDirectory ($localName + '-buckets.csv'))) | Out-Null
        }
        $report = Get-Content -LiteralPath $localJson -Raw | ConvertFrom-Json
        $config = $report.configuration
        if ($config.holderCount -ne 256 -or -not $config.useSlim -or -not $config.useGenerationBatch -or
            -not $config.disableBuildMeshDiagnostics -or $config.spriteBacked -or $config.forceUguiFallback -or
            $config.sampleFrames -ne 3000 -or $config.targetFrameRate -ne 60 -or $report.scriptingBackend -ne 'IL2CPP') {
            throw "Wrong benchmark configuration: $localJson"
        }
        if ($variant -ne 'bucket-legacy') {
            if (-not $config.useBucketScheduling -or $config.bucketStrategy -ne $strategies[$variant] -or
                $report.schema -ne 'hud-rc-buckets-v1' -or $report.frames.Count -ne 9006) { throw 'Bucket strategy/report mismatch' }
        } elseif ($config.useBucketScheduling -or $report.frames.Count -ne 9000) { throw 'Legacy control mismatch' }
        $batteryAfter = @(Invoke-Adb @('shell','dumpsys','battery') | Where-Object { "$_" -match 'level:|temperature:|status:' })
        $thermalAfter = @(Invoke-Adb @('shell','dumpsys','thermalservice') | Where-Object { "$_" -match 'Thermal Status:|mStatus=' })
        $memoryAfter = @(Invoke-Adb @('shell','dumpsys','meminfo',$package))
        Invoke-Adb @('shell','am','force-stop',$package) | Out-Null
        [ordered]@{
            variant=$variant; order=$index; device=$Device; startedUtc=$started.ToString('o')
            apk=(Split-Path $apk -Leaf); apkSha256=(Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash
            originalReport=$remoteJson; reportSha256=(Get-FileHash -LiteralPath $localJson -Algorithm SHA256).Hash
            sourceFingerprint=$report.sourceFingerprint; batteryBefore=$batteryBefore; batteryAfter=$batteryAfter
            thermalBefore=$thermalBefore; thermalAfter=$thermalAfter; memoryAfter=$memoryAfter
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputDirectory ($localName + '-metadata.json')) -Encoding utf8
        Write-Output "[$index/7] Verified $localJson"
        Start-Sleep -Seconds 10
    }
} finally {
    Invoke-Adb @('shell','am','force-stop',$package) | Out-Null
    Invoke-Adb @('shell','settings','put','global','stay_on_while_plugged_in',$originalStayAwake) | Out-Null
}
