[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ProjectRoot "TestResults/Validation/test-governance.json"
}

$scopes = @(
    [pscustomobject]@{
        Name = "Package"
        ContentRoot = Join-Path $ProjectRoot "com.uidatarender"
        TestRoot = Join-Path $ProjectRoot "com.uidatarender/Tests"
        RecordRoot = Join-Path $ProjectRoot "com.uidatarender/Documentation~/Testing/Unit"
    },
    [pscustomobject]@{
        Name = "Sample"
        ContentRoot = Join-Path $ProjectRoot "HUDRenderTestSample"
        TestRoot = Join-Path $ProjectRoot "HUDRenderTestSample/Assets/Tests/PlayMode"
        RecordRoot = Join-Path $ProjectRoot "HUDRenderTestSample/Documentation~/Testing/Unit"
    }
)

function Get-ScopeRelativePath {
    param([string]$Root, [string]$Path)

    $rootPrefix = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }
    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

$tests = New-Object System.Collections.Generic.List[object]
$records = New-Object System.Collections.Generic.List[object]
$errors = New-Object System.Collections.Generic.List[string]

foreach ($scope in $scopes) {
    $testFiles = @(Get-ChildItem -LiteralPath $scope.TestRoot -Recurse -File -Filter "*.cs")
    foreach ($file in $testFiles) {
        $lines = @(Get-Content -LiteralPath $file.FullName)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -notmatch '^\s*\[(Test|UnityTest)\]') {
                continue
            }

            $categoryId = $null
            $methodName = $null
            for ($j = $i + 1; $j -lt [Math]::Min($i + 10, $lines.Count); $j++) {
                if ($lines[$j] -match '\[Category\("(?<id>UT_[A-Z]+_\d{3})"\)\]') {
                    $categoryId = $matches["id"]
                }
                if ($lines[$j] -match '\b(public|private|internal)\s+(?:unsafe\s+)?(?:IEnumerator|void)\s+(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\(') {
                    $methodName = $matches["method"]
                    break
                }
            }

            $recordReference = $null
            for ($j = [Math]::Max(0, $i - 4); $j -lt $i; $j++) {
                if ($lines[$j] -match 'TestRecord:\s*(?<path>\S.*?\.md)\s*$') {
                    $recordReference = $matches["path"].Trim()
                    break
                }
            }

            if ([string]::IsNullOrWhiteSpace($categoryId)) {
                $errors.Add("Missing governed Category near $($file.FullName):$($i + 1)")
                continue
            }
            if ([string]::IsNullOrWhiteSpace($methodName)) {
                $errors.Add("Missing test method near $($file.FullName):$($i + 1)")
                continue
            }
            if ([string]::IsNullOrWhiteSpace($recordReference)) {
                $errors.Add("Missing TestRecord for $categoryId in $($file.FullName)")
                continue
            }

            $resolvedRecord = [System.IO.Path]::GetFullPath((Join-Path $scope.ContentRoot $recordReference))
            $recordRootPrefix = [System.IO.Path]::GetFullPath($scope.RecordRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
            if (-not $resolvedRecord.StartsWith($recordRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $errors.Add("$categoryId record escapes $($scope.Name) governance root: $recordReference")
            }
            elseif (-not (Test-Path -LiteralPath $resolvedRecord -PathType Leaf)) {
                $errors.Add("$categoryId record does not exist: $resolvedRecord")
            }

            $tests.Add([pscustomobject]@{
                Id = $categoryId
                Scope = $scope.Name
                File = Get-ScopeRelativePath -Root $scope.ContentRoot -Path $file.FullName
                Method = $methodName
                RecordPath = $resolvedRecord
            })
        }
    }

    $recordFiles = @(Get-ChildItem -LiteralPath $scope.RecordRoot -Recurse -File -Filter "UT_*.md")
    foreach ($recordFile in $recordFiles) {
        $content = Get-Content -LiteralPath $recordFile.FullName -Raw
        $idMatch = [regex]::Match($content, '(?m)^testId:\s*(?<value>UT_[A-Z]+_\d{3})\s*$')
        $fileMatch = [regex]::Match($content, '(?m)^testFile:\s*(?<value>\S.*?)\s*$')
        $methodMatch = [regex]::Match($content, '(?m)^testMethod:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)\s*$')
        $statusMatch = [regex]::Match($content, '(?m)^status:\s*(?<value>\S+)\s*$')
        if (-not ($idMatch.Success -and $fileMatch.Success -and $methodMatch.Success -and $statusMatch.Success)) {
            $errors.Add("Invalid governance frontmatter: $($recordFile.FullName)")
            continue
        }
        if ($statusMatch.Groups["value"].Value -ne "active") {
            continue
        }

        $testFile = $fileMatch.Groups["value"].Value
        $resolvedTestFile = [System.IO.Path]::GetFullPath((Join-Path $scope.ContentRoot $testFile))
        if (-not (Test-Path -LiteralPath $resolvedTestFile -PathType Leaf)) {
            $errors.Add("Record testFile does not resolve: $($recordFile.FullName) -> $testFile")
        }

        $records.Add([pscustomobject]@{
            Id = $idMatch.Groups["value"].Value
            Scope = $scope.Name
            File = $testFile
            Method = $methodMatch.Groups["value"].Value
            Path = $recordFile.FullName
        })
    }
}

$duplicateTestIds = @($tests | Group-Object Id | Where-Object Count -ne 1)
foreach ($duplicate in $duplicateTestIds) {
    $errors.Add("Governed test ID occurs $($duplicate.Count) times: $($duplicate.Name)")
}
$duplicateRecordIds = @($records | Group-Object Id | Where-Object Count -ne 1)
foreach ($duplicate in $duplicateRecordIds) {
    $errors.Add("Active record ID occurs $($duplicate.Count) times: $($duplicate.Name)")
}

foreach ($test in $tests) {
    $matchingRecords = @($records | Where-Object Id -eq $test.Id)
    if ($matchingRecords.Count -ne 1) {
        $errors.Add("$($test.Id) has $($matchingRecords.Count) active records")
        continue
    }
    $record = $matchingRecords[0]
    if ($record.Scope -ne $test.Scope -or $record.File -ne $test.File -or $record.Method -ne $test.Method) {
        $errors.Add("$($test.Id) record contract differs from source test")
    }
    if ([System.IO.Path]::GetFullPath($record.Path) -ne [System.IO.Path]::GetFullPath($test.RecordPath)) {
        $errors.Add("$($test.Id) TestRecord comment points to a different record")
    }
}

foreach ($record in $records) {
    if (@($tests | Where-Object Id -eq $record.Id).Count -ne 1) {
        $errors.Add("$($record.Id) active record does not map to exactly one source test")
    }
}

$report = [pscustomobject]@{
    schemaVersion = "1.0"
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    success = $errors.Count -eq 0
    testCount = $tests.Count
    recordCount = $records.Count
    errors = $errors.ToArray()
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

if ($errors.Count -gt 0) {
    throw "Unit test governance validation failed:`n$($errors -join [Environment]::NewLine)`nReport: $OutputPath"
}

Write-Output "Unit test governance passed: $($tests.Count) tests, $($records.Count) records. Report: $OutputPath"
