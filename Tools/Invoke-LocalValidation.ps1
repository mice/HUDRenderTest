[CmdletBinding()]
param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$SkipEditMode,
    [switch]$SkipPlayMode,
    [switch]$WithCoverage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Invoke-UnityTestRun {
    param(
        [string]$Name,
        [string[]]$Arguments,
        [string]$ResultPath,
        [string]$LogPath
    )

    Remove-PathIfExists -Path $ResultPath
    Remove-PathIfExists -Path $LogPath

    Write-Host "==> $Name"
    & $script:UnityExe @Arguments
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode. See $LogPath"
    }

    $run = $null
    for ($i = 0; $i -lt 60 -and $null -eq $run; $i++) {
        if (Test-Path -LiteralPath $ResultPath) {
            try {
                [xml]$xml = Get-Content -LiteralPath $ResultPath
                $run = $xml.'test-run'
            }
            catch {
                $run = $null
            }
        }

        if ($null -eq $run) {
            Start-Sleep -Seconds 1
        }
    }

    if ($null -eq $run) {
        throw "$Name produced an invalid NUnit XML: $ResultPath"
    }

    $summary = [pscustomobject]@{
        Name       = $Name
        Result     = [string]$run.result
        Total      = [int]$run.total
        Passed     = [int]$run.passed
        Failed     = [int]$run.failed
        Skipped    = [int]$run.skipped
        ResultPath = $ResultPath
        LogPath    = $LogPath
    }

    Write-Host ("{0}: total={1}, passed={2}, failed={3}, skipped={4}, result={5}" -f `
        $summary.Name,
        $summary.Total,
        $summary.Passed,
        $summary.Failed,
        $summary.Skipped,
        $summary.Result)

    return $summary
}

if (-not (Test-Path -LiteralPath $UnityExe)) {
    throw "Unity executable not found: $UnityExe"
}

$logsPath = Join-Path $ProjectPath "Logs"
$coveragePath = Join-Path $ProjectPath "CodeCoverage\Local"

New-Item -ItemType Directory -Force -Path $logsPath | Out-Null

$summaries = @()

if (-not $SkipEditMode) {
    $editResultPath = Join-Path $logsPath "EditModeRepoTests.xml"
    $editLogPath = Join-Path $logsPath "EditModeRepoTests.log"
    $editArgs = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $ProjectPath,
        "-runTests",
        "-testPlatform", "EditMode",
        "-assemblyNames", "Tests",
        "-testResults", $editResultPath,
        "-logFile", $editLogPath
    )

    if ($WithCoverage) {
        Remove-PathIfExists -Path $coveragePath
        New-Item -ItemType Directory -Force -Path $coveragePath | Out-Null
        $editArgs += @(
            "-debugCodeOptimization",
            "-enableCodeCoverage",
            "-coverageResultsPath", $coveragePath,
            "-coverageOptions", "generateAdditionalMetrics;generateHtmlReport;assemblyFilters:+UIDataRender"
        )
    }

    $summaries += Invoke-UnityTestRun -Name "EditMode repo tests" -Arguments $editArgs -ResultPath $editResultPath -LogPath $editLogPath
}

if (-not $SkipPlayMode) {
    $playResultPath = Join-Path $logsPath "PlayMode_TestsPlayMode.xml"
    $playLogPath = Join-Path $logsPath "PlayMode_TestsPlayMode.log"
    $playArgs = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $ProjectPath,
        "-runTests",
        "-testPlatform", "PlayMode",
        "-assemblyNames", "Tests.PlayMode",
        "-testResults", $playResultPath,
        "-logFile", $playLogPath
    )

    $summaries += Invoke-UnityTestRun -Name "PlayMode repo tests" -Arguments $playArgs -ResultPath $playResultPath -LogPath $playLogPath
}

if ($WithCoverage) {
    $coverageArtifacts = @(
        Get-ChildItem -Path $coveragePath -Recurse -File -Filter "index.htm*" -ErrorAction SilentlyContinue
        Get-ChildItem -Path $coveragePath -Recurse -File -Filter "Summary.xml" -ErrorAction SilentlyContinue
        Get-ChildItem -Path $coveragePath -Recurse -File | Where-Object { $_.Name -like "*OpenCover*.xml" }
    ) | Where-Object { $null -ne $_ } | Select-Object -Unique

    if ($coverageArtifacts.Count -eq 0) {
        throw "Coverage was requested but no HTML/OpenCover artifacts were found under $coveragePath"
    }

    Write-Host "Coverage artifacts:"
    foreach ($artifact in $coverageArtifacts) {
        Write-Host "  $($artifact.FullName)"
    }
}

Write-Host ""
Write-Host "Artifacts:"
foreach ($summary in $summaries) {
    Write-Host ("  {0}: {1}" -f $summary.ResultPath, $summary.Result)
    Write-Host ("  {0}" -f $summary.LogPath)
}

if ($WithCoverage) {
    Write-Host ("  {0}" -f $coveragePath)
}
