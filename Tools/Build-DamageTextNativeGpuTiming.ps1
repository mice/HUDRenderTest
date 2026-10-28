[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$UnityEditorDirectory = 'C:/Program Files/Unity/Hub/Editor/2022.3.53f1/Editor'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$ndk=Join-Path $UnityEditorDirectory 'Data/PlaybackEngines/AndroidPlayer/NDK'
$headers=Join-Path $UnityEditorDirectory 'Data/PluginAPI'
$compiler=Join-Path $ndk 'toolchains/llvm/prebuilt/windows-x86_64/bin/clang++.exe'
$source=Join-Path $PSScriptRoot 'NativeGpuTiming/DamageTextGpuTiming.cpp'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use a fresh native build directory.'}
New-Item -ItemType Directory -Path $output | Out-Null
$library=Join-Path $output 'libDamageTextGpuTiming.so'
$arguments=@('--target=aarch64-linux-android30','-std=c++17','-O2','-fPIC','-shared','-fvisibility=hidden',
    '-fno-exceptions','-fno-rtti','-static-libstdc++','-Wall','-Wextra','-Werror',
    '-Wl,--no-undefined','-Wl,-z,max-page-size=16384',('-I'+$headers),$source,'-o',$library)
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'Native Vulkan timestamp compilation failed.'}
$inputs=@($source,$compiler,(Join-Path $ndk 'source.properties'))
$inputs+=@('IUnityInterface.h','IUnityGraphics.h','IUnityGraphicsVulkan.h') | ForEach-Object { Join-Path $headers $_ }
[ordered]@{schema='damage-native-gpu-build-v1';architecture='ARM64';androidApi=30;librarySHA256=(Get-FileHash -LiteralPath $library).Hash
    arguments=$arguments;compilerVersion=((& $compiler --version) -join "`n");inputs=@($inputs | ForEach-Object {@{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash}})
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'native-build.json') -Encoding utf8
Write-Output $library
