[CmdletBinding()]
param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.53f1\Editor\Unity.exe",
    [ValidateSet("EditMode", "PlayMode", "Both")]
    [string]$TestPlatform = "EditMode",
    [int]$TimeoutMinutes = 30,
    [switch]$WithGraphics
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$gitSafeDirectory = $repositoryRoot.Replace('\', '/')
$tempRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "Temp"))
$snapshotRoot = [System.IO.Path]::GetFullPath((Join-Path $tempRoot "CleanValidationSnapshot"))
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "TestResults\CleanValidation"))
$temporaryIndex = [System.IO.Path]::GetFullPath((Join-Path $tempRoot "clean-validation.index"))

function Assert-ChildPath {
    param([string]$Parent, [string]$Child)

    $prefix = $Parent.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $Child.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside $Parent`: $Child"
    }
}

function Invoke-CheckedGit {
    param([string[]]$Arguments)

    & git -c "safe.directory=$gitSafeDirectory" -c core.safecrlf=false -C $repositoryRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git failed: git -C $repositoryRoot $($Arguments -join ' ')"
    }
}

function Invoke-UnityTests {
    param([string]$Platform, [string]$AssemblyName)

    $projectPath = Join-Path $snapshotRoot "HUDRenderTestSample"
    $resultPath = Join-Path $outputRoot "$($Platform.ToLowerInvariant())-results.xml"
    $logPath = Join-Path $outputRoot "unity-$($Platform.ToLowerInvariant()).log"
    $arguments = @(
        "-batchmode",
        "-projectPath", $projectPath,
        "-runTests",
        "-testPlatform", $Platform,
        "-assemblyNames", $AssemblyName,
        "-testResults", $resultPath,
        "-logFile", $logPath
    )
    if ($Platform -eq "PlayMode" -and $WithGraphics) { $arguments += "-force-d3d11" }
    else { $arguments += "-nographics" }

    $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    while (-not $process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) {
            Stop-Process -Id $process.Id -Force
            throw "Unity $Platform validation timed out after $TimeoutMinutes minutes. Log: $logPath"
        }

        Start-Sleep -Seconds 5
        $process.Refresh()
    }

    if ($process.ExitCode -ne 0) {
        throw "Unity $Platform validation failed with exit code $($process.ExitCode). Log: $logPath"
    }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "Unity $Platform validation did not produce results. Log: $logPath"
    }

    [xml]$result = Get-Content -LiteralPath $resultPath
    $run = $result.'test-run'
    if ($null -eq $run -or [string]$run.result -ne "Passed" -or [int]$run.failed -gt 0) {
        throw "Unity $Platform tests failed. Results: $resultPath"
    }

    Write-Output ("Clean {0}: total={1}, passed={2}, failed={3}, skipped={4}" -f `
        $Platform, $run.total, $run.passed, $run.failed, $run.skipped)
}

if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    throw "Unity executable not found: $UnityExe"
}

Assert-ChildPath -Parent $tempRoot -Child $snapshotRoot
Assert-ChildPath -Parent $tempRoot -Child $temporaryIndex
Assert-ChildPath -Parent $repositoryRoot -Child $outputRoot

New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
if (Test-Path -LiteralPath $snapshotRoot) {
    Remove-Item -LiteralPath $snapshotRoot -Recurse -Force
}
if (Test-Path -LiteralPath $temporaryIndex) {
    Remove-Item -LiteralPath $temporaryIndex -Force
}
if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $snapshotRoot | Out-Null
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$previousIndex = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $temporaryIndex
    Invoke-CheckedGit -Arguments @("read-tree", "HEAD")
    Invoke-CheckedGit -Arguments @("add", "-A", "--", ".")

    $prefix = $snapshotRoot.Replace('\', '/').TrimEnd('/') + "/"
    Invoke-CheckedGit -Arguments @("checkout-index", "--all", "--force", "--prefix=$prefix")
}
finally {
    $env:GIT_INDEX_FILE = $previousIndex
    if (Test-Path -LiteralPath $temporaryIndex) {
        Remove-Item -LiteralPath $temporaryIndex -Force
    }
}

$unexpectedInputs = @(@(
    (Join-Path $snapshotRoot "HUDRenderTestSample\Library"),
    (Join-Path $snapshotRoot "HUDRenderTestSample\Packages\com.unity.ugui@1.0.0"),
    (Join-Path $snapshotRoot "Library")
) | Where-Object { Test-Path -LiteralPath $_ })
if ($unexpectedInputs.Count -gt 0) {
    throw "Clean snapshot contains forbidden generated or ignored inputs: $($unexpectedInputs -join ', ')"
}

& (Join-Path $snapshotRoot "Tools\Test-PackageLayout.ps1") `
    -RepositoryRoot $snapshotRoot `
    -OutputPath (Join-Path $outputRoot "package-layout.json") | Out-Host

$platforms = if ($TestPlatform -eq "Both") { @("EditMode", "PlayMode") } else { @($TestPlatform) }
foreach ($platform in $platforms) {
    $assemblyName = if ($platform -eq "EditMode") { "Tests" } else { "Tests.PlayMode" }
    Invoke-UnityTests -Platform $platform -AssemblyName $assemblyName
}

Write-Output "Clean validation passed. Snapshot: $snapshotRoot"
Write-Output "Artifacts: $outputRoot"
