# Repository Guidelines

## Project Structure & Module Organization
This is a Unity project targeting Unity `2022.3.53f1c1`. Runtime source lives under `Assets/Scripts`, with the main renderer in `Assets/Scripts/UIDataRender`, sample and performance drivers in `Assets/Scripts/HOT`, and UGUI helpers in `Assets/Scripts/UGUI.Extends`. Third-party or local support plugins are under `Assets/OtherPlugins`. Tests are split into `Assets/Tests/EditMode` and `Assets/Tests/PlayMode`; matching governance records live in `Documentation~/Testing/Unit`. Scenes, prefabs, shaders, and art are in `Assets/Scenes`, `Assets/Resources`, `Assets/Resources/Shader`, and `Assets/Art`. Treat `Library`, `Temp`, `Logs`, `obj`, `CodeCoverage`, and generated `.csproj` files as Unity/tool output.

## Build, Test, and Development Commands
- `.\Tools\Invoke-LocalValidation.ps1`: runs EditMode and PlayMode Unity tests for this repo.
- `.\Tools\Invoke-LocalValidation.ps1 -SkipPlayMode`: runs only EditMode tests for faster feedback.
- `.\Tools\Invoke-LocalValidation.ps1 -WithCoverage`: runs tests with Unity Code Coverage and writes artifacts under `CodeCoverage/Local`.
- `.\Tools\Invoke-LocalValidation.ps1 -WithCoverage -CoverageThreshold 70`: applies the coverage gate to `UIDataRender`.
- `.\Tools\UnityTestRunner\Sync-UnitTestGovernance.ps1`: refreshes unit test governance docs after adding or renaming test IDs.

## Coding Style & Naming Conventions
Use C# conventions already present in the project: 4-space indentation, PascalCase for public types/methods/properties, camelCase for locals and parameters, and `_camelCase` for private fields. Keep Unity lifecycle and rendering code allocation-conscious, especially in mesh, job, batching, and prefab paths. Preserve `.asmdef` boundaries and Unity `.meta` files when adding assets or scripts.

## Testing Guidelines
Use NUnit via Unity Test Framework. Name test files `Test*.cs` and test methods with behavior-focused names such as `Plan_SplitsWhenOver`. Add `[Category("UT_*_NNN")]` and a nearby `TestRecord: Documentation~/Testing/Unit/...` comment for governed tests. Prefer EditMode tests for deterministic logic and PlayMode tests for scene, coroutine, Burst/job runtime, or UI integration behavior. Run local validation before submitting changes that touch runtime code.

## Commit & Pull Request Guidelines
Recent commits use short, imperative summaries, for example `Add mesh stream toggle probe` and `Close csv diagnostic scene workflow`. Keep commits focused and avoid committing generated Unity output unless it is an intentional asset or setting change. Pull requests should describe the behavior changed, list validation performed, link related issues or test records, and include screenshots or scene notes for visual/UI changes.

## Agent-Specific Instructions
Do not overwrite user changes in Unity scenes, prefabs, or generated assets. Check `git status --short` before editing, keep changes scoped, and prefer existing scripts and assembly layout over new tooling.
