[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReportPath,
    [Parameter(Mandatory)][string]$BuildMetadataPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$probe = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
$build = Get-Content -LiteralPath $BuildMetadataPath -Raw | ConvertFrom-Json
if ($probe.schema -ne 'damage-frame-timing-calibration-v2' -or $probe.formal) { throw 'Expected a non-formal v2 calibration.' }
foreach ($name in @('sourceFingerprint','commit','buildGuid','applicationIdentifier')) {
    if ($probe.$name -ne $build.$name) { throw "Calibration/build identity mismatch: $name" }
}
if (-not $probe.featureEnabled -or -not $build.frameTimingProbe -or -not $build.frameTimingStatsEnabled -or
    $build.backend -ne 'IL2CPP' -or $build.architecture -ne 'ARM64' -or $build.api -ne 'Vulkan' -or $build.buildType -ne 'Release') { throw 'Expected the registered Release/IL2CPP/ARM64/Vulkan timing probe.' }
$frames = @($probe.frames)
if ($frames.Count -ne $probe.frameCount -or $frames.Count -ne 640) { throw 'Incomplete calibration sequence.' }
$cpuFrequency = [decimal]$probe.cpuTimerFrequency; $stopwatchFrequency = [decimal]$probe.stopwatchFrequency
if ($cpuFrequency -le 0 -or $stopwatchFrequency -le 0) { throw 'Missing CPU clock frequencies.' }
$clocks = [decimal[]]::new($frames.Count)
$cameraErrors = @()
for ($i=0; $i -lt $frames.Count; $i++) {
    $frame=$frames[$i]; $clocks[$i]=[decimal]$frame.stopwatchTicks*$cpuFrequency/$stopwatchFrequency
    if ($frame.relativeFrame -ne $i -or ($i -gt 0 -and ($frame.unityFrame -ne $frames[$i-1].unityFrame+1 -or $clocks[$i] -le $clocks[$i-1]))) { throw 'Nonsequential engine frames/clocks.' }
    $sleep=if($i -eq 80){80}elseif($i -eq 168){130}elseif($i -eq 328){100}elseif($i -eq 520){90}else{0}
    $repeats=if(($i -ge 120 -and $i -le 122) -or ($i -ge 240 -and $i -le 242) -or ($i -ge 360 -and $i -le 362)){64}
        elseif($i -in @(96,384)){16}elseif($i -in @(192,480)){32}elseif($i -in @(288,576)){64}else{0}
    if ($frame.cpuSleepMs -ne $sleep -or $frame.requestedDrawRepeats -ne $repeats) { throw 'Perturbation schedule differs from registration.' }
    if ($frame.cameraPasses -ne 1 -or $frame.preCullUnityFrame -ne $frame.unityFrame -or $frame.postRenderUnityFrame -ne $frame.unityFrame -or
        $frame.renderedDrawRepeats -ne $frame.requestedDrawRepeats -or [decimal]$frame.preCullTicks -lt [decimal]$frame.stopwatchTicks -or
        [decimal]$frame.postRenderTicks -lt [decimal]$frame.preCullTicks) { $cameraErrors += $frame.unityFrame }
}
function Find-NearestUpdate([decimal]$Timestamp) {
    $low=0; $high=$clocks.Length
    while ($low -lt $high) { $middle=[int][Math]::Floor(($low+$high)/2); if ($clocks[$middle] -lt $Timestamp) { $low=$middle+1 } else { $high=$middle } }
    $index=[Math]::Min($low,$clocks.Length-1)
    if ($index -gt 0 -and [Math]::Abs($clocks[$index-1]-$Timestamp) -lt [Math]::Abs($clocks[$index]-$Timestamp)) { $index-- }
    return $index
}
$byTimestamp=@{}
foreach ($timing in $probe.timings) {
    if ($timing.frameStartTimestamp -eq '0000000000000000') { continue }
    if (-not $byTimestamp.ContainsKey($timing.frameStartTimestamp)) { $byTimestamp[$timing.frameStartTimestamp]=[Collections.Generic.List[object]]::new() }
    $byTimestamp[$timing.frameStartTimestamp].Add($timing)
}
$rows=@(); $byFrame=@{}; $duplicateFrames=@()
foreach ($key in @($byTimestamp.Keys | Sort-Object)) {
    $observations=$byTimestamp[$key]; $first=$observations[0]; $last=$observations[$observations.Count-1]
    $stamp=[decimal][Convert]::ToUInt64($key,16); $index=Find-NearestUpdate $stamp; $frame=$frames[$index]
    $errorMs=[double](($clocks[$index]-$stamp)*1000/$cpuFrequency)
    $stable=$observations.Count -ge 2 -and [Math]::Abs([double]$last.gpuFrameMsRaw-[double]$observations[$observations.Count-2].gpuFrameMsRaw) -lt 0.000001
    $row=[ordered]@{relativeFrame=$index;unityFrame=$frame.unityFrame;nativeTimestamp=$key;clockErrorMs=$errorMs
        observations=$observations.Count;firstObserved=$first.observedAtUnityFrame;lastObserved=$last.observedAtUnityFrame
        firstGpuMs=$first.gpuFrameMsRaw;lastGpuMs=$last.gpuFrameMsRaw;lateRecovery=(-not $first.gpuTimeValid -and $last.gpuTimeValid)
        gpuChanged=@($observations.gpuFrameMsRaw | Sort-Object -Unique).Count -gt 1;lastGpuValid=$last.gpuTimeValid;lastGpuStable=$stable
        cpuMainMs=$last.cpuMainThreadMs;cpuRenderMs=$last.cpuRenderThreadMs;cpuFrameMs=$last.cpuFrameMs
        cpuSleepMs=$frame.cpuSleepMs;renderedDrawRepeats=$frame.renderedDrawRepeats}
    $rows += $row
    if ($byFrame.ContainsKey($index)) {
        if ($index -ge $probe.warmupFrames -and $index -lt $probe.drainStart) { $duplicateFrames += $index }
    } else { $byFrame[$index]=$row }
}
$required=@(); $missing=@(); $invalid=@(); $unstable=@(); $clockErrors=@()
for ($i=[int]$probe.warmupFrames; $i -lt [int]$probe.drainStart; $i++) {
    if (-not $byFrame.ContainsKey($i)) { $missing += $i; continue }
    $row=$byFrame[$i]; $required += $row
    if (-not $row.lastGpuValid) { $invalid += $i }
    if (-not $row.lastGpuStable) { $unstable += $i }
    if ([Math]::Abs($row.clockErrorMs) -gt 2) { $clockErrors += $i }
}
$baseline=@($required | Where-Object { $_.renderedDrawRepeats -eq 0 -and $_.lastGpuValid } | ForEach-Object { [double]$_.lastGpuMs } | Sort-Object)
if (-not $baseline.Count) { throw 'No positive baseline GPU readings.' }
$baselineMedian=$baseline[[int][Math]::Floor($baseline.Count/2)]
$baselineP95=$baseline[[int][Math]::Ceiling($baseline.Count*.95)-1]
$pulseThreshold=$baselineP95*1.5
$fits=@()
foreach ($offset in -2..2) {
    $n=0; $sx=0.0; $sy=0.0; $sxx=0.0; $syy=0.0; $sxy=0.0; $falsePositive=0; $falseNegative=0
    foreach ($row in $required) {
        $attributed=$row.relativeFrame+$offset
        if (-not $row.lastGpuValid -or $attributed -lt $probe.warmupFrames -or $attributed -ge $probe.drainStart) { continue }
        $x=[double]$frames[$attributed].renderedDrawRepeats; $y=[double]$row.lastGpuMs
        $n++; $sx+=$x; $sy+=$y; $sxx+=$x*$x; $syy+=$y*$y; $sxy+=$x*$y
        $elevated=$y -gt $pulseThreshold
        if ($x -gt 0 -and -not $elevated) { $falseNegative++ }
        if ($x -eq 0 -and $elevated) { $falsePositive++ }
    }
    $cov=$n*$sxy-$sx*$sy; $vx=$n*$sxx-$sx*$sx; $vy=$n*$syy-$sy*$sy
    $fits += [ordered]@{gpuFrameOffset=$offset;n=$n;slope=if($vx -gt 0){$cov/$vx}else{0};rSquared=if($vx -gt 0 -and $vy -gt 0){$cov*$cov/($vx*$vy)}else{0};falsePositive=$falsePositive;falseNegative=$falseNegative}
}
$cpuChecks=@()
foreach ($frame in @($frames | Where-Object cpuSleepMs -gt 0)) {
    $row=$byFrame[[int]$frame.relativeFrame]
    $cpuChecks += [ordered]@{relativeFrame=$frame.relativeFrame;sleepMs=$frame.cpuSleepMs;cpuMainMs=if($null -ne $row){$row.cpuMainMs}else{$null}
        gpuMs=if($null -ne $row){$row.lastGpuMs}else{$null};passed=($null -ne $row -and $row.cpuMainMs -ge $frame.cpuSleepMs*.9 -and $row.lastGpuValid -and $row.lastGpuMs -lt $frame.cpuSleepMs*.25)}
}
$best=@($fits | Sort-Object { $_.rSquared } -Descending)[0]
$consistentOffsets=@($fits | Where-Object { $_.rSquared -ge .9 -and $_.slope -gt 0 -and $_.falseNegative -eq 0 -and $_.falsePositive -eq 0 })
$complete=$missing.Count -eq 0 -and $invalid.Count -eq 0 -and $unstable.Count -eq 0 -and $clockErrors.Count -eq 0 -and $duplicateFrames.Count -eq 0 -and $cameraErrors.Count -eq 0
$candidate=$complete -and $cpuChecks.Count -eq 4 -and @($cpuChecks | Where-Object { -not $_.passed }).Count -eq 0 -and $consistentOffsets.Count -eq 1
$analysis=[ordered]@{schema='damage-gpu-calibration-analysis-v2';reportSHA256=(Get-FileHash -LiteralPath $ReportPath).Hash
    buildMetadataSHA256=(Get-FileHash -LiteralPath $BuildMetadataPath).Hash;analyzerSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash
    sourceFingerprint=$probe.sourceFingerprint;buildGuid=$probe.buildGuid;scheme=$probe.scheme;scope='Unity whole-frame elapsed GPU time, not HUD-only; diagnostic perturbations, not performance ranking'
    frameBindingCandidate=$candidate;formalRankingEligible=$false;requiredFrames=([int]$probe.drainStart-[int]$probe.warmupFrames)
    mappedRequiredFrames=$required.Count;totalObservations=@($probe.timings).Count;uniqueTimestamps=$byTimestamp.Count
    lateRecoveries=@($rows | Where-Object lateRecovery).Count;revisedGpuValues=@($rows | Where-Object gpuChanged).Count
    complete=$complete;missing=$missing;invalidGpu=$invalid;unstableGpu=$unstable;clockErrors=$clockErrors;duplicateFrameMappings=$duplicateFrames;cameraErrors=$cameraErrors
    baselineGpuMedianMs=$baselineMedian;baselineGpuP95Ms=$baselineP95;pulseThresholdMs=$pulseThreshold
    cpuPerturbationChecks=$cpuChecks;offsetFits=$fits;consistentOffsets=$consistentOffsets;bestFit=$best;nativeFrames=$rows
    interpretation='Candidate requires complete/stable values and camera mapping, CPU/GPU separation, and a unique workload offset with zero pulse classification errors. R-squared and load threshold describe this diagnostic, not a product performance budget.'}
$directory=Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
$analysis | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Frame-binding candidate: $candidate; complete: $complete; recovered: $($analysis.lateRecoveries); missing/invalid: $($missing.Count)/$($invalid.Count); best offset: $($best.gpuFrameOffset), R2=$($best.rSquared)"
