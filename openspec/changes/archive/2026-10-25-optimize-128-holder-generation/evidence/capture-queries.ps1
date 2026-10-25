param([string]$Device = '791d02af', [int]$FirstRun = 1, [int]$LastRun = 4, [switch]$SkipFirstInstall)
$ErrorActionPreference = 'Stop'
$repo = 'D:\UnityPackages\HUDRenderPackage'
$package = 'com.DefaultCompany.HUDRenderTest'
$remoteDirectory = "/sdcard/Android/data/$package/files"
$outputDirectory = Join-Path $repo 'Builds/HudRc/Xiaomi10/query-reports-20261025'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
function Invoke-Adb([string[]]$AdbArguments) {
    $result = @(& adb -s $Device @AdbArguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw ("ADB failed: " + ($result -join "`n")) }
    return $result
}
function Get-Reports {
    $listing = Invoke-Adb @('shell', "ls $remoteDirectory/hud_rc_*.json 2>/dev/null || true")
    return @($listing | ForEach-Object { "$_".Trim() } | Where-Object { $_ -like "$remoteDirectory/hud_rc_*.json" })
}
$deviceState = @(Invoke-Adb @('get-state'))
if (("$($deviceState[-1])").Trim() -ne 'device') { throw ('Device is not ready: ' + ($deviceState -join ' ')) }
$variants = @('query-live','query-batch','query-batch','query-live')
$originalStayAwake = (Invoke-Adb @('shell','settings','get','global','stay_on_while_plugged_in') | Out-String).Trim()
try {
    Invoke-Adb @('shell','svc','power','stayon','true') | Out-Null
$index = 0
foreach ($variant in $variants) {
    $index++
    if ($index -lt $FirstRun -or $index -gt $LastRun) { continue }
    $apk = Join-Path $repo "Builds/HudRc/Xiaomi10/$variant.apk"
    if (-not (Test-Path -LiteralPath $apk)) { throw ("Missing APK: " + $apk) }
    Write-Output "[$index/4] Installing $variant"
    Invoke-Adb @('shell','am','force-stop',$package) | Out-Null
    if (-not ($SkipFirstInstall -and $index -eq $FirstRun)) {
        Invoke-Adb @('install','-r',$apk) | Out-Null
    }
    $before = @(Get-Reports)
    $batteryBefore = @(Invoke-Adb @('shell','dumpsys','battery') | Where-Object { "$_" -match 'level:|temperature:|status:' })
    $thermalBefore = @(Invoke-Adb @('shell','dumpsys','thermalservice') | Where-Object { "$_" -match 'Thermal Status:|mStatus=' })
    Invoke-Adb @('shell','input','keyevent','KEYCODE_WAKEUP') | Out-Null
    $started = [DateTime]::UtcNow
    Invoke-Adb @('shell','am','start','-n',"$package/com.unity3d.player.UnityPlayerActivity") | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(300)
    $newReports = @()
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds 3
        $newReports = @(Get-Reports | Where-Object { $_ -notin $before })
        if ($newReports.Count -gt 0) { break }
    }
    if ($newReports.Count -ne 1) { throw ("Expected one completed report for " + $variant + "; found " + $newReports.Count) }
    Start-Sleep -Seconds 2
    $remoteJson = $newReports[0]
    $localName = '{0:D2}-{1}' -f $index, $variant
    $localJson = Join-Path $outputDirectory ($localName + '.json')
    $localCsv = Join-Path $outputDirectory ($localName + '.csv')
    Invoke-Adb @('pull',$remoteJson,$localJson) | Out-Null
    Invoke-Adb @('pull',($remoteJson -replace '\.json$','.csv'),$localCsv) | Out-Null
    $report = Get-Content -LiteralPath $localJson -Raw | ConvertFrom-Json
    if ($report.configuration.holderCount -ne 128 -or $report.configuration.spriteBacked -or $report.configuration.forceUguiFallback -or $report.preparedSpriteImages -ne 0 -or $report.preparedFastPathImages -ne 3) { throw '128-holder baseline configuration mismatch' }
    $expectedSlim = $true
    $expectedProfile = $false
    $expectedStages = $false
    $expectedBatch = $variant -eq 'query-batch'
    if ($report.configuration.useGenerationBatch -ne $expectedBatch) { throw 'Generation batch configuration mismatch' }
    if ($report.configuration.disableBuildMeshDiagnostics -eq $expectedStages) { throw 'Stage configuration mismatch' }
    if ($report.schema -ne 'hud-rc-v2' -or $report.configuration.useSlim -ne $expectedSlim -or $report.configuration.profileQuadWrites -ne $expectedProfile) {
        throw ('Wrong report configuration: ' + $localJson)
    }
    if ($report.configuration.sampleFrames -ne 3000 -or $report.configuration.targetFrameRate -ne 60 -or $report.frames.Count -ne 9000) {
        throw ('Unexpected workload/sample count: ' + $localJson)
    }
    foreach ($sample in $report.frames) {
        $expectedBatches = if ($expectedBatch -and $sample.updatedHolders -gt 0) { 3 } else { 0 }
        if ($sample.generationBatchCount -ne $expectedBatches) { throw 'Actual generation batch count mismatch' }
        if ($sample.buildMeshStagesAvailable -ne $expectedStages) { throw 'Stage availability mismatch' }
        if (-not $expectedStages -and ($sample.buildMeshDoGenerateMs -ne -1 -or $sample.buildMeshMs -ne -1)) { throw 'Stage sentinel mismatch' }
        $available = $expectedProfile -and $sample.updatedHolders -gt 0
        if ($sample.buildMeshWriteAvailable -ne $available) { throw 'Write availability mismatch' }
        $expectedWrites = if ($expectedProfile) { $sample.updatedHolders * $report.configuration.quadsPerHolder } else { 0 }
        if ($sample.buildMeshWriteSamples -ne $expectedWrites) { throw 'Write count mismatch' }
        foreach ($field in @('buildMeshWriteStorageMs','buildMeshWriteVertexMs','buildMeshWriteUvMs','buildMeshWriteColorMs','buildMeshWriteIndexMs')) {
            if (($available -and $sample.$field -lt 0) -or (-not $available -and $sample.$field -ne -1)) { throw 'Write timing sentinel mismatch' }
        }
    }
    $batteryAfter = @(Invoke-Adb @('shell','dumpsys','battery') | Where-Object { "$_" -match 'level:|temperature:|status:' })
    $thermalAfter = @(Invoke-Adb @('shell','dumpsys','thermalservice') | Where-Object { "$_" -match 'Thermal Status:|mStatus=' })
    Invoke-Adb @('shell','am','force-stop',$package) | Out-Null
    $metadata = [ordered]@{
        variant=$variant; order=$index; device=$Device; startedUtc=$started.ToString('o')
        apk=(Split-Path $apk -Leaf); apkSha256=(Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash
        originalReport=$remoteJson; reportSha256=(Get-FileHash -LiteralPath $localJson -Algorithm SHA256).Hash
        sourceFingerprint=$report.sourceFingerprint; batteryBefore=$batteryBefore; batteryAfter=$batteryAfter
        thermalBefore=$thermalBefore; thermalAfter=$thermalAfter
    }
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputDirectory ($localName + '-metadata.json')) -Encoding utf8
    Write-Output "[$index/4] Verified $localJson"
    Start-Sleep -Seconds 10
}
Write-Output "Completed requested runs ${FirstRun}-${LastRun}: $outputDirectory"

} finally {
    Invoke-Adb @('shell','am','force-stop',$package) | Out-Null
    Invoke-Adb @('shell','settings','put','global','stay_on_while_plugged_in',$originalStayAwake) | Out-Null
}
