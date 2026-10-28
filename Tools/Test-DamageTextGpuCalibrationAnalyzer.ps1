param([string]$OutputDirectory = 'TestResults/DamageTextGpuCalibration/AnalyzerFixtures')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$analyzer=Join-Path $PSScriptRoot 'Analyze-DamageTextGpuCalibration.ps1'
$identity=@{sourceFingerprint=('A'*64);commit=('b'*40);buildGuid=('c'*32);applicationIdentifier='synthetic.calibration.fixture'}
$metadata=@{}; foreach($key in $identity.Keys){$metadata[$key]=$identity[$key]}
$metadata.frameTimingProbe=$metadata.frameTimingStatsEnabled=$true
$metadata.backend='IL2CPP';$metadata.architecture='ARM64';$metadata.api='Vulkan';$metadata.buildType='Release'
$buildPath=Join-Path $OutputDirectory 'synthetic-build.json'
$metadata | ConvertTo-Json | Set-Content -LiteralPath $buildPath -Encoding utf8
$sleeps=@{80=80;168=130;328=100;520=90}
$loads=@{96=16;192=32;288=64;384=16;480=32;576=64}
foreach($start in @(120,240,360)){foreach($frame in $start..($start+2)){$loads[$frame]=64}}
$frames=@();$timings=@();$clock=[long]100000000000
for($i=0;$i -lt 640;$i++){
    $sleep=if($sleeps.ContainsKey($i)){$sleeps[$i]}else{0}
    $load=if($loads.ContainsKey($i)){$loads[$i]}else{0}
    $frames+=@{relativeFrame=$i;unityFrame=$i+1;stopwatchTicks=[string]$clock
        preCullTicks=[string]($clock+1000+[long]$sleep*10000);postRenderTicks=[string]($clock+2000+[long]$sleep*10000)
        cpuSleepMs=$sleep;requestedDrawRepeats=$load;renderedDrawRepeats=$load;cameraPasses=1;preCullUnityFrame=$i+1;postRenderUnityFrame=$i+1}
    if($i -lt 636){
        for($observation=0;$observation -lt 3;$observation++){
            $pending=$i -in @(180,300) -and $observation -eq 0
            $timings+=@{frameStartTimestamp=([long]($clock*100-100000)).ToString('X16');observedAtUnityFrame=$i+5+$observation
                observationPhase=($observation%2);gpuFrameMsRaw=if($pending){0.0}else{.5+$load*.2};gpuTimeValid=(-not $pending)
                cpuMainThreadMs=.4+$sleep;cpuRenderThreadMs=.2;cpuFrameMs=16.7+$sleep}
        }
    }
    $clock+=167000+[long]$sleep*10000
}
$probe=@{schema='damage-frame-timing-calibration-v2';formal=$false;featureEnabled=$true;frameCount=640;warmupFrames=64;drainStart=624
    cpuTimerFrequency='1000000000';stopwatchFrequency='10000000';scheme='synthetic-not-a-device';frames=$frames;timings=$timings}
foreach($key in $identity.Keys){$probe[$key]=$identity[$key]}
$validJson=$probe | ConvertTo-Json -Depth 8
$validPath=Join-Path $OutputDirectory 'synthetic-valid.json'
$validJson | Set-Content -LiteralPath $validPath -Encoding utf8
$validOutput=Join-Path $OutputDirectory 'valid-analysis.json'
& $analyzer -ReportPath $validPath -BuildMetadataPath $buildPath -OutputPath $validOutput
$valid=Get-Content -LiteralPath $validOutput -Raw | ConvertFrom-Json
if(-not $valid.frameBindingCandidate -or $valid.lateRecoveries -ne 2 -or $valid.formalRankingEligible){throw 'Synthetic complete/recovered fixture did not pass the candidate gate.'}
$checks=@()
foreach($case in @('missing','zero','unstable','camera','clock','variableOffset','metadata','duplicateTimestampMapping','schedule')){
    $broken=$validJson | ConvertFrom-Json
    $target=$broken.timings[600].frameStartTimestamp
    switch($case){
        'missing' {$broken.timings=@($broken.timings | Where-Object frameStartTimestamp -ne $target)}
        'zero' {foreach($row in $broken.timings | Where-Object frameStartTimestamp -eq $target){$row.gpuFrameMsRaw=0;$row.gpuTimeValid=$false}}
        'unstable' {$broken.timings[602].gpuFrameMsRaw=2.0}
        'camera' {$broken.frames[200].preCullUnityFrame++}
        'clock' {foreach($row in $broken.timings){$row.frameStartTimestamp=([long][Convert]::ToUInt64($row.frameStartTimestamp,16)+[long]900000000000000).ToString('X16')}}
        'variableOffset' {
            for($i=400;$i -lt 635;$i++){
                $nextLoad=$broken.frames[$i+1].renderedDrawRepeats
                for($j=0;$j -lt 3;$j++){$broken.timings[$i*3+$j].gpuFrameMsRaw=.5+$nextLoad*.2}
            }
        }
        'metadata' {$broken.commit='d'*40}
        'duplicateTimestampMapping' {
            $copy=($broken.timings[600] | ConvertTo-Json | ConvertFrom-Json)
            $copy.frameStartTimestamp=([long][Convert]::ToUInt64($copy.frameStartTimestamp,16)+10000).ToString('X16')
            $broken.timings=@($broken.timings)+@($copy,$copy)
        }
        'schedule' {$broken.frames[80].cpuSleepMs=0}
    }
    $path=Join-Path $OutputDirectory ("broken-$case.json");$output=Join-Path $OutputDirectory ("rejected-$case.json")
    $broken | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8
    $rejected=$false;$reason=''
    try{
        & $analyzer -ReportPath $path -BuildMetadataPath $buildPath -OutputPath $output
        $result=Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
        $rejected=-not $result.frameBindingCandidate -and -not $result.formalRankingEligible
    }catch{$rejected=$true;$reason=$_.Exception.Message}
    if(-not $rejected){throw "Corrupted calibration accepted: $case"}
    $checks+=@{case=$case;rejected=$rejected;reason=$reason}
}
@{schema='damage-gpu-calibration-analyzer-self-test-v1';synthetic=$true;performanceEvidence=$false
    validCandidate=$valid.frameBindingCandidate;lateRecoveries=$valid.lateRecoveries;corruptCases=$checks
    analyzerSHA256=(Get-FileHash -LiteralPath $analyzer).Hash
} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'self-test.json') -Encoding utf8
Write-Output 'Synthetic late-recovery fixture and nine corrupted-data rejection cases passed; no hardware/performance claim.'
