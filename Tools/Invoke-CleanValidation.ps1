[CmdletBinding()]
param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.53f1\Editor\Unity.exe",
    [ValidateSet("EditMode", "PlayMode", "Both")]
    [string]$TestPlatform = "EditMode",
    [int]$TimeoutMinutes = 30,
    [switch]$WithGraphics,
    [string]$SnapshotRoot = "D:\workspace\HUDRenderPackageValidation",
    [string]$TestFilter = "",
    [switch]$PrepareOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$gitSafeDirectory = $repositoryRoot.Replace('\', '/')
$snapshotRoot = [System.IO.Path]::GetFullPath($SnapshotRoot)
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "TestResults\CleanValidation"))
$temporaryIndex = Join-Path $outputRoot "clean-validation.index"
$ownerPath = Join-Path $snapshotRoot ".hudrender-validation.json"
$projectPath = Join-Path $snapshotRoot "HUDRenderTestSample"

function Assert-ChildPath {
    param([string]$Parent, [string]$Child)

    $prefix = $Parent.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $Child.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside $Parent`: $Child"
    }
}

function Write-Json {
    param([object]$Value, [string]$Path)
    $json = $Value | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
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

    $suffix = if ($TestFilter) { "-filtered" } else { "" }
    $resultPath = Join-Path $outputRoot "$($Platform.ToLowerInvariant())$suffix-results.xml"
    $logPath = Join-Path $outputRoot "unity-$($Platform.ToLowerInvariant())$suffix.log"
    # Remove only this runner's previous output so stale XML cannot pass a new run.
    foreach ($path in @($resultPath, $logPath, (Join-Path $projectPath "Library/StockUguiProvenance.json"))) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    $arguments = @(
        "-batchmode",
        "-projectPath", $projectPath,
        "-runTests",
        "-testPlatform", $Platform,
        "-assemblyNames", $AssemblyName,
        "-testResults", $resultPath,
        "-logFile", $logPath
    )
    if ($TestFilter) { $arguments += @("-testFilter", $TestFilter) }
    if ($Platform -eq "PlayMode" -and $WithGraphics) { $arguments += "-force-d3d11" }
    else { $arguments += "-nographics" }

    $quotedArguments = $arguments | ForEach-Object { '"' + $_ + '"' }
    $process = Start-Process -FilePath $UnityExe -ArgumentList $quotedArguments -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    while (-not $process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) {
            Stop-Process -Id $process.Id -Force
            throw "Unity $Platform validation timed out after $TimeoutMinutes minutes. Log: $logPath"
        }

        Start-Sleep -Seconds 5
        $process.Refresh()
    }
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        throw "Unity $Platform validation failed with exit code $($process.ExitCode). Log: $logPath"
    }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "Unity $Platform validation did not produce results. Log: $logPath"
    }

    [xml]$result = Get-Content -LiteralPath $resultPath
    $run = $result.'test-run'
    if ($null -eq $run -or [string]$run.result -ne "Passed" -or
        [int]$run.total -le 0 -or [int]$run.passed -le 0 -or
        [int]$run.failed -gt 0 -or [int]$run.skipped -gt 0 -or [int]$run.inconclusive -gt 0) {
        throw "Unity $Platform tests failed. Results: $resultPath"
    }

    $provenancePath = Join-Path $projectPath "Library/StockUguiProvenance.json"
    if (-not (Test-Path -LiteralPath $provenancePath)) { throw "Missing resolved UGUI provenance: $provenancePath" }
    $provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
    $resolvedLock = Get-Content -LiteralPath (Join-Path $projectPath "Packages/packages-lock.json") -Raw | ConvertFrom-Json
    $ugui = $resolvedLock.dependencies.'com.unity.ugui'
    if ($provenance.version -ne "1.0.0" -or $provenance.hasCustomCopyIndices -or
        $provenance.source -notin @("BuiltIn", "Registry") -or
        $ugui.version -ne "1.0.0" -or $ugui.source -notin @("builtin", "registry")) {
        throw "Resolved UGUI is not stock 1.0.0. Provenance: $provenancePath"
    }
    Copy-Item -LiteralPath $provenancePath -Destination (Join-Path $outputRoot "ugui-provenance.json") -Force
    Copy-Item -LiteralPath (Join-Path $projectPath "Packages/packages-lock.json") -Destination (Join-Path $outputRoot "resolved-packages-lock.json") -Force

    Write-Output ("Clean {0}: total={1}, passed={2}, failed={3}, skipped={4}" -f `
        $Platform, $run.total, $run.passed, $run.failed, $run.skipped)
}

if (-not $PrepareOnly -and -not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    throw "Unity executable not found: $UnityExe"
}

Assert-ChildPath -Parent "D:\workspace" -Child $snapshotRoot
Assert-ChildPath -Parent $outputRoot -Child $temporaryIndex
Assert-ChildPath -Parent $repositoryRoot -Child $outputRoot

if (Test-Path -LiteralPath $snapshotRoot) {
    if (-not (Test-Path -LiteralPath $ownerPath -PathType Leaf)) {
        throw "Refusing to overwrite a pre-existing unowned snapshot: $snapshotRoot"
    }
    $owner = Get-Content -LiteralPath $ownerPath -Raw | ConvertFrom-Json
    if ($owner.repositoryRoot -ne $repositoryRoot) { throw "Snapshot belongs to another repository: $snapshotRoot" }
}
else {
    Invoke-CheckedGit -Arguments @("worktree", "add", "--detach", "--no-checkout", $snapshotRoot, "HEAD")
    & git -c "safe.directory=$($snapshotRoot.Replace('\', '/'))" -C $snapshotRoot read-tree HEAD
    if ($LASTEXITCODE -ne 0) { throw "Cannot initialize validation worktree index: $snapshotRoot" }
    Write-Json -Path $ownerPath -Value @{ repositoryRoot = $repositoryRoot; exportedFiles = @() }
}
$owner = Get-Content -LiteralPath $ownerPath -Raw | ConvertFrom-Json
if (Test-Path -LiteralPath (Join-Path $projectPath "Temp/UnityLockfile")) {
    throw "Snapshot has a Unity lock; close its Editor before refreshing: $projectPath"
}

# Upgrade this runner's earlier export in place, retaining its imported Library.
# Sample report tests require real Git provenance when run outside the source repo.
if (-not (Test-Path -LiteralPath (Join-Path $snapshotRoot ".git"))) {
    $stagingRoot = [System.IO.Path]::GetFullPath($snapshotRoot + ".source-export")
    Assert-ChildPath -Parent "D:\workspace" -Child $stagingRoot
    if (Test-Path -LiteralPath $stagingRoot) { throw "Refusing to overwrite migration staging: $stagingRoot" }
    Move-Item -LiteralPath $snapshotRoot -Destination $stagingRoot
    Invoke-CheckedGit -Arguments @("worktree", "add", "--detach", "--no-checkout", $snapshotRoot, "HEAD")
    & git -c "safe.directory=$($snapshotRoot.Replace('\', '/'))" -C $snapshotRoot read-tree HEAD
    if ($LASTEXITCODE -ne 0) { throw "Cannot initialize validation worktree index; export retained at $stagingRoot" }
    foreach ($entry in Get-ChildItem -LiteralPath $stagingRoot -Force) {
        Move-Item -LiteralPath $entry.FullName -Destination $snapshotRoot
    }
    Remove-Item -LiteralPath $stagingRoot
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
if (Test-Path -LiteralPath $temporaryIndex) { throw "Export index already exists; inspect before retrying: $temporaryIndex" }
$hadLibrary = Test-Path -LiteralPath (Join-Path $projectPath "Library")

$previousIndex = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $temporaryIndex
    Invoke-CheckedGit -Arguments @("read-tree", "HEAD")
    Invoke-CheckedGit -Arguments @("add", "-A", "--", ".")
    # Exclude the tracked custom dependency from this temporary index only.
    Invoke-CheckedGit -Arguments @("rm", "-r", "--cached", "--quiet", "--ignore-unmatch", "--", "com.unity.ugui")

    $exportedFiles = @(Invoke-CheckedGit -Arguments @("ls-files"))
    foreach ($oldFile in @($owner.exportedFiles)) {
        if ($oldFile -notin $exportedFiles) {
            $oldPath = [System.IO.Path]::GetFullPath((Join-Path $snapshotRoot $oldFile))
            Assert-ChildPath -Parent $snapshotRoot -Child $oldPath
            if (Test-Path -LiteralPath $oldPath -PathType Leaf) { Remove-Item -LiteralPath $oldPath -Force }
        }
    }
    $prefix = $snapshotRoot.Replace('\', '/').TrimEnd('/') + "/"
    Invoke-CheckedGit -Arguments @("checkout-index", "--all", "--force", "--prefix=$prefix")
    $sourceTree = Invoke-CheckedGit -Arguments @("write-tree")
    Write-Json -Path $ownerPath -Value @{ repositoryRoot = $repositoryRoot; exportedFiles = $exportedFiles; sourceTree = $sourceTree }
}
finally {
    $env:GIT_INDEX_FILE = $previousIndex
    if (Test-Path -LiteralPath $temporaryIndex) {
        Remove-Item -LiteralPath $temporaryIndex -Force
    }
}

$customUguiSnapshot = [System.IO.Path]::GetFullPath((Join-Path $snapshotRoot "com.unity.ugui"))
Assert-ChildPath -Parent $snapshotRoot -Child $customUguiSnapshot
if (Test-Path -LiteralPath $customUguiSnapshot) {
    # Owned snapshot files from earlier preparations; never the source dependency.
    Remove-Item -LiteralPath $customUguiSnapshot -Recurse -Force
}

$unexpectedInputs = @(@(
    (Join-Path $snapshotRoot "HUDRenderTestSample\Packages\com.unity.ugui@1.0.0"),
    (Join-Path $snapshotRoot "com.unity.ugui")
) | Where-Object { Test-Path -LiteralPath $_ })
if ($unexpectedInputs.Count -gt 0) {
    throw "Clean snapshot contains forbidden generated or ignored inputs: $($unexpectedInputs -join ', ')"
}

# The exported consumer resolves stock UGUI; source Sample overrides stay untouched.
$manifestPath = Join-Path $projectPath "Packages/manifest.json"
$lockPath = Join-Path $projectPath "Packages/packages-lock.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$manifest.dependencies.'com.unity.ugui' = "1.0.0"
foreach ($optionalTool in @("com.unitymcp.agent-bridge", "com.unitymcp.plugin-abstractions")) {
    $manifest.dependencies.PSObject.Properties.Remove($optionalTool)
    $lock.dependencies.PSObject.Properties.Remove($optionalTool)
}
$lock.dependencies.'com.unity.ugui' = [pscustomobject]@{
    version = "1.0.0"; depth = 0; source = "builtin"
    dependencies = @{ "com.unity.modules.ui" = "1.0.0"; "com.unity.modules.imgui" = "1.0.0" }
}
Write-Json -Value $manifest -Path $manifestPath
Write-Json -Value $lock -Path $lockPath
$probeDirectory = Join-Path $projectPath "Assets/Editor"
New-Item -ItemType Directory -Force -Path $probeDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "UnityTestRunner/StockDependencyProvenance.cs") `
    -Destination (Join-Path $probeDirectory "StockDependencyProvenance.cs") -Force
[System.IO.File]::WriteAllText((Join-Path $probeDirectory "StockDependencyProvenance.cs.meta"),
    "fileFormatVersion: 2`nguid: 48dad15cef574ccba6f3214badca6bf7`n", [System.Text.UTF8Encoding]::new($false))
$packageHashes = @(Get-ChildItem -LiteralPath (Join-Path $snapshotRoot "com.uidatarender") -Recurse -File |
    Sort-Object FullName | ForEach-Object {
        @{ path = $_.FullName.Substring($snapshotRoot.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
Write-Json -Path (Join-Path $outputRoot "source-snapshot.json") -Value @{
    sourceTree = $sourceTree; repositoryRoot = $repositoryRoot; snapshotRoot = $snapshotRoot
    preparedUtc = [DateTime]::UtcNow.ToString("o"); reusedLibrary = $hadLibrary
    requestedUgui = "1.0.0"; dependencyResolutionVerified = $false; packageFiles = $packageHashes
}

& (Join-Path $snapshotRoot "Tools\Test-PackageLayout.ps1") `
    -RepositoryRoot $snapshotRoot `
    -OutputPath (Join-Path $outputRoot "package-layout.json") | Out-Host

if ($PrepareOnly) {
    Write-Output "Stock snapshot prepared; Unity has not been launched. Project: $projectPath"
    Write-Output "Source hashes and requested dependencies: $outputRoot/source-snapshot.json"
    return
}

$platforms = if ($TestPlatform -eq "Both") { @("EditMode", "PlayMode") } else { @($TestPlatform) }
foreach ($platform in $platforms) {
    $assemblyName = if ($platform -eq "EditMode") { "Tests" } else { "Tests.PlayMode" }
    Invoke-UnityTests -Platform $platform -AssemblyName $assemblyName
}

Write-Output "Clean validation passed. Snapshot: $snapshotRoot"
Write-Output "Artifacts: $outputRoot"
