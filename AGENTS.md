# Repository Guidelines

## Project Structure & Module Organization
This is a Unity `2022.3.53f1` UPM repository. Reusable runtime source, shaders, and support code live under `com.uidatarender/Runtime`; deterministic package tests live under `com.uidatarender/Tests/EditMode`. The sibling `HUDRenderTestSample` project owns scenes, prefabs, art, sample/performance drivers under `Assets/Scripts/HOT`, editor helpers, and PlayMode tests under `Assets/Tests/PlayMode`. Package governance records live under `com.uidatarender/Documentation~/Testing/Unit`; Sample records and historical repository documentation live under the root `Documentation~`. Treat `Library`, `Temp`, `Logs`, `obj`, `CodeCoverage`, `TestResults`, and generated `.csproj` files as Unity/tool output.

## Build, Test, and Development Commands
- `.\Tools\Invoke-LocalValidation.ps1`: runs EditMode and PlayMode Unity tests for this repo.
- `.\Tools\Invoke-LocalValidation.ps1 -SkipPlayMode`: runs only EditMode tests for faster feedback.
- `.\Tools\Invoke-LocalValidation.ps1 -WithCoverage`: runs tests with Unity Code Coverage and writes artifacts under `CodeCoverage/Local`.
- `.\Tools\Invoke-LocalValidation.ps1 -WithCoverage -CoverageThreshold 70`: applies the coverage gate to `UIDataRender`.
- `.\Tools\Test-PackageLayout.ps1`: checks package metadata, dependencies, local linking, assemblies, asset ownership, and Unity `.meta` integrity.
- `.\Tools\Invoke-CleanValidation.ps1 -TestPlatform Both`: exports repository-trackable state and validates it without an existing Unity `Library` or ignored dependency directory.
- `.\Tools\UnityTestRunner\Sync-UnitTestGovernance.ps1 -Scope All`: refreshes and validates Package and Sample unit-test governance records.

## Coding Style & Naming Conventions
Use C# conventions already present in the project: 4-space indentation, PascalCase for public types/methods/properties, camelCase for locals and parameters, and `_camelCase` for private fields. Keep Unity lifecycle and rendering code allocation-conscious, especially in mesh, job, batching, and prefab paths. Preserve `.asmdef` boundaries and Unity `.meta` files when adding assets or scripts.

## Testing Guidelines
Use NUnit via Unity Test Framework. Name test files `Test*.cs` and test methods with behavior-focused names such as `Plan_SplitsWhenOver`. Add `[Category("UT_*_NNN")]` and a nearby `TestRecord: Documentation~/Testing/Unit/...` comment for governed tests. Package record references resolve from the package root; Sample record references resolve from the repository root. Test-file paths in records remain relative to their owning package or Sample content root. Prefer EditMode tests for deterministic logic and PlayMode tests for scene, coroutine, Burst/job runtime, or UI integration behavior. Run local validation before submitting changes that touch runtime code.

## Commit & Pull Request Guidelines
Recent commits use short, imperative summaries, for example `Add mesh stream toggle probe` and `Close csv diagnostic scene workflow`. Keep commits focused and avoid committing generated Unity output unless it is an intentional asset or setting change. Pull requests should describe the behavior changed, list validation performed, link related issues or test records, and include screenshots or scene notes for visual/UI changes.

## Agent-Specific Instructions
Do not overwrite user changes in Unity scenes, prefabs, or generated assets. Check `git status --short` before editing, keep changes scoped, and prefer existing scripts and assembly layout over new tooling.
