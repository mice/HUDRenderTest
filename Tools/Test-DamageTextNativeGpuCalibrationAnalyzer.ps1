param([string]$OutputDirectory='TestResults/DamageTextNativeGpuCalibration/AnalyzerFixtures')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$base=Join-Path (Split-Path -Parent $PSScriptRoot) 'TestResults/DamageTextGpuCalibration/AnalyzerFixtures'
if(-not (Test-Path -LiteralPath (Join-Path $base 'synthetic-valid.json'))){
    & (Join-Path $PSScriptRoot 'Test-DamageTextGpuCalibrationAnalyzer.ps1') -OutputDirectory $base
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$probe=Get-Content -LiteralPath (Join-Path $base 'synthetic-valid.json') -Raw | ConvertFrom-Json
$build=Get-Content -LiteralPath (Join-Path $base 'synthetic-build.json') -Raw | ConvertFrom-Json
$probe.schema='damage-frame-timing-calibration-v3'
$probe | Add-Member nativePluginSHA256 ('D'*64)
$probe | Add-Member nativeScope 'Synthetic fixture only; not hardware/performance evidence.'
$probe | Add-Member api 'Vulkan'
$probe | Add-Member backend 'IL2CPP'
$probe | Add-Member width 2340
$probe | Add-Member height 1080
$probe | Add-Member nativeInfo @{status=1;capacity=640;timestampPeriodNs=10.0;timestampValidBits=64}
$build | Add-Member nativeVulkanProbe $true
$build | Add-Member nativePluginSHA256 ('D'*64)
$native=@()
foreach($frame in $probe.frames){
    $i=$frame.relativeFrame;$ms=.5+$frame.renderedDrawRepeats*.2;$begin=[long]1000000000+[long]$i*10000000
    $native+=@{index=$i;unityFrame=$i+1;beginCalls=1;endCalls=1;available=1;status=2;milliseconds=$ms
        beginNativeFrame=[string]($i+100);endNativeFrame=[string]($i+100);readSafeFrame=[string]($i+100);readNativeFrame=[string]($i+104)
        beginTimestamp=$begin.ToString('X16');endTimestamp=($begin+[long]($ms*100000)).ToString('X16')}
}
$probe | Add-Member nativeTimings $native
$buildPath=Join-Path $OutputDirectory 'synthetic-build.json'
$build | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $buildPath -Encoding utf8
$json=$probe | ConvertTo-Json -Depth 10
$validPath=Join-Path $OutputDirectory 'synthetic-valid.json'; $validOutput=Join-Path $OutputDirectory 'valid-analysis.json'
$json | Set-Content -LiteralPath $validPath -Encoding utf8
$analyzer=Join-Path $PSScriptRoot 'Analyze-DamageTextNativeGpuCalibration.ps1'
& $analyzer -ReportPath $validPath -BuildMetadataPath $buildPath -OutputPath $validOutput
$valid=Get-Content -LiteralPath $validOutput -Raw | ConvertFrom-Json
if(-not $valid.frameBindingCandidate -or $valid.formalRankingEligible){throw 'Synthetic command-bound calibration did not pass.'}
$checks=@()
foreach($case in @('missing','token','unavailable','scale','camera','cpuSeparation','nativeFrame','shiftedLoad','pluginMetadata','readBeforeFence')){
    $broken=$json | ConvertFrom-Json
    switch($case){
        'missing' {$broken.nativeTimings=@($broken.nativeTimings | Where-Object index -ne 200)}
        'token' {$broken.nativeTimings[200].unityFrame++}
        'unavailable' {$broken.nativeTimings[200].available=0;$broken.nativeTimings[200].status=1}
        'scale' {$broken.nativeInfo.timestampPeriodNs=20}
        'camera' {$broken.frames[200].postRenderUnityFrame++}
        'cpuSeparation' {
            $broken.nativeTimings[80].milliseconds=81
            $begin=[long][Convert]::ToUInt64($broken.nativeTimings[80].beginTimestamp,16)
            $broken.nativeTimings[80].endTimestamp=($begin+8100000).ToString('X16')
        }
        'nativeFrame' {$broken.nativeTimings[200].endNativeFrame='301'}
        'shiftedLoad' {
            for($i=400;$i -lt 624;$i++){
                $ms=.5+$broken.frames[$i+1].renderedDrawRepeats*.2
                $broken.nativeTimings[$i].milliseconds=$ms
                $begin=[long][Convert]::ToUInt64($broken.nativeTimings[$i].beginTimestamp,16)
                $broken.nativeTimings[$i].endTimestamp=($begin+[long]($ms*100000)).ToString('X16')
            }
        }
        'pluginMetadata' {$broken.nativePluginSHA256='E'*64}
        'readBeforeFence' {$broken.nativeTimings[200].readSafeFrame='299'}
    }
    $path=Join-Path $OutputDirectory ("broken-$case.json");$output=Join-Path $OutputDirectory ("rejected-$case.json")
    $broken | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
    $rejected=$false;$reason=''
    try{
        & $analyzer -ReportPath $path -BuildMetadataPath $buildPath -OutputPath $output
        $result=Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
        $rejected=-not $result.frameBindingCandidate -and -not $result.formalRankingEligible
    }catch{$rejected=$true;$reason=$_.Exception.Message}
    if(-not $rejected){throw "Native corrupted calibration accepted: $case"}
    $checks+=@{case=$case;rejected=$rejected;reason=$reason}
}
@{schema='damage-native-gpu-analyzer-self-test-v1';synthetic=$true;performanceEvidence=$false;validCandidate=$true;corruptCases=$checks
    analyzerSHA256=(Get-FileHash -LiteralPath $analyzer).Hash
} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'self-test.json') -Encoding utf8
Write-Output 'Synthetic native binding fixture and ten corrupted-data rejection cases passed; no hardware claim.'
