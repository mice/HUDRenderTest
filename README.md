# HUDRenderTest

`HUDRenderTest` is a Unity 2022.3 LTS UPM repository for the UI data batch renderer.

## Layout

- `com.uidatarender/`: reusable package runtime, tests, shaders, TMP resources, and package documentation.
- `HUDRenderTestSample/`: Unity sample project that references the package through a local `file:` dependency.
- `Documentation~/`: repository and Sample documentation, including PlayMode governance records and historical single-project notes.
- `Tools/`: repository validation and governance scripts.

Open `HUDRenderTestSample/` with Unity 2022.3.53f1. Its manifest links `com.uidatarender` with `file:../../com.uidatarender`; all other package dependencies resolve from their declared Unity sources.

## Validation

Run these commands from the repository root:

```powershell
.\Tools\Test-PackageLayout.ps1
.\Tools\Invoke-LocalValidation.ps1
.\Tools\Invoke-CleanValidation.ps1 -TestPlatform Both
.\Tools\Invoke-LocalValidation.ps1 -WithCoverage -CoverageThreshold 70
```

The working-tree flow writes logs and NUnit XML under `HUDRenderTestSample/Logs`, and coverage under `CodeCoverage/Local`. Clean validation exports the current repository-trackable state to `Temp/CleanValidationSnapshot`, starts without a Unity `Library` or ignored package directory, and writes durable artifacts under `TestResults/CleanValidation`.

After adding, renaming, or moving governed tests, run:

```powershell
.\Tools\UnityTestRunner\Sync-UnitTestGovernance.ps1 -Scope All
```

Package EditMode records live under `com.uidatarender/Documentation~/Testing/Unit`; Sample PlayMode records live under the repository-level `Documentation~/Testing/Unit`.
