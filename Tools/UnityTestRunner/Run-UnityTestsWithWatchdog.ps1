param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe",
    [ValidateSet("EditMode", "PlayMode", "Both")]
    [string]$TestPlatform = "EditMode",
    [string]$EditModeAssemblies = "Tests",
    [string]$PlayModeAssemblies = "Tests.PlayMode",
    [string]$CoverageAssemblyFilters = "+UIDataRender,+UGui.Extends,+HOT,+managedTask",
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments, [string]$WorkingDirectory) {
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -PassThru -NoNewWindow
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)

    while (-not $process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) {
            Stop-Process -Id $process.Id -Force
            throw "Timed out after $TimeoutMinutes minutes: $FilePath $($Arguments -join ' ')"
        }

        Write-Host ("[{0:HH:mm:ss}] waiting for pid {1}: {2}" -f [DateTime]::Now, $process.Id, $FilePath)
        Start-Sleep -Seconds 15
        $process.Refresh()
    }

    if ($process.ExitCode -ne 0) {
        throw "Process failed with exit code $($process.ExitCode): $FilePath $($Arguments -join ' ')"
    }
}

function Invoke-UnityTests([string]$Platform, [string]$AssemblyNames, [string]$CloneRoot, [string]$OutputRoot) {
    $logPath = Join-Path $OutputRoot "unity-$($Platform.ToLowerInvariant()).log"
    $resultsPath = Join-Path $OutputRoot "$($Platform.ToLowerInvariant())-results.xml"
    $coveragePath = Join-Path $OutputRoot "Coverage\$Platform"
    New-Item -ItemType Directory -Force -Path $coveragePath | Out-Null

    $coverageOptions = "generateAdditionalMetrics;generateHtmlReport;generateBadgeReport;assemblyFilters:$CoverageAssemblyFilters"
    $args = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", $CloneRoot,
        "-runTests",
        "-testPlatform", $Platform,
        "-assemblyNames", $AssemblyNames,
        "-testResults", $resultsPath,
        "-logFile", $logPath,
        "-enableCodeCoverage",
        "-coverageResultsPath", $coveragePath,
        "-coverageOptions", $coverageOptions
    )

    Invoke-CheckedProcess -FilePath $UnityPath -Arguments $args -WorkingDirectory $CloneRoot
}

$projectRootFull = Resolve-FullPath $ProjectRoot
$tempRoot = Resolve-FullPath (Join-Path $projectRootFull "Temp")
$cloneRoot = Resolve-FullPath (Join-Path $tempRoot "UnityTestClone")
$outputRoot = Resolve-FullPath (Join-Path $projectRootFull "TestResults\UnityTestRunner")

if (-not (Test-Path $UnityPath)) {
    throw "Unity executable not found: $UnityPath"
}

New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

if (-not $cloneRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Clone path is outside Temp: $cloneRoot"
}

if (Test-Path $cloneRoot) {
    Remove-Item -LiteralPath $cloneRoot -Recurse -Force
}

Invoke-CheckedProcess -FilePath "git" -Arguments @("clone", "--no-local", $projectRootFull, $cloneRoot) -WorkingDirectory $projectRootFull

$platforms = if ($TestPlatform -eq "Both") { @("EditMode", "PlayMode") } else { @($TestPlatform) }
foreach ($platform in $platforms) {
    $assemblies = if ($platform -eq "EditMode") { $EditModeAssemblies } else { $PlayModeAssemblies }
    Invoke-UnityTests -Platform $platform -AssemblyNames $assemblies -CloneRoot $cloneRoot -OutputRoot $outputRoot
}

Write-Host "Unity test results written to $outputRoot"
