[CmdletBinding()]
param([string]$OutputDirectory = 'TestResults/DamageTextComparison/AnalyzerFixtures')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$invariant = [Globalization.CultureInfo]::InvariantCulture
$analyzer = Join-Path $PSScriptRoot 'Analyze-DamageTextBenchmark.ps1'
if (-not ('DamageBenchmarkFixtureHash' -as [type])) {
    Add-Type -TypeDefinition @'
public static class DamageBenchmarkFixtureHash {
    public static long Value() {
        unchecked {
            ulong h = 14695981039346656037UL;
            foreach (uint v in new uint[] {1,10,0,1,0,0,0,0}) h = (h ^ v) * 1099511628211UL;
            return (long)h;
        }
    }
}
'@
}
$fixtureStateHash = [DamageBenchmarkFixtureHash]::Value()

function Get-BinaryHash([bool]$Times) {
    $stream = [IO.MemoryStream]::new(); $writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::UTF8, $true)
    try {
        if ($Times) { $writer.Write('damage-times-v1'); for ($i=0; $i -lt 3000; $i++) { $writer.Write([double](60.0 * $i / 2999)) } }
        else {
            $writer.Write('damage-benchmark-events-v1'); $writer.Write([int]1)
            $writer.Write([uint32]1); $writer.Write([uint32]10); $writer.Write([byte]0)
            foreach ($value in @(0,0,0,0)) { $writer.Write([single]$value) }; $writer.Write([uint32]1)
        }
        $writer.Flush(); $sha = [Security.Cryptography.SHA256]::Create()
        try { return [BitConverter]::ToString($sha.ComputeHash($stream.ToArray())).Replace('-','') } finally { $sha.Dispose() }
    }
    finally { $writer.Dispose(); $stream.Dispose() }
}
function Save-Report($Report, [string]$Stem) {
    $path = Join-Path $OutputDirectory ($Stem + '.report.json'); $csvPath = Join-Path $OutputDirectory ($Stem + '.frames.csv')
    $Report | ConvertTo-Json -Depth 15 -Compress | Set-Content -LiteralPath $path -Encoding utf8
    $rows = foreach ($frame in $Report.frames) {
        [pscustomobject][ordered]@{
            unityFrame=$frame.unityFrame; index=$frame.index; stage=$frame.stage; time=([double]$frame.time).ToString('R',$invariant)
            outerUpdateMs=$frame.outerUpdateMs; telemetryMs=$frame.telemetryMs; frameIntervalMs=$frame.frameIntervalMs
            mainThreadMs=$frame.mainThreadMs; renderThreadMs=$frame.renderThreadMs; gpuMs=$frame.gpuMs; gcBytes=$frame.gcBytes
            consumed=$frame.consumed; live=$frame.statistics.LiveCharacters; submitted=$frame.statistics.SubmittedCharacters
            expiredResident=$frame.statistics.ExpiredResidentCharacters; buckets=$frame.statistics.Buckets; groups=$frame.statistics.InstanceGroups
            stride=$frame.statistics.VertexStride; draws=$frame.statistics.Draws; passes=$frame.statistics.Passes
            refresh=[int]$frame.refreshFrame; stateMatches=[int]$frame.stateMatches; completedWindows=$frame.completedWindows
        }
    }
    $rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8
    @{ schema='damage-benchmark-artifacts-v1'; reportSHA256=(Get-FileHash $path -Algorithm SHA256).Hash; csvSHA256=(Get-FileHash $csvPath -Algorithm SHA256).Hash } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory ($Stem + '.manifest.json')) -Encoding utf8
    return $path
}
$visualPath = Join-Path $OutputDirectory 'synthetic-visual-evidence.json'
@{ passed=$true; sourceFingerprint=('A'*64); atlasHash=('C'*64); styleHash=('D'*64); graphicsApi='Direct3D11'; width=1920; height=1080; cameraHash=('1'*64); configurationHash=('F'*64); scope='synthetic analyzer fixtures, not rendered performance evidence' } |
    ConvertTo-Json | Set-Content -LiteralPath $visualPath -Encoding utf8
$frames = for ($i=0; $i -lt 3000; $i++) {
    $alive = $i -lt 2999
    [ordered]@{
        unityFrame=$i; index=$i; stage=3; time=[single](60.0 * $i / 2999); outerUpdateMs=[double](1 + ($i % 100) / 100.0)
        telemetryMs=.01; frameIntervalMs=16.6667; mainThreadMs=-1; renderThreadMs=-1; gpuMs=-1; gcBytes=-1
        mainThreadAvailable=$false; renderThreadAvailable=$false; gcAvailable=$false; consumed=1; completedWindows=[int](-not $alive)
        expectedLiveEvents=[int]$alive; expectedLiveCharacters=2*[int]$alive
        expectedLiveChecksum=$(if($alive){$fixtureStateHash}else{[long]0}); actualLiveChecksum=$(if($alive){$fixtureStateHash}else{[long]0}); stateMatches=$true; refreshFrame=($i -eq 0)
        statistics=[ordered]@{ LiveEvents=[int]$alive; LiveCharacters=2*[int]$alive; SubmittedCharacters=2*[int]$alive; ExpiredResidentCharacters=0
            LiveEventChecksum=$(if($alive){$fixtureStateHash}else{[long]0}); Rejected=0; Accepted=1; MissedPresentation=0; PendingEvents=0; MaximumPendingAge=0; Rebuilds=1
            Buckets=[int]$alive; InstanceGroups=0; InstanceGroupCapacity=0; InstanceParameterStride=0; VertexStride=68; Draws=[int]$alive; Passes=[int]$alive }
    }
}
$report = [ordered]@{
    schema='damage-text-benchmark-v1'; utc='2026-10-26T00:00:00Z'; commit=('B'*40); sourceStatus='synthetic-analyzer-fixture'; sourceFingerprint=('A'*64)
    buildGuid='synthetic'; packageVersion='0.1.2'; unityVersion='2022.3.53f1'; device='Synthetic Editor'; operatingSystem='fixture'
    graphicsApi='Direct3D11'; graphicsDevice='synthetic'; backend='Mono'; executionMode='Editor'; clockPolicy='pre-registered-times-v1'
    colorSpace='Linear'; buildType='Development'; targetFrameRate=60; vSyncCount=0; qualityLevel=0
    clipHash=('E'*64); atlasHash=('C'*64); styleHash=('D'*64); configurationHash=('F'*64); cameraHash=('1'*64); planHash=('2'*64); workloadHash=('3'*64)
    timesHash=(Get-BinaryHash $true); expectedRequestHash=(Get-BinaryHash $false); consumedRequestHash=(Get-BinaryHash $false)
    scheme='A-SharedQuad-Float32'; requestedFormat='Float32'; effectiveFormat='Float32'; formatReason='requested-float32'
    shaderName='UIData/DamageTextInstanced'; shaderPassCount=1; vertexLayout='stride=12|Position:Float32x3@0#0'
    visualStatus='passed'; visualEvidenceSHA256=(Get-FileHash $visualPath -Algorithm SHA256).Hash; gpuStatus='unavailable'; gpuScope='unavailable'
    formal=$true; controlsVisible=$false; aborted=$false; reason='synthetic'; completeEventInterval=$true; expectedEvents=1; consumedEvents=1
    samples=3000; completedWindows=1; width=1920; height=1080; intervalStart=0; intervalEnd=60
    plan=@{ Mode=1; Phase=1; BurstWorkload=$false }; registeredTimes=@(for($i=0;$i -lt 3000;$i++){60.0*$i/2999})
    timeEncoding='ieee754-f64-hex-v1'; registeredTimeBits=@(for($i=0;$i -lt 3000;$i++){[BitConverter]::DoubleToInt64Bits(60.0*$i/2999).ToString('X16',$invariant)})
    events=@(@{ request=@{ EventId=1; Amount=10; Kind=0; WorldPosition=@{x=0;y=0;z=0}; SpawnTime=0; RandomSeed=1 }
        result=0; consumedAt=0; firstPublishedAt=0; endTime=60; observedEndedAt=60; missed=$false })
    frames=@($frames)
}
$paths = @()
foreach ($item in @(@('A1','A-SharedQuad-Float32'),@('B1','B-PersistentMesh-Float32'),@('B2','B-PersistentMesh-Float32'),@('A2','A-SharedQuad-Float32'))) {
    $report.scheme = $item[1]; $instanced = $item[0].StartsWith('A')
    $report.shaderName = if ($instanced) { 'UIData/DamageTextInstanced' } else { 'UIData/DamageText' }
    $report.vertexLayout = if ($instanced) { 'stride=12|Position:Float32x3@0#0' } else { 'stride=68|Position:Float32x2@0#0|Color:UNorm8x4@8#0|TexCoord0:Float32x2@12#0|TexCoord1:Float32x4@20#0|TexCoord2:Float32x4@36#0|TexCoord3:Float32x4@52#0' }
    foreach ($frame in $report.frames) {
        $frame.statistics.VertexStride = if ($instanced) { 12 } else { 68 }
        $frame.statistics.InstanceGroups = if ($instanced) { $frame.statistics.Draws } else { 0 }
        $frame.statistics.InstanceGroupCapacity = if ($instanced) { 128 } else { 0 }
        $frame.statistics.InstanceParameterStride = if ($instanced) { 96 } else { 0 }
    }
    $paths += Save-Report $report $item[0]
}
$validOutput = Join-Path $OutputDirectory 'valid-analysis.json'
& $analyzer -ReportPaths $paths -VisualEvidencePath $visualPath -OutputPath $validOutput
if ($LASTEXITCODE -ne 0) { throw "Valid synthetic ABBA was rejected: $validOutput" }
$validResult = Get-Content $validOutput -Raw | ConvertFrom-Json
if (-not $validResult.valid -or $validResult.directRankingEligible) { throw 'Synthetic data must pass protocol checks without becoming device evidence.' }
if ($validResult.runs[0].summaries.allCpuMs.n -ne 3000 -or $validResult.runs[0].summaries.refreshCpuMs.n -ne 1 -or $validResult.runs[0].summaries.allCpuMs.max -ne 1.99) { throw 'Quantile/refresh distribution mismatch.' }
$cases = [ordered]@{
    aborted={ param($r) $r.aborted=$true }
    rejected={ param($r) $r.events[0].result=8 }
    request={ param($r) $r.events[0].request.Amount=11 }
    visual={ param($r) $r.visualStatus='failed' }
    metadata={ param($r) $r.sourceFingerprint='unknown' }
    samples={ param($r) $r.samples=2999 }
    time={ param($r) $r.frames[7].time=2 }
    state={ param($r) $r.frames[7].actualLiveChecksum=0 }
    coherentState={ param($r) $r.frames[7].actualLiveChecksum=0; $r.frames[7].expectedLiveChecksum=0; $r.frames[7].statistics.LiveEventChecksum=0 }
    stride={ param($r) $r.frames[7].statistics.VertexStride=999 }
    timeBits={ param($r) $r.registeredTimeBits[7]='0000000000000000' }
    gpu={ param($r) $r.frames[7].gpuMs=0 }
    burst={ param($r) $r.plan.BurstWorkload=$true }
    viewport={ param($r) $r.width=1080; $r.height=1920 }
    camera={ param($r) $r.cameraHash=('9'*64) }
    visualConfiguration={ param($r) $r.configurationHash=('9'*64) }
}
$results = @()
$original = Get-Content $paths[0] -Raw
foreach ($name in $cases.Keys) {
    $broken = $original | ConvertFrom-Json; & $cases[$name] $broken
    $brokenPath = Save-Report $broken ('broken-' + $name)
    $output = Join-Path $OutputDirectory ('rejected-' + $name + '.json')
    & $analyzer -ReportPaths @($brokenPath,$paths[1],$paths[2],$paths[3]) -VisualEvidencePath $visualPath -OutputPath $output
    $rejected = Get-Content $output -Raw | ConvertFrom-Json
    if ($LASTEXITCODE -eq 0 -or $rejected.valid -or $rejected.directRankingEligible) { throw "Corrupt $name fixture was accepted." }
    $bindingError = switch ($name) { 'viewport' { 'visual-viewport-binding' } 'camera' { 'visual-camera-binding' } 'visualConfiguration' { 'visual-configuration-binding' } default { $null } }
    if ($bindingError -and $bindingError -notin $rejected.runs[0].errors) { throw "Visual binding regression was not detected: $name" }
    $results += [ordered]@{ case=$name; rejected=$true; reasons=$rejected.runs[0].errors }
}
@{ schema='damage-analyzer-self-test-v1'; passed=$true; syntheticOnly=$true; validFixtureSamples=3000; corruptCases=$results } |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'self-test.json') -Encoding utf8
Write-Output ('Analyzer self-test passed: valid synthetic ABBA and ' + $cases.Count + ' corrupt cases; no device ranking.')
