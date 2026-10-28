[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$ReportPaths,
    [string]$OutputPath = 'TestResults/DamageTextComparison/analysis.json',
    [string]$VisualEvidencePath,
    [string]$NativeGpuCalibrationPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$invariant = [Globalization.CultureInfo]::InvariantCulture
. (Join-Path $PSScriptRoot 'NativeGpuTiming/Validate-NativeGpuReport.ps1')
if (-not ('DamageBenchmarkOfflineHash' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
public static class DamageBenchmarkOfflineHash {
    public static long Compute(uint id, uint amount, uint kind, uint seed, float spawn, float x, float y, float z) {
        unchecked {
            ulong h = 14695981039346656037UL;
            foreach (uint v in new uint[] { id, amount, kind, seed, Bits(spawn), Bits(x), Bits(y), Bits(z) }) h = (h ^ v) * 1099511628211UL;
            return (long)h;
        }
    }
    private static uint Bits(float value) { return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0); }
    public static long Xor(long a, long b) { return a ^ b; }
}
'@
}

function Get-RequestHash($Events) {
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::UTF8, $true)
    try {
        $writer.Write('damage-benchmark-events-v1'); $writer.Write([int]$Events.Count)
        foreach ($event in $Events) {
            $request = $event.request
            $writer.Write([uint32]$request.EventId); $writer.Write([uint32]$request.Amount); $writer.Write([byte]$request.Kind)
            $writer.Write([single]$request.WorldPosition.x); $writer.Write([single]$request.WorldPosition.y); $writer.Write([single]$request.WorldPosition.z)
            $writer.Write([single]$request.SpawnTime); $writer.Write([uint32]$request.RandomSeed)
        }
        $writer.Flush(); $sha = [Security.Cryptography.SHA256]::Create()
        try { return [BitConverter]::ToString($sha.ComputeHash($stream.ToArray())).Replace('-', '') } finally { $sha.Dispose() }
    }
    finally { $writer.Dispose(); $stream.Dispose() }
}

function Get-Distribution($Values) {
    $sorted = @($Values | Where-Object { $null -ne $_ -and [double]$_ -ge 0 -and -not [double]::IsNaN([double]$_) -and -not [double]::IsInfinity([double]$_) } | Sort-Object { [double]$_ })
    if ($sorted.Count -eq 0) { return [ordered]@{ available=$false; n=0; median=$null; p95=$null; p99=$null; max=$null } }
    $n = $sorted.Count
    $median = if ($n % 2) { [double]$sorted[[int][Math]::Floor($n / 2)] } else { ([double]$sorted[$n / 2 - 1] + [double]$sorted[$n / 2]) / 2 }
    return [ordered]@{ available=$true; n=$n; median=$median; p95=[double]$sorted[[int][Math]::Ceiling($n * .95)-1]; p99=[double]$sorted[[int][Math]::Ceiling($n * .99)-1]; max=[double]$sorted[$n-1] }
}

function Get-TimesHash($Bits) {
    $stream = [IO.MemoryStream]::new(); $writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::UTF8, $true)
    try {
        $writer.Write('damage-times-v1')
        foreach ($value in $Bits) { $writer.Write([long]::Parse([string]$value, [Globalization.NumberStyles]::HexNumber, $invariant)) }; $writer.Flush()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return [BitConverter]::ToString($sha.ComputeHash($stream.ToArray())).Replace('-', '') } finally { $sha.Dispose() }
    }
    finally { $writer.Dispose(); $stream.Dispose() }
}

$runs = @()
foreach ($reportPath in $ReportPaths) {
    $errors = [Collections.Generic.List[string]]::new()
    $entry = [ordered]@{ path=$reportPath; valid=$false; errors=@(); summaries=$null; burstWindows=@() }
    try {
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if($report.gpuStatus -eq 'validated-native-timestamps') {
            $native=Test-DamageNativeGpuReport $report $NativeGpuCalibrationPath
            foreach($error in $native.errors){$errors.Add('native-gpu:'+$error)}
        } elseif($report.gpuStatus -ne 'unavailable') {$errors.Add('invalid-gpu-status')}
        if ($report.schema -ne 'damage-text-benchmark-v1') { throw 'Unsupported report schema.' }
        $stem = $reportPath -replace '\.report\.json$', ''
        if ($stem -eq $reportPath) { throw 'Expected *.report.json artifact name.' }
        $manifest = Get-Content -LiteralPath ($stem + '.manifest.json') -Raw | ConvertFrom-Json
        if ($manifest.schema -ne 'damage-benchmark-artifacts-v1') { $errors.Add('artifact-schema') }
        if ((Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash -ne $manifest.reportSHA256) { $errors.Add('report-hash') }
        if ((Get-FileHash -LiteralPath ($stem + '.frames.csv') -Algorithm SHA256).Hash -ne $manifest.csvSHA256) { $errors.Add('csv-hash') }
        $csv = @(Import-Csv -LiteralPath ($stem + '.frames.csv'))
        foreach ($field in @('sourceFingerprint','clipHash','atlasHash','styleHash','configurationHash','cameraHash','planHash','workloadHash','timesHash','expectedRequestHash','consumedRequestHash')) {
            if ([string]$report.$field -notmatch '\A[0-9A-Fa-f]{64}\z') { $errors.Add('missing-metadata:' + $field) }
        }
        if ([string]$report.commit -notmatch '\A[0-9A-Fa-f]{40}\z') { $errors.Add('missing-metadata:commit') }
        foreach ($field in @('sourceStatus','packageVersion','unityVersion','device','operatingSystem','graphicsApi','graphicsDevice','backend','executionMode','clockPolicy','colorSpace','buildType')) {
            if ([string]::IsNullOrWhiteSpace([string]$report.$field) -or $report.$field -eq 'unknown') { $errors.Add('missing-metadata:' + $field) }
        }
        if ($report.executionMode -eq 'Player' -and [string]::IsNullOrWhiteSpace($report.buildGuid)) { $errors.Add('missing-metadata:buildGuid') }
        if ($report.aborted) { $errors.Add('aborted:' + $report.reason) }
        if (-not $report.formal -or $report.controlsVisible -or [int]$report.plan.Mode -eq 0) { $errors.Add('diagnostic-or-visual-segment') }
        if ($report.requestedFormat -ne $report.effectiveFormat) { $errors.Add('format-fallback-not-requested-experiment') }
        if ($report.scheme -notin @('A-SharedQuad-Float32','B-PersistentMesh-Float32','B-PersistentMesh-Float16')) { $errors.Add('unsupported-scheme') }
        $instanced = $report.scheme -eq 'A-SharedQuad-Float32'; $half = $report.effectiveFormat -eq 'Float16'
        $expectedStride = if ($instanced) { 12 } elseif ($half) { 64 } else { 68 }
        $positionDimension = if ($instanced) { 3 } else { 2 }; $positionFormat = if ($half) { 'Float16' } else { 'Float32' }
        if ($report.effectiveFormat -notin @('Float32','Float16') -or ($instanced -and $half)) { $errors.Add('invalid-effective-format') }
        if (-not $instanced -and $report.scheme -ne ('B-PersistentMesh-' + $report.effectiveFormat)) { $errors.Add('scheme-format-mismatch') }
        if ($report.shaderPassCount -ne 1) { $errors.Add('shader-pass-count') }
        $shaderName = if ($instanced) { 'UIData/DamageTextInstanced' } else { 'UIData/DamageText' }
        if ($report.shaderName -ne $shaderName) { $errors.Add('shader-identity') }
        if (-not ([string]$report.vertexLayout).StartsWith("stride=$expectedStride|Position:${positionFormat}x${positionDimension}@0#0")) { $errors.Add('vertex-layout') }
        if (-not $instanced) {
            $shift = if ($half) { 4 } else { 0 }
            foreach ($tag in @("Color:UNorm8x4@$((8-$shift))#0","TexCoord0:Float32x2@$((12-$shift))#0","TexCoord1:Float32x4@$((20-$shift))#0","TexCoord2:Float32x4@$((36-$shift))#0","TexCoord3:Float32x4@$((52-$shift))#0")) {
                if (-not ([string]$report.vertexLayout).Contains($tag)) { $errors.Add('vertex-layout') }
            }
        }
        if ($half -and (-not $report.precision.candidatePassed -or $report.precision.maximumPixelError -gt .25 -or $report.precision.candidateThreshold -ne .25)) { $errors.Add('fp16-precision-gate') }
        if ($report.expectedEvents -le 0) { $errors.Add('empty-request-interval') }
        if (-not $report.completeEventInterval -or $report.consumedEvents -ne $report.expectedEvents -or @($report.events).Count -ne $report.expectedEvents) { $errors.Add('incomplete-event-interval') }
        if ($report.expectedRequestHash -ne $report.consumedRequestHash -or (Get-RequestHash @($report.events)) -ne $report.consumedRequestHash) { $errors.Add('request-sequence') }
        if ($report.timeEncoding -ne 'ieee754-f64-hex-v1' -or @($report.registeredTimeBits).Count -ne @($report.registeredTimes).Count) { $errors.Add('registered-time-encoding') }
        if ((Get-TimesHash @($report.registeredTimeBits)) -ne $report.timesHash) { $errors.Add('registered-times-hash') }
        $registeredTimes = [double[]]::new(@($report.registeredTimeBits).Count)
        for ($i=0; $i -lt @($report.registeredTimeBits).Count; $i++) {
            $exact = [BitConverter]::Int64BitsToDouble([long]::Parse($report.registeredTimeBits[$i], [Globalization.NumberStyles]::HexNumber, $invariant))
            $registeredTimes[$i] = $exact
            if ([double]::IsNaN($exact) -or [double]::IsInfinity($exact) -or $exact -lt 0) { $errors.Add('invalid-registered-time') }
            if ([Math]::Abs($exact - [double]$report.registeredTimes[$i]) -gt 1e-6 * [Math]::Max(1,[Math]::Abs($exact))) { $errors.Add('registered-time-numeric-mirror') }
        }
        $ids = [Collections.Generic.HashSet[uint32]]::new()
        $stateHashes = [Collections.Generic.Dictionary[uint32,long]]::new()
        $previousSpawn = -1.0; $previousId = 0
        foreach ($event in $report.events) {
            $request = $event.request
            $stateHashes[[uint32]$request.EventId] = [DamageBenchmarkOfflineHash]::Compute([uint32]$request.EventId,[uint32]$request.Amount,[uint32]$request.Kind,
                [uint32]$request.RandomSeed,[single]$request.SpawnTime,[single]$request.WorldPosition.x,[single]$request.WorldPosition.y,[single]$request.WorldPosition.z)
            if (-not $ids.Add([uint32]$request.EventId)) { $errors.Add('duplicate-request') }
            if ($request.SpawnTime -lt $previousSpawn -or ($request.SpawnTime -eq $previousSpawn -and $request.EventId -le $previousId)) { $errors.Add('request-order') }
            $previousSpawn = $request.SpawnTime; $previousId = $request.EventId
            if ([int]$event.result -ne 0) { $errors.Add('rejected-request') }
            if ($event.consumedAt -lt $request.SpawnTime) { $errors.Add('early-consumption') }
            if ($event.missed -or $event.firstPublishedAt -lt 0) { $errors.Add('missed-presentation') }
            if ($event.firstPublishedAt -ge 0 -and $event.firstPublishedAt -lt $event.consumedAt) { $errors.Add('invalid-first-publication') }
            if ($event.endTime -le $request.SpawnTime) { $errors.Add('invalid-lifetime') }
            if ([int]$report.plan.Phase -eq 1 -and $event.observedEndedAt -lt $event.endTime) { $errors.Add('unobserved-end') }
        }
        $sampling = @($report.frames | Where-Object { [int]$_.stage -eq 3 })
        if ($sampling.Count -lt 3000 -or $sampling.Count -ne $report.samples) { $errors.Add('insufficient-or-inconsistent-samples') }
        if ([int]$report.plan.Mode -eq 1 -and @($report.registeredTimes).Count -ne $sampling.Count) { $errors.Add('registered-times-count') }
        if ($csv.Count -ne @($report.frames).Count) { $errors.Add('csv-row-count') }
        $previousTime = -1.0; $previousConsumed = 0; $previousRebuilds = 0; $sampleIndex = 0
        $birthCursor = 0; $endCursor = 0; $oracleEvents = 0; $oracleCharacters = 0; [long]$oracleHash = 0
        $endOrder = @($report.events | Sort-Object { [double]$_.endTime })
        for ($i = 0; $i -lt $report.frames.Count; $i++) {
            $frame = $report.frames[$i]
            if ($i -lt $csv.Count) {
                $row = $csv[$i]
                foreach ($field in @('unityFrame','index','stage','consumed','completedWindows')) {
                    if ([long]$row.$field -ne [long]$frame.$field) { $errors.Add('csv-content:' + $field) }
                }
                foreach ($field in @('time','outerUpdateMs','telemetryMs','frameIntervalMs','mainThreadMs','renderThreadMs','gpuMs','gcBytes')) {
                    $raw = [double]::Parse([string]$row.$field, $invariant)
                    if ([Math]::Abs($raw - [double]$frame.$field) -gt 1e-6 * [Math]::Max(1, [Math]::Abs($raw))) { $errors.Add('csv-content:' + $field) }
                }
                foreach ($mapping in @(@('live','LiveCharacters'),@('submitted','SubmittedCharacters'),@('expiredResident','ExpiredResidentCharacters'),@('buckets','Buckets'),@('groups','InstanceGroups'),@('stride','VertexStride'),@('draws','Draws'),@('passes','Passes'))) {
                    if ([long]$row.($mapping[0]) -ne [long]$frame.statistics.($mapping[1])) { $errors.Add('csv-geometry:' + $mapping[0]) }
                }
                if ([bool][int]$row.refresh -ne [bool]$frame.refreshFrame -or [bool][int]$row.stateMatches -ne [bool]$frame.stateMatches) { $errors.Add('csv-flags') }
            }
            if ([int]$frame.stage -ne 3) { continue }
            if ($frame.index -ne $sampleIndex++) { $errors.Add('nonsequential-sample-index') }
            $s = $frame.statistics
            while ($birthCursor -lt $report.events.Count -and [single]$report.events[$birthCursor].request.SpawnTime -le [single]$frame.time) {
                $event = $report.events[$birthCursor++]; $oracleEvents++; $oracleCharacters += ([uint32]$event.request.Amount).ToString($invariant).Length
                $oracleHash = [DamageBenchmarkOfflineHash]::Xor($oracleHash, $stateHashes[[uint32]$event.request.EventId])
            }
            while ($endCursor -lt $endOrder.Count -and [single]$endOrder[$endCursor].endTime -le [single]$frame.time) {
                $event = $endOrder[$endCursor++]; $oracleEvents--; $oracleCharacters -= ([uint32]$event.request.Amount).ToString($invariant).Length
                $oracleHash = [DamageBenchmarkOfflineHash]::Xor($oracleHash, $stateHashes[[uint32]$event.request.EventId])
            }
            if ($frame.consumed -ne $birthCursor -or $frame.expectedLiveEvents -ne $oracleEvents -or $frame.expectedLiveCharacters -ne $oracleCharacters -or $frame.actualLiveChecksum -ne $oracleHash) { $errors.Add('input-state-oracle') }
            if (-not $frame.stateMatches -or $frame.expectedLiveEvents -ne $s.LiveEvents -or $frame.expectedLiveCharacters -ne $s.LiveCharacters -or $frame.expectedLiveChecksum -ne $frame.actualLiveChecksum -or $frame.actualLiveChecksum -ne $s.LiveEventChecksum) { $errors.Add('state-oracle') }
            if ($s.VertexStride -ne $expectedStride -or $s.Passes -ne $s.Draws -or ($instanced -and ($s.InstanceGroupCapacity -ne 128 -or $s.InstanceParameterStride -ne 96 -or $s.Draws -ne $s.InstanceGroups)) -or
                (-not $instanced -and ($s.InstanceGroups -ne 0 -or $s.Draws -ne $s.Buckets))) { $errors.Add('geometry-format-or-draw-count') }
            if ($s.SubmittedCharacters -ne $s.LiveCharacters + $s.ExpiredResidentCharacters) { $errors.Add('resident-accounting') }
            if ($s.Rejected -gt 0 -or $s.Accepted -ne $frame.consumed) { $errors.Add('rejection-or-consumption') }
            if ($s.MissedPresentation -gt 0) { $errors.Add('missed-presentation') }
            if ($s.PendingEvents -gt 0 -or $s.MaximumPendingAge -gt 0) { $errors.Add('unpublished-backlog') }
            if ($frame.time -lt $previousTime -or $frame.consumed -lt $previousConsumed) { $errors.Add('nonmonotonic-history') }
            if ([int]$report.plan.Phase -eq 1 -and $sampleIndex -gt 1 -and $frame.time -eq $previousTime) { $errors.Add('nonadvancing-lifecycle-clock') }
            if ([int]$report.plan.Phase -eq 0 -and $s.LiveCharacters -eq 0) { $errors.Add('empty-draw-checkpoint') }
            $previousTime = $frame.time; $previousConsumed = $frame.consumed
            if ([int]$report.plan.Mode -eq 1 -and $frame.index -lt $registeredTimes.Count -and [single]$frame.time -ne [single]$registeredTimes[$frame.index]) { $errors.Add('registered-time-mismatch') }
            if ($report.gpuStatus -eq 'unavailable' -and $frame.gpuMs -ge 0) { $errors.Add('fabricated-gpu-value') }
            foreach ($metric in @(@('mainThreadAvailable','mainThreadMs'),@('renderThreadAvailable','renderThreadMs'),@('gcAvailable','gcBytes'))) {
                if (-not $frame.($metric[0]) -and [double]$frame.($metric[1]) -ge 0) { $errors.Add('fabricated-unavailable:' + $metric[1]) }
            }
            if ([double]$frame.outerUpdateMs -lt 0 -or [double]::IsNaN($frame.outerUpdateMs) -or [double]::IsInfinity($frame.outerUpdateMs)) { $errors.Add('invalid-cpu-sample') }
            if ([int]$report.plan.Phase -eq 1 -and (($s.Rebuilds -gt $previousRebuilds) -ne [bool]$frame.refreshFrame)) { $errors.Add('refresh-frame-classification') }
            $previousRebuilds = $s.Rebuilds
        }
        if ($sampling.Count -gt 0 -and [single]$sampling[-1].time -ne [single]$report.intervalEnd) { $errors.Add('interval-not-completed') }
        if ($report.visualStatus -ne 'passed' -or [string]$report.visualEvidenceSHA256 -notmatch '\A[0-9A-Fa-f]{64}\z') { $errors.Add('visual-unverified') }
        if ($VisualEvidencePath) {
            if ((Get-FileHash -LiteralPath $VisualEvidencePath -Algorithm SHA256).Hash -ne $report.visualEvidenceSHA256) { $errors.Add('visual-evidence-hash') }
            $visual = Get-Content -LiteralPath $VisualEvidencePath -Raw | ConvertFrom-Json
            if (-not $visual.passed -or $visual.sourceFingerprint -ne $report.sourceFingerprint -or $visual.atlasHash -ne $report.atlasHash -or $visual.styleHash -ne $report.styleHash -or $visual.graphicsApi -ne $report.graphicsApi) { $errors.Add('visual-evidence-binding') }
            if (-not $visual.PSObject.Properties['width'] -or -not $visual.PSObject.Properties['height'] -or
                $visual.width -ne $report.width -or $visual.height -ne $report.height) { $errors.Add('visual-viewport-binding') }
            if (-not $visual.PSObject.Properties['cameraHash'] -or $visual.cameraHash -ne $report.cameraHash) { $errors.Add('visual-camera-binding') }
            if (-not $visual.PSObject.Properties['configurationHash'] -or $visual.configurationHash -ne $report.configurationHash) { $errors.Add('visual-configuration-binding') }
        }
        else { $errors.Add('visual-evidence-file-required') }
        $windows = @()
        if ([bool]$report.plan.BurstWorkload -and [int]$report.plan.Phase -eq 1) {
            $groups = @($report.events | Group-Object { [single]$_.request.SpawnTime })
            foreach ($group in $groups) {
                $start = [double]$group.Group[0].request.SpawnTime
                $nominalEnd = ($group.Group | Measure-Object -Property endTime -Maximum).Maximum
                $end = [Math]::Max($nominalEnd, ($group.Group | Measure-Object -Property observedEndedAt -Maximum).Maximum)
                $frames = @($sampling | Where-Object { $_.time -ge $start -and $_.time -le $end })
                $windows += [ordered]@{ start=$start; nominalEnd=$nominalEnd; observedEnd=$end; requests=$group.Count; frames=$frames.Count; cpu=(Get-Distribution @($frames | ForEach-Object { $_.outerUpdateMs })) }
            }
            if ($groups.Count -lt 100 -or $report.completedWindows -lt 100) { $errors.Add('insufficient-burst-windows') }
        }
        $refresh = @($sampling | Where-Object { $_.refreshFrame })
        $entry.summaries = [ordered]@{
            allCpuMs=(Get-Distribution @($sampling | ForEach-Object { $_.outerUpdateMs })); refreshCpuMs=(Get-Distribution @($refresh | ForEach-Object { $_.outerUpdateMs }))
            frameIntervalMs=(Get-Distribution @($sampling | ForEach-Object { $_.frameIntervalMs }))
            mainThreadMs=(Get-Distribution @($sampling | Where-Object { $_.mainThreadAvailable } | ForEach-Object { $_.mainThreadMs }))
            renderThreadMs=(Get-Distribution @($sampling | Where-Object { $_.renderThreadAvailable } | ForEach-Object { $_.renderThreadMs }))
            gpuMs=(Get-Distribution @($sampling | Where-Object { $report.gpuStatus -ne 'unavailable' } | ForEach-Object { $_.gpuMs }))
            refreshGpuMs=(Get-Distribution @($sampling | Where-Object { $report.gpuStatus -eq 'validated-native-timestamps' -and $_.refreshFrame } | ForEach-Object { $_.gpuMs }))
        }
        $entry.burstWindows = $windows
        $entry['metadata'] = $report | Select-Object scheme,commit,sourceFingerprint,buildGuid,executionMode,device,graphicsApi,backend,clipHash,atlasHash,styleHash,configurationHash,cameraHash,workloadHash,timesHash,width,height,gpuStatus,gpuScope,colorSpace,buildType,targetFrameRate,vSyncCount,qualityLevel
    }
    catch { $errors.Add('malformed-or-missing-artifact:' + $_.Exception.Message) }
    $entry.errors = @($errors | Sort-Object -Unique); $entry.valid = $entry.errors.Count -eq 0; $runs += $entry
}

$comparisonErrors = [Collections.Generic.List[string]]::new()
if ($runs.Count -ne 4) { $comparisonErrors.Add('ABBA-requires-four-runs') }
if (@($runs | Where-Object { -not $_.valid }).Count) { $comparisonErrors.Add('invalid-run') }
if ($runs.Count -eq 4 -and @($runs | Where-Object { -not $_.valid }).Count -eq 0) {
    $schemes = @($runs | ForEach-Object { $_.metadata.scheme })
    if ($schemes[0] -ne $schemes[3] -or $schemes[1] -ne $schemes[2] -or $schemes[0] -eq $schemes[1]) { $comparisonErrors.Add('not-balanced-ABBA') }
    foreach ($field in @('commit','sourceFingerprint','buildGuid','executionMode','device','graphicsApi','backend','clipHash','atlasHash','styleHash','configurationHash','cameraHash','workloadHash','timesHash','width','height','colorSpace','buildType','targetFrameRate','vSyncCount','qualityLevel','gpuStatus','gpuScope')) {
        if (@($runs | ForEach-Object { $_.metadata.$field } | Sort-Object -Unique).Count -ne 1) { $comparisonErrors.Add('unequal:' + $field) }
    }
}
$valid = $comparisonErrors.Count -eq 0
$deviceCandidate = $valid -and $runs[0].metadata.executionMode -eq 'Player' -and $runs[0].metadata.backend -eq 'IL2CPP' -and $runs[0].metadata.graphicsApi -eq 'Vulkan' -and $runs[0].metadata.device -match 'Mi.?10|Xiaomi.?10'
$nativeGpuCandidate = $deviceCandidate -and @($runs | Where-Object { $_.metadata.gpuStatus -ne 'validated-native-timestamps' }).Count -eq 0
$repeatSpread = $null
if ($valid) {
    $a = @([double]$runs[0].summaries.allCpuMs.median, [double]$runs[3].summaries.allCpuMs.median)
    $b = @([double]$runs[1].summaries.allCpuMs.median, [double]$runs[2].summaries.allCpuMs.median)
    $repeatSpread = [ordered]@{
        scope='outer Update CPU only; GPU/whole-frame conclusions remain unverified'
        firstScheme=$runs[0].metadata.scheme; secondScheme=$runs[1].metadata.scheme
        firstMedians=$a; secondMedians=$b; firstSpreadMs=[Math]::Abs($a[0]-$a[1]); secondSpreadMs=[Math]::Abs($b[0]-$b[1])
        secondMinusFirstMeanMedianMs=($b[0]+$b[1]-$a[0]-$a[1])/2
    }
    if ($nativeGpuCandidate) {
        $gpuA=@([double]$runs[0].summaries.gpuMs.median,[double]$runs[3].summaries.gpuMs.median)
        $gpuB=@([double]$runs[1].summaries.gpuMs.median,[double]$runs[2].summaries.gpuMs.median)
        $repeatSpread.nativeGpu=@{scope='HUD-camera-instrumented; includes query/pass-boundary overhead';firstMedians=$gpuA;secondMedians=$gpuB
            firstSpreadMs=[Math]::Abs($gpuA[0]-$gpuA[1]);secondSpreadMs=[Math]::Abs($gpuB[0]-$gpuB[1]);secondMinusFirstMeanMedianMs=($gpuB[0]+$gpuB[1]-$gpuA[0]-$gpuA[1])/2}
    }
}
$result = [ordered]@{
    schema='damage-benchmark-analysis-v1'; valid=$valid; directRankingEligible=$false; targetDeviceCandidate=$deviceCandidate; comparisonErrors=@($comparisonErrors)
    percentileMethod='median average middle pair; P95/P99 nearest-rank; no tail removal or empty-frame padding'
    nativeGpuCandidate=$nativeGpuCandidate
    gpuConclusion=if($nativeGpuCandidate){'Validated instrumented HUD camera interval; final acceptance also requires exact APK/environment and visual/instrumentation evidence. Not an uninstrumented whole-frame claim.'}else{'unverified: requires validated external GPU tool/scope evidence'}
    fullFrameConclusion='unverified until balanced target-device whole-frame evidence is accepted'
    rankingGate='Validated report accounting alone does not establish GPU or whole-frame gains; exact APK/environment and GPU tool/scope evidence require separate acceptance'
    repeatSpread=$repeatSpread
    runs=$runs
}
$parent = Split-Path -Parent $OutputPath
if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$result | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output ("Valid ABBA: {0}; direct device ranking remains unverified; analysis: {1}" -f $valid, $OutputPath)
if (-not $valid) { exit 2 }
exit 0
