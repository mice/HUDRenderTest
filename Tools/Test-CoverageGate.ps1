[CmdletBinding()]
param(
    [string]$SummaryPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "CodeCoverage\Local\Report\Summary.xml"),
    [string]$AssemblyName = "UIDataRender",
    [double]$MinimumLineCoverage = 70
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SummaryPath)) {
    throw "Coverage summary not found: $SummaryPath"
}

[xml]$xml = Get-Content -LiteralPath $SummaryPath
$assembly = @($xml.CoverageReport.Coverage.Assembly | Where-Object { $_.name -eq $AssemblyName })

if ($assembly.Count -ne 1) {
    throw "Expected exactly one assembly named '$AssemblyName' in $SummaryPath, found $($assembly.Count)."
}

$lineCoverage = [double]$assembly[0].coverage
$coveredLines = [int]$assembly[0].coveredlines
$coverableLines = [int]$assembly[0].coverablelines

Write-Host ("Coverage gate: assembly={0}, lineCoverage={1:N1}%, coveredLines={2}, coverableLines={3}, threshold={4:N1}%" -f `
    $AssemblyName,
    $lineCoverage,
    $coveredLines,
    $coverableLines,
    $MinimumLineCoverage)

if ($lineCoverage -lt $MinimumLineCoverage) {
    throw ("Coverage gate failed for {0}: {1:N1}% < {2:N1}%." -f $AssemblyName, $lineCoverage, $MinimumLineCoverage)
}
