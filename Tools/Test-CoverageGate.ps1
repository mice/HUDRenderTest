[CmdletBinding()]
param(
    [string]$SummaryPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "CodeCoverage\Local\Report\Summary.xml"),
    [string[]]$AssemblyName = @(),
    [double]$MinimumLineCoverage = 70,
    [hashtable]$AssemblyThreshold = @{}
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SummaryPath)) {
    throw "Coverage summary not found: $SummaryPath"
}

[xml]$xml = Get-Content -LiteralPath $SummaryPath

if ($AssemblyThreshold.Count -eq 0) {
    if ($AssemblyName.Count -eq 0) {
        $AssemblyThreshold = @{
            "UIDataRender" = 70
            "UGui.Extends" = 70
            "managedTask" = 70
            "HOT" = 10
        }
    } else {
        foreach ($name in $AssemblyName) {
            $AssemblyThreshold[$name] = $MinimumLineCoverage
        }
    }
}

$failures = New-Object System.Collections.Generic.List[string]
foreach ($entry in $AssemblyThreshold.GetEnumerator()) {
    $targetAssemblyName = [string]$entry.Key
    $minimumCoverage = [double]$entry.Value
    $assembly = @($xml.CoverageReport.Coverage.Assembly | Where-Object { $_.name -eq $targetAssemblyName })

    if ($assembly.Count -ne 1) {
        throw "Expected exactly one assembly named '$targetAssemblyName' in $SummaryPath, found $($assembly.Count)."
    }

    $lineCoverage = [double]$assembly[0].coverage
    $coveredLines = [int]$assembly[0].coveredlines
    $coverableLines = [int]$assembly[0].coverablelines

    Write-Host ("Coverage gate: assembly={0}, lineCoverage={1:N1}%, coveredLines={2}, coverableLines={3}, threshold={4:N1}%" -f `
        $targetAssemblyName,
        $lineCoverage,
        $coveredLines,
        $coverableLines,
        $minimumCoverage)

    if ($lineCoverage -lt $minimumCoverage) {
        $failures.Add(("{0}: {1:N1}% < {2:N1}%" -f $targetAssemblyName, $lineCoverage, $minimumCoverage))
    }
}

if ($failures.Count -gt 0) {
    throw ("Coverage gate failed: {0}" -f ($failures -join "; "))
}
