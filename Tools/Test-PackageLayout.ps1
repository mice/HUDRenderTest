[CmdletBinding()]
param(
    [string]$RepositoryRoot = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepositoryRoot "TestResults/Validation/package-layout.json"
}
else {
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
}

$packageRoot = Join-Path $RepositoryRoot "com.uidatarender"
$gitSafeDirectory = $RepositoryRoot.Replace('\', '/')
$sampleRoot = Join-Path $RepositoryRoot "HUDRenderTestSample"
$samplePackagesRoot = Join-Path $sampleRoot "Packages"
$manifestPath = Join-Path $samplePackagesRoot "manifest.json"
$lockPath = Join-Path $samplePackagesRoot "packages-lock.json"
$packageJsonPath = Join-Path $packageRoot "package.json"
$projectVersionPath = Join-Path $sampleRoot "ProjectSettings/ProjectVersion.txt"
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param([string]$Name, [bool]$Passed, [string]$Evidence)

    $checks.Add([pscustomobject]@{
        name = $Name
        passed = $Passed
        evidence = $Evidence
    })
}

function Get-RepositoryRelativePath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPrefix = $RepositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }

    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Test-TrackedDirectory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot ".git"))) {
        return $true
    }

    $relativePath = Get-RepositoryRelativePath -Path $Path
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        return $false
    }

    $trackedFiles = @(& git -c "safe.directory=$gitSafeDirectory" -C $RepositoryRoot ls-files -- "$relativePath/**")
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed while checking $relativePath"
    }

    return $trackedFiles.Count -gt 0
}

$requiredFiles = @(
    $packageJsonPath,
    $manifestPath,
    $lockPath,
    $projectVersionPath,
    (Join-Path $packageRoot "Runtime/UIDataRender/UIDataRender.asmdef"),
    (Join-Path $packageRoot "Runtime/UGUI.Extends/UGui.Extends.asmdef"),
    (Join-Path $packageRoot "Runtime/Support/managedTask/managedTask.asmdef"),
    (Join-Path $packageRoot "Tests/EditMode/TestUIDataRender.asmdef"),
    (Join-Path $sampleRoot "Assets/Tests/PlayMode/TestUIDataRender.PlayMode.asmdef"),
    (Join-Path $packageRoot "Third Party Notices.md"),
    (Join-Path $packageRoot "Third Party Notices.md.meta"),
    (Join-Path $packageRoot "Runtime/Shaders/UIE-AtlasBlit.shader"),
    (Join-Path $packageRoot "Runtime/Shaders/UIE-TMP-SDF.shader"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Resources/TMP Settings.asset"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Sprites/EmojiOne.png"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Fonts/LiberationSans.ttf"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Shaders/TMP_SDF.shader"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader"),
    (Join-Path $packageRoot "Runtime/TextMesh Pro/Shaders/TMP_Sprite.shader"),
    (Join-Path $sampleRoot "Assets/Art/Material/LiberationSans SDF - Outline.mat"),
    (Join-Path $sampleRoot "Assets/Art/Material/LiberationSans SDF - Outline2.mat"),
    (Join-Path $RepositoryRoot "Documentation~/README.md"),
    (Join-Path $RepositoryRoot "Documentation~/Testing/Unit/README.md"),
    (Join-Path $RepositoryRoot "Tools/UnityTestRunner/Test-UnitTestGovernance.ps1")
)
$missingFiles = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
Add-Check "required-files" ($missingFiles.Count -eq 0) ($missingFiles -join ", ")

$sampleExternalizedPaths = @(
    (Join-Path $sampleRoot "CodeCoverage"),
    (Join-Path $sampleRoot "Documentation~"),
    (Join-Path $sampleRoot "Assets/Doc")
)
$samplePathsStillPresent = @($sampleExternalizedPaths | Where-Object { Test-Path -LiteralPath $_ })
Add-Check "sample-output-and-documentation-externalized" `
    ($samplePathsStillPresent.Count -eq 0) `
    ($samplePathsStillPresent -join ", ")

if ($missingFiles.Count -eq 0) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    $package = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json

    Add-Check "package-name" ($package.name -eq "com.uidatarender") ([string]$package.name)
    Add-Check "package-version" ($package.version -match '^\d+\.\d+\.\d+$') ([string]$package.version)
    Add-Check "package-unity-baseline" ($package.unity -eq "2022.3") ([string]$package.unity)

    $requiredDependencies = [ordered]@{
        "com.unity.burst" = "1.8.28"
        "com.unity.textmeshpro" = "3.0.6"
        "com.unity.ugui" = "1.0.0"
    }
    foreach ($dependency in $requiredDependencies.GetEnumerator()) {
        $actualVersion = [string]$package.dependencies.($dependency.Key)
        Add-Check "package-dependency-$($dependency.Key)" `
            ($actualVersion -eq $dependency.Value) `
            "$actualVersion (expected $($dependency.Value))"
    }

    $link = [string]$manifest.dependencies.'com.uidatarender'
    $resolvedLink = ""
    if ($link.StartsWith("file:", [System.StringComparison]::Ordinal)) {
        $resolvedLink = [System.IO.Path]::GetFullPath((Join-Path $samplePackagesRoot $link.Substring(5)))
    }
    Add-Check "local-upm-link" `
        ($link.StartsWith("file:", [System.StringComparison]::Ordinal) -and
         $resolvedLink -eq [System.IO.Path]::GetFullPath($packageRoot)) `
        "$link -> $resolvedLink"
    Add-Check "local-upm-link-tracked" `
        (Test-TrackedDirectory -Path $packageRoot) `
        (Get-RepositoryRelativePath -Path $packageRoot)

    $embeddedEntries = New-Object System.Collections.Generic.List[string]
    $missingEmbeddedDirectories = New-Object System.Collections.Generic.List[string]
    $untrackedEmbeddedDirectories = New-Object System.Collections.Generic.List[string]
    foreach ($property in $lock.dependencies.PSObject.Properties) {
        $entry = $property.Value
        if ([string]$entry.source -ne "embedded") {
            continue
        }

        $embeddedEntries.Add($property.Name)
        $version = [string]$entry.version
        $relativeDirectory = if ($version.StartsWith("file:", [System.StringComparison]::Ordinal)) {
            $version.Substring(5)
        }
        else {
            $property.Name
        }
        $embeddedDirectory = [System.IO.Path]::GetFullPath((Join-Path $samplePackagesRoot $relativeDirectory))
        if (-not (Test-Path -LiteralPath $embeddedDirectory -PathType Container)) {
            $missingEmbeddedDirectories.Add("$($property.Name): $embeddedDirectory")
        }
        elseif (-not (Test-TrackedDirectory -Path $embeddedDirectory)) {
            $untrackedEmbeddedDirectories.Add("$($property.Name): $embeddedDirectory")
        }
    }
    Add-Check "embedded-lock-directories-exist" `
        ($missingEmbeddedDirectories.Count -eq 0) `
        ($missingEmbeddedDirectories -join ", ")
    Add-Check "embedded-lock-directories-tracked" `
        ($untrackedEmbeddedDirectories.Count -eq 0) `
        ($untrackedEmbeddedDirectories -join ", ")

    $samplePackageDirectories = @(Get-ChildItem -LiteralPath $samplePackagesRoot -Directory -Force -ErrorAction SilentlyContinue)
    $untrackedPackageDirectories = @($samplePackageDirectories | Where-Object { -not (Test-TrackedDirectory -Path $_.FullName) })
    Add-Check "sample-package-directories-tracked" `
        ($untrackedPackageDirectories.Count -eq 0) `
        (($untrackedPackageDirectories | Select-Object -ExpandProperty FullName) -join ", ")

    $uguiLock = $lock.dependencies.'com.unity.ugui'
    $uguiSource = [string]$uguiLock.source
    $uguiVersion = [string]$uguiLock.version
    $uguiIsLocalFile = $uguiVersion -like "file:*" -and $uguiVersion -eq "file:../../com.unity.ugui"
    Add-Check "ugui-resolution" `
        (($uguiVersion -eq "1.0.0" -and $uguiSource -ne "embedded" -and $uguiSource -ne "local") -or $uguiIsLocalFile) `
        "version=$uguiVersion; source=$uguiSource"

    $versionLine = Select-String -LiteralPath $projectVersionPath -Pattern '^m_EditorVersion:\s*(?<version>\S+)' | Select-Object -First 1
    $unityVersion = if ($null -ne $versionLine) { $versionLine.Matches[0].Groups["version"].Value } else { "" }
    Add-Check "sample-unity-version" ($unityVersion -eq "2022.3.53f1") $unityVersion
}

$asmdefFiles = @(
    Get-ChildItem -LiteralPath $packageRoot -Recurse -Filter "*.asmdef" -File -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath (Join-Path $sampleRoot "Assets") -Recurse -Filter "*.asmdef" -File -ErrorAction SilentlyContinue
)
$asmdefs = @($asmdefFiles | ForEach-Object {
    $definition = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    $references = if ($definition.PSObject.Properties.Name -contains "references") { @($definition.references) } else { @() }
    $precompiledReferences = if ($definition.PSObject.Properties.Name -contains "precompiledReferences") { @($definition.precompiledReferences) } else { @() }
    [pscustomobject]@{
        path = $_.FullName
        name = [string]$definition.name
        references = $references
        precompiledReferences = $precompiledReferences
    }
})
$duplicateNames = @($asmdefs | Group-Object name | Where-Object Count -gt 1 | Select-Object -ExpandProperty Name)
Add-Check "asmdef-unique-names" ($duplicateNames.Count -eq 0) ($duplicateNames -join ", ")

$requiredAsmdefs = @("UIDataRender", "UGui.Extends", "managedTask", "Tests", "HOT", "Tests.PlayMode")
$missingAsmdefs = @($requiredAsmdefs | Where-Object { $_ -notin $asmdefs.name })
Add-Check "required-asmdefs" ($missingAsmdefs.Count -eq 0) ($missingAsmdefs -join ", ")

$runtimeRoot = Join-Path $packageRoot "Runtime"
$runtimeAsmdefs = @($asmdefs | Where-Object {
    $_.path.StartsWith($runtimeRoot, [System.StringComparison]::OrdinalIgnoreCase) -or $_.name -eq "HOT"
})
$testLeaks = @($runtimeAsmdefs | Where-Object {
    @($_.references | Where-Object { [string]$_ -match 'Test|NUnit' }).Count -gt 0 -or
    @($_.precompiledReferences | Where-Object { [string]$_ -match 'nunit' }).Count -gt 0
} | Select-Object -ExpandProperty name)
Add-Check "runtime-asmdefs-test-free" ($testLeaks.Count -eq 0) ($testLeaks -join ", ")

$runtimeTextFiles = @(Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Extension -in @(".cs", ".asmdef", ".json", ".shader", ".cginc", ".asset", ".prefab", ".unity")
})
$sampleReferences = @($runtimeTextFiles | Select-String -SimpleMatch "HUDRenderTestSample" -ErrorAction SilentlyContinue)
Add-Check "package-runtime-sample-independent" `
    ($sampleReferences.Count -eq 0) `
    (($sampleReferences | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join ", ")

$unityAssetRoots = @(
    (Join-Path $packageRoot "Runtime"),
    (Join-Path $packageRoot "Tests"),
    (Join-Path $sampleRoot "Assets")
)
$missingMetaFiles = New-Object System.Collections.Generic.List[string]
$metaIdentities = New-Object System.Collections.Generic.List[object]
foreach ($assetRoot in $unityAssetRoots) {
    Get-ChildItem -LiteralPath $assetRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object Extension -ne ".meta" |
        ForEach-Object {
            if (-not (Test-Path -LiteralPath ($_.FullName + ".meta") -PathType Leaf)) {
                $missingMetaFiles.Add($_.FullName)
            }
        }
    Get-ChildItem -LiteralPath $assetRoot -Recurse -Directory -ErrorAction SilentlyContinue |
        ForEach-Object {
            if (-not (Test-Path -LiteralPath ($_.FullName + ".meta") -PathType Leaf)) {
                $missingMetaFiles.Add($_.FullName + " [directory]")
            }
        }
    Get-ChildItem -LiteralPath $assetRoot -Recurse -File -Filter "*.meta" -ErrorAction SilentlyContinue |
        ForEach-Object {
            $guidLine = Select-String -LiteralPath $_.FullName -Pattern '^guid:\s*(?<guid>[0-9a-f]{32})$' | Select-Object -First 1
            if ($null -ne $guidLine) {
                $metaIdentities.Add([pscustomobject]@{
                    guid = $guidLine.Matches[0].Groups["guid"].Value
                    path = $_.FullName
                })
            }
        }
}
Add-Check "unity-meta-completeness" ($missingMetaFiles.Count -eq 0) ($missingMetaFiles -join ", ")

$duplicateGuids = @($metaIdentities | Group-Object guid | Where-Object Count -gt 1)
Add-Check "unity-meta-guid-uniqueness" `
    ($duplicateGuids.Count -eq 0) `
    (($duplicateGuids | ForEach-Object { "$($_.Name): $(@($_.Group.path) -join '; ')" }) -join ", ")

$governanceOutputPath = Join-Path (Split-Path -Parent $OutputPath) "test-governance.json"
try {
    & (Join-Path $RepositoryRoot "Tools/UnityTestRunner/Test-UnitTestGovernance.ps1") `
        -ProjectRoot $RepositoryRoot `
        -OutputPath $governanceOutputPath | Out-Null
    Add-Check "test-governance" $true $governanceOutputPath
}
catch {
    Add-Check "test-governance" $false $_.Exception.Message
}

$failedChecks = @($checks | Where-Object { -not $_.passed })
$report = [pscustomobject]@{
    schemaVersion = "1.0"
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    success = $failedChecks.Count -eq 0
    repositoryRoot = $RepositoryRoot
    packagePath = $packageRoot
    samplePath = $sampleRoot
    asmdefCount = $asmdefs.Count
    checks = $checks.ToArray()
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

if ($failedChecks.Count -gt 0) {
    $details = ($failedChecks | ForEach-Object { "$($_.name): $($_.evidence)" }) -join [Environment]::NewLine
    throw "Package/layout validation failed:`n$details`nReport: $OutputPath"
}

Write-Output "UIDataRender package/layout validation passed. Report: $OutputPath"
