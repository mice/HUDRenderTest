[CmdletBinding()]
param(
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.53f1\Editor\Unity.exe',
    [ValidateRange(1, 120)][int]$TimeoutMinutes = 30,
    [switch]$FrameTimingProbe,
    [switch]$PrecisionProbe,
    [switch]$NativeVulkanProbe,
    [switch]$NativeGpuTiming,
    [switch]$ProbeInBenchmarkPackage
)

$ErrorActionPreference = 'Stop'
if ($NativeVulkanProbe) { $FrameTimingProbe = $true }
if ($NativeGpuTiming -and ($FrameTimingProbe -or $PrecisionProbe)) { throw 'Choose formal native timing or a diagnostic probe.' }
if ($FrameTimingProbe -and $PrecisionProbe) { throw 'Choose one diagnostic probe per build.' }
if ($ProbeInBenchmarkPackage -and -not ($FrameTimingProbe -or $PrecisionProbe)) { throw 'Package reuse requires a diagnostic probe.' }
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'HUDRenderTestSample'
$head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0) {
    throw 'Build from an isolated clean checkout so user changes and dependencies are not silently included.'
}
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw "Unity executable missing: $UnityExe" }
$outputRoot = Join-Path $repositoryRoot ('Builds/DamageTextComparison/' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$apk = Join-Path $outputRoot 'DamageTextComparison.apk'
$log = Join-Path $outputRoot 'unity-build.log'
$sourceManifest = Join-Path $outputRoot 'sources.sha256'
$sourcePaths = @(& git -C $repositoryRoot ls-files -- .gitattributes .gitignore com.uidatarender com.unity.ugui HUDRenderTestSample/Assets HUDRenderTestSample/Packages HUDRenderTestSample/ProjectSettings Tools)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory build source.' }
$sourceRows = foreach ($relative in $sourcePaths) { "$((Get-FileHash -LiteralPath (Join-Path $repositoryRoot $relative)).Hash)  $relative" }
[IO.File]::WriteAllLines($sourceManifest, $sourceRows, [Text.UTF8Encoding]::new($false))
$previousCommit = $env:DAMAGE_COMPARISON_SOURCE_COMMIT
$nativeLibrary = $null
if ($NativeVulkanProbe -or $NativeGpuTiming) {
    $nativeLibrary = & (Join-Path $PSScriptRoot 'Build-DamageTextNativeGpuTiming.ps1') -OutputDirectory (Join-Path $outputRoot 'native') -UnityEditorDirectory (Split-Path -Parent $UnityExe)
}
try {
    $env:DAMAGE_COMPARISON_SOURCE_COMMIT = $head
    $arguments = @('-batchmode','-quit','-projectPath',('"' + $projectPath + '"'),
        '-executeMethod','DamageTextComparisonBuilder.BuildAndroidBatch','-damageComparisonOutput',('"' + $apk + '"'),
        '-logFile',('"' + $log + '"'))
    if ($FrameTimingProbe) { $arguments += '-damageComparisonTimingProbe' }
    if ($PrecisionProbe) { $arguments += '-damageComparisonPrecisionProbe' }
    if ($ProbeInBenchmarkPackage) { $arguments += '-damageComparisonProbeInBenchmarkPackage' }
    if ($NativeVulkanProbe) { $arguments += @('-damageComparisonNativeVulkanProbe','-damageComparisonNativeLibrary',('"' + $nativeLibrary + '"')) }
    if ($NativeGpuTiming) { $arguments += @('-damageComparisonNativeGpuTiming','-damageComparisonNativeLibrary',('"' + $nativeLibrary + '"')) }
    $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    while (-not $process.WaitForExit(10000)) {
        if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id -Force; throw "Android build timed out. Log: $log" }
        Write-Output "Building Android capture APK, PID $($process.Id). Log: $log"
    }
    if ($process.ExitCode -ne 0) { throw "Android build failed ($($process.ExitCode)). Log: $log" }
}
finally { $env:DAMAGE_COMPARISON_SOURCE_COMMIT = $previousCommit }
if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0) { throw 'Build changed tracked inputs; inspect the isolated checkout.' }
$metadataPath = $apk + '.build.json'
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.backend -ne 'IL2CPP' -or $metadata.api -ne 'Vulkan' -or $metadata.architecture -ne 'ARM64' -or
    $metadata.buildType -ne 'Release' -or $metadata.instancedVulkanCompilerEntries -le 0 -or $metadata.meshVulkanCompilerEntries -le 0) {
    throw 'Build settings or Vulkan compiler-input audit failed.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($apk)
try {
    $libraries = @($archive.Entries.FullName | Where-Object { $_ -like 'lib/*' })
    if ('lib/arm64-v8a/libil2cpp.so' -notin $libraries -or 'lib/arm64-v8a/libunity.so' -notin $libraries -or
        @($libraries | Where-Object { $_ -notlike 'lib/arm64-v8a/*' }).Count -gt 0) { throw 'APK native libraries do not match ARM64 IL2CPP.' }
}
finally { $archive.Dispose() }
$artifact = [ordered]@{
    schema='damage-comparison-android-artifacts-v1'; commit=$head; sourceStatus='clean checkout'
    sourceManifestSHA256=(Get-FileHash -LiteralPath $sourceManifest).Hash
    apkSHA256=(Get-FileHash -LiteralPath $apk).Hash; buildMetadataSHA256=(Get-FileHash -LiteralPath $metadataPath).Hash
    sourceFingerprint=$metadata.sourceFingerprint; buildGuid=$metadata.buildGuid
    nativeLibraries=$libraries; validation='offline build only; no device, GPU timing or Jenkins acceptance'
}
if ($NativeVulkanProbe -or $NativeGpuTiming) {
    if (($NativeVulkanProbe -and -not $metadata.nativeVulkanProbe) -or ($NativeGpuTiming -and -not $metadata.nativeGpuTiming) -or $metadata.nativePluginSHA256 -ne (Get-FileHash -LiteralPath $nativeLibrary).Hash -or
        'lib/arm64-v8a/libDamageTextGpuTiming.so' -notin $libraries) { throw 'Native calibration plugin provenance or APK inclusion failed.' }
    $artifact.nativePluginSHA256=$metadata.nativePluginSHA256
    $artifact.nativeBuildMetadataSHA256=(Get-FileHash -LiteralPath (Join-Path $outputRoot 'native/native-build.json')).Hash
}
$artifact | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'artifacts.json') -Encoding utf8
Write-Output "APK: $apk"
Write-Output "Artifacts: $outputRoot"
