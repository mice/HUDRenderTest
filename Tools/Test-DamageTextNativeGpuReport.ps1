param([string]$OutputDirectory='TestResults/DamageTextNativeGpuReport')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'NativeGpuTiming/Validate-NativeGpuReport.ps1')
New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'device') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'build') -Force | Out-Null
$calibrationPath=Join-Path $OutputDirectory 'device/index.json'
$archivePath=Join-Path $OutputDirectory 'device/synthetic-archive.txt'
'Synthetic identity fixture, not device evidence.' | Set-Content -LiteralPath $archivePath -Encoding utf8
@{nativePluginSHA256='A'*64} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'build/DamageTextComparison.apk.build.json') -Encoding utf8
$capacity=3678
$info=@{status=1;capacity=$capacity;timestampPeriodNs=10.0;timestampValidBits=48;queueFamily=0;vendorId=1;deviceId=2;apiVersion=3;driverVersion=4}
$runs=@();for($i=0;$i -lt 3;$i++){$runs+=@{candidate=$true;complete=$true;required=560;errors=@();info=$info}}
@{schema='damage-native-gpu-calibration-device-evidence-v1';runs=$runs;archive='synthetic-archive.txt';archiveSHA256=(Get-FileHash -LiteralPath $archivePath).Hash} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $calibrationPath -Encoding utf8
$cameras=@();$timings=@();$frames=@()
for($i=0;$i -lt $capacity;$i++){
    $unityFrame=100+$i;$nativeFrame=1000+$i;$begin=[long]1000000000+[long]$i*1000000
    $cameras+=@{unityFrame=$unityFrame;preCullFrame=$unityFrame;postRenderFrame=$unityFrame;cameraPasses=1;bindingCpuMs=.01}
    $timings+=@{index=$i;unityFrame=$unityFrame;beginCalls=1;endCalls=1;available=1;status=2;milliseconds=.6
        beginNativeFrame=[string]$nativeFrame;endNativeFrame=[string]$nativeFrame;readSafeFrame=[string]$nativeFrame;readNativeFrame=[string]($nativeFrame+1)
        beginTimestamp=$begin.ToString('X16');endTimestamp=($begin+60000).ToString('X16')}
    if($i -lt $capacity-16){$frames+=@{unityFrame=$unityFrame;stage=if($i -lt 60){1}elseif($i -lt 62){2}else{3};gpuAvailable=$true;gpuMs=.6}}
}
$report=@{executionMode='Player';graphicsApi='Vulkan';backend='IL2CPP';buildType='Release';gpuScope='HUD-camera-instrumented';samples=3600;frames=$frames
    nativeGpu=@{schema='damage-native-gpu-capture-v1';complete=$true;pluginSHA256='A'*64;calibrationSHA256=(Get-FileHash -LiteralPath $calibrationPath).Hash
        info=$info;capacity=$capacity;commandBuffers=$capacity*2;drainFrames=16;setupCpuMs=10.0;capturedFrames=$capacity;firstUnityFrame=100;matchedSamples=3600;cameraFrames=$cameras;timings=$timings}}
$json=$report | ConvertTo-Json -Depth 10
$valid=Test-DamageNativeGpuReport ($json | ConvertFrom-Json) $calibrationPath
if(-not $valid.valid){throw ('Synthetic native report rejected: '+($valid.errors -join ';'))}
$checks=@()
foreach($case in @('zero','token','scale','fence','camera','samples','calibration','plugin','scope','duplicateCall','gpuMirror','device','missingDrain')){
    $broken=$json | ConvertFrom-Json
    switch($case){
        'zero' {$broken.nativeGpu.timings[200].available=0;$broken.nativeGpu.timings[200].milliseconds=0}
        'token' {$broken.nativeGpu.timings[200].unityFrame++}
        'scale' {$broken.nativeGpu.timings[200].endTimestamp=$broken.nativeGpu.timings[200].beginTimestamp}
        'fence' {$broken.nativeGpu.timings[200].readSafeFrame='1'}
        'camera' {$broken.nativeGpu.cameraFrames[200].cameraPasses=2}
        'samples' {$broken.samples=2999}
        'calibration' {$broken.nativeGpu.calibrationSHA256='B'*64}
        'plugin' {$broken.nativeGpu.pluginSHA256='B'*64}
        'scope' {$broken.gpuScope='whole-frame'}
        'duplicateCall' {$broken.nativeGpu.timings[200].endCalls=2}
        'gpuMirror' {$broken.frames[200].gpuMs=.7}
        'device' {$broken.nativeGpu.info.driverVersion++}
        'missingDrain' {$broken.nativeGpu.drainFrames=0}
    }
    $result=Test-DamageNativeGpuReport $broken $calibrationPath
    if($result.valid){throw "Corrupted native report accepted: $case"}
    $checks+=@{case=$case;rejected=$true;errors=$result.errors}
}
@{schema='damage-native-gpu-report-self-test-v1';synthetic=$true;performanceEvidence=$false;validFixturePassed=$valid.valid;corruptCases=$checks
    validatorSHA256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'NativeGpuTiming/Validate-NativeGpuReport.ps1')).Hash} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'self-test.json') -Encoding utf8
Write-Output 'Native report fixture and thirteen corrupted traces rejected correctly; synthetic only.'
