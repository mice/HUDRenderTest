[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReportPath,
    [Parameter(Mandatory)][string]$BuildMetadataPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$probe=Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
$build=Get-Content -LiteralPath $BuildMetadataPath -Raw | ConvertFrom-Json
if($probe.schema -ne 'damage-frame-timing-calibration-v3' -or $probe.formal){throw 'Expected non-formal native v3 calibration.'}
foreach($key in @('commit','sourceFingerprint','buildGuid','applicationIdentifier','nativePluginSHA256')){
    if($probe.$key -ne $build.$key){throw "Native calibration identity mismatch: $key"}
}
if(-not $build.nativeVulkanProbe -or -not $build.frameTimingProbe -or $build.backend -ne 'IL2CPP' -or
    $build.architecture -ne 'ARM64' -or $build.api -ne 'Vulkan' -or $build.buildType -ne 'Release' -or
    $probe.api -ne 'Vulkan' -or $probe.backend -ne 'IL2CPP' -or $probe.width -ne 2340 -or $probe.height -ne 1080 -or
    $probe.nativePluginSHA256 -notmatch '^[0-9A-Fa-f]{64}$'){throw 'Unexpected native probe build or viewport.'}
$frames=@($probe.frames); $native=@($probe.nativeTimings); $info=$probe.nativeInfo
if($frames.Count -ne 640 -or $native.Count -ne 640 -or $probe.frameCount -ne 640 -or $probe.warmupFrames -ne 64 -or $probe.drainStart -ne 624){throw 'Incomplete or unregistered native sequence.'}
if($info.status -ne 1 -or $info.capacity -ne 640 -or $info.timestampPeriodNs -le 0 -or
    $info.timestampValidBits -lt 1 -or $info.timestampValidBits -gt 64){throw 'Native timestamp capability/configuration failed.'}
$cpuFrequency=[decimal]$probe.cpuTimerFrequency; $stopwatchFrequency=[decimal]$probe.stopwatchFrequency
if($cpuFrequency -le 0 -or $stopwatchFrequency -le 0){throw 'Missing CPU clock frequencies.'}
$clocks=[decimal[]]::new(640)
$errors=[Collections.Generic.List[object]]::new()
$rows=@()
for($i=0;$i -lt 640;$i++){
    $f=$frames[$i]; $n=$native[$i]
    $clocks[$i]=[decimal]$f.stopwatchTicks*$cpuFrequency/$stopwatchFrequency
    $sleep=if($i -eq 80){80}elseif($i -eq 168){130}elseif($i -eq 328){100}elseif($i -eq 520){90}else{0}
    $load=if(($i -ge 120 -and $i -le 122) -or ($i -ge 240 -and $i -le 242) -or ($i -ge 360 -and $i -le 362)){64}
        elseif($i -in @(96,384)){16}elseif($i -in @(192,480)){32}elseif($i -in @(288,576)){64}else{0}
    if($f.relativeFrame -ne $i -or $f.cpuSleepMs -ne $sleep -or $f.requestedDrawRepeats -ne $load -or
        ($i -gt 0 -and ($f.unityFrame -ne $frames[$i-1].unityFrame+1 -or $clocks[$i] -le $clocks[$i-1]))){throw 'Engine frames or registered perturbations changed.'}
    if($i -lt 64 -or $i -ge 624){continue}
    $reasons=@()
    if($f.cameraPasses -ne 1 -or $f.preCullUnityFrame -ne $f.unityFrame -or $f.postRenderUnityFrame -ne $f.unityFrame -or
        $f.renderedDrawRepeats -ne $load -or [decimal]$f.preCullTicks -lt [decimal]$f.stopwatchTicks -or
        [decimal]$f.postRenderTicks -lt [decimal]$f.preCullTicks){$reasons+='camera'}
    if($n.index -ne $i -or $n.unityFrame -ne $f.unityFrame -or $n.beginCalls -ne 1 -or $n.endCalls -ne 1 -or
        $n.beginNativeFrame -ne $n.endNativeFrame -or ($i -gt 64 -and [decimal]$n.beginNativeFrame -le [decimal]$native[$i-1].endNativeFrame)){$reasons+='command-binding'}
    if($n.available -ne 1 -or $n.status -ne 2 -or $n.milliseconds -le 0 -or [double]::IsInfinity($n.milliseconds) -or [double]::IsNaN($n.milliseconds)){$reasons+='query-unavailable'}
    if([decimal]$n.readSafeFrame -lt [decimal]$n.endNativeFrame -or [decimal]$n.readNativeFrame -lt [decimal]$n.readSafeFrame){$reasons+='read-before-fence'}
    $begin=[decimal][Convert]::ToUInt64($n.beginTimestamp,16); $end=[decimal][Convert]::ToUInt64($n.endTimestamp,16)
    $modulus=[decimal]1
    for($bit=0;$bit -lt $info.timestampValidBits;$bit++){$modulus*=2}
    if($begin -ge $modulus -or $end -ge $modulus){$reasons+='timestamp-bits'}
    $delta=($end-$begin+$modulus)%$modulus
    $expected=[double]($delta*[decimal]$info.timestampPeriodNs/1000000)
    if([Math]::Abs($expected-$n.milliseconds) -gt .000001){$reasons+='timestamp-scale'}
    if($reasons.Count){$errors.Add(@{relativeFrame=$i;reasons=$reasons})}
    $rows+=@{relativeFrame=$i;unityFrame=$f.unityFrame;gpuMs=$n.milliseconds;drawRepeats=$load;cpuSleepMs=$sleep;valid=$reasons.Count -eq 0}
}
# CPU timestamps are only used to verify the four injected sleeps, never as GPU time.
$cpuByFrame=@{}
$latest=@{}
foreach($observation in $probe.timings){$latest[$observation.frameStartTimestamp]=$observation}
foreach($timing in $latest.Values){
    if($timing.frameStartTimestamp -eq '0000000000000000'){continue}
    $stamp=[decimal][Convert]::ToUInt64($timing.frameStartTimestamp,16)
    $best=-1; $distance=[decimal]::MaxValue
    foreach($at in @(80,168,328,520)){
        $candidate=[Math]::Abs($stamp-$clocks[$at]); if($candidate -lt $distance){$best=$at;$distance=$candidate}
    }
    if($distance*1000/$cpuFrequency -le 2){
        if($cpuByFrame.ContainsKey($best)){throw 'Ambiguous CPU perturbation clock mapping.'}
        $cpuByFrame[$best]=$timing
    }
}
$cpuChecks=@($rows | Where-Object cpuSleepMs -gt 0 | ForEach-Object {
    $cpu=$cpuByFrame[$_.relativeFrame]
    @{relativeFrame=$_.relativeFrame;sleepMs=$_.cpuSleepMs;cpuMainMs=if($null -ne $cpu){$cpu.cpuMainThreadMs}else{$null};gpuMs=$_.gpuMs
        passed=$_.valid -and $null -ne $cpu -and $cpu.cpuMainThreadMs -ge $_.cpuSleepMs*.9 -and $_.gpuMs -lt $_.cpuSleepMs*.25}
})
$baseline=@($rows | Where-Object { $_.drawRepeats -eq 0 -and $_.valid } | ForEach-Object gpuMs | Sort-Object)
if(-not $baseline.Count){throw 'No valid native GPU baseline.'}
$p95=$baseline[[int][Math]::Ceiling($baseline.Count*.95)-1]; $threshold=$p95*1.5
$n=0;$sx=0.0;$sy=0.0;$sxx=0.0;$syy=0.0;$sxy=0.0;$falsePositive=0;$falseNegative=0
foreach($row in $rows){
    if(-not $row.valid){continue}
    $x=[double]$row.drawRepeats;$y=[double]$row.gpuMs
    $n++;$sx+=$x;$sy+=$y;$sxx+=$x*$x;$syy+=$y*$y;$sxy+=$x*$y
    if($x -gt 0 -and $y -le $threshold){$falseNegative++}
    if($x -eq 0 -and $y -gt $threshold){$falsePositive++}
}
$cov=$n*$sxy-$sx*$sy;$vx=$n*$sxx-$sx*$sx;$vy=$n*$syy-$sy*$sy
$rSquared=if($vx -gt 0 -and $vy -gt 0){$cov*$cov/($vx*$vy)}else{0}
$slope=if($vx -gt 0){$cov/$vx}else{0}
$complete=$errors.Count -eq 0 -and $rows.Count -eq 560
$candidate=$complete -and $cpuChecks.Count -eq 4 -and @($cpuChecks | Where-Object {-not $_.passed}).Count -eq 0 -and
    $rSquared -ge .9 -and $slope -gt 0 -and $falsePositive -eq 0 -and $falseNegative -eq 0
$result=[ordered]@{schema='damage-native-gpu-calibration-analysis-v1';frameBindingCandidate=$candidate;formalRankingEligible=$false
    scope=$probe.nativeScope;sourceFingerprint=$probe.sourceFingerprint;buildGuid=$probe.buildGuid;scheme=$probe.scheme;nativePluginSHA256=$probe.nativePluginSHA256
    reportSHA256=(Get-FileHash -LiteralPath $ReportPath).Hash;buildMetadataSHA256=(Get-FileHash -LiteralPath $BuildMetadataPath).Hash
    analyzerSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;nativeInfo=$info;requiredFrames=560;complete=$complete;errors=@($errors.ToArray())
    baselineMedianMs=$baseline[[int][Math]::Floor($baseline.Count/2)];baselineP95Ms=$p95;pulseThresholdMs=$threshold
    cpuPerturbationChecks=$cpuChecks;zeroOffsetFit=@{n=$n;rSquared=$rSquared;slope=$slope;falsePositive=$falsePositive;falseNegative=$falseNegative}
    frames=$rows;interpretation='Direct command-token binding; no fitted frame offset. Pass-boundary overhead is included. Diagnostic calibration does not establish formal performance ranking.'}
$directory=Split-Path -Parent $OutputPath
if($directory){New-Item -ItemType Directory -Path $directory -Force | Out-Null}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Native binding candidate: $candidate; complete: $complete; R2=$rSquared; errors=$($errors.Count); pulse errors=$falsePositive/$falseNegative"
