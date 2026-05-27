# HUDRenderTest Copilot Instructions

## Build, test, and lint

This repository is a Unity project (Unity `2022.3.53f1c1`, see `ProjectSettings/ProjectVersion.txt`).

### Build (player)

```powershell
"C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath "D:\ProjCommon\HUDRenderTest" -buildWindows64Player "Build\HUDRenderTest.exe"
```

### Tests (EditMode, NUnit via Unity Test Runner)

Run repository-owned EditMode tests:

```powershell
"C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe" -batchmode -nographics -projectPath "D:\ProjCommon\HUDRenderTest" -runTests -testPlatform EditMode -assemblyNames "Tests" -testResults "Logs\EditModeRepoTests.xml"
```

Run repository-owned PlayMode tests:

```powershell
"C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe" -batchmode -nographics -projectPath "D:\ProjCommon\HUDRenderTest" -runTests -testPlatform PlayMode -assemblyNames "Tests.PlayMode" -testResults "Logs\PlayMode_TestsPlayMode.xml"
```

Run the local validation bundle (current working tree, no GitHub Actions license secrets required):

```powershell
.\Tools\Invoke-LocalValidation.ps1
.\Tools\Invoke-LocalValidation.ps1 -WithCoverage
.\Tools\Invoke-LocalValidation.ps1 -WithCoverage -CoverageThreshold 70
.\Tools\Test-CoverageGate.ps1 -SummaryPath ".\CodeCoverage\Local\Report\Summary.xml" -AssemblyName "UIDataRender" -MinimumLineCoverage 70
```

Run a single test:

```powershell
"C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe" -batchmode -nographics -projectPath "D:\ProjCommon\HUDRenderTest" -runTests -testPlatform EditMode -assemblyNames "Tests" -testFilter "TestUIGeometry.TestUIGeometryAlloc46" -testResults "Logs\SingleTest.xml"
```

Do not add `-quit` when running Unity Test Runner commands in this repository; it can exit before the XML results are written.

### Lint

There is no dedicated lint command/script in this repo. C# diagnostics come from Unity/VS analyzers in generated `.csproj` files.

## High-level architecture

This project implements a custom UGUI batching pipeline that extracts UI geometry, merges it, and renders with one `Graphics.DrawMesh` call.

1. **UI extraction layer** (`Assets/Scripts/UIDataRender/UI/`)
   - `UIText` / `UIImage` implement `IUIDrawTarget`.
   - They capture `VertexHelper` data during `OnPopulateMesh` and write into an `IUIData` container.

2. **Mesh data layer** (`Assets/Scripts/UIDataRender/UIMeshData.cs`, `UIMeshDataX.cs`)
   - `UIMeshData`: per-element managed arrays.
   - `UIMeshDataX`: shared-buffer mode backed by global `UIGeometry`.
   - Both flow through `IUIData` so higher layers can switch strategies.

3. **Geometry pool** (`Assets/Scripts/UIDataRender/UIGeometry.cs`)
   - Shared vertex/index buffers with free-slice tracking (`openVertexList`, `openIndicesList`).
   - `Alloc` / `ReAlloc` / `Release` manage contiguous slices (`MeshSlim`).

4. **Prefab + texture orchestration** (`UIPrefaHolder.cs`, `DataPrefabHolder.cs`, `UIPrefabManager.cs`, `UIPrefabRegistration.cs`)
   - `UIPrefabManager` registers draw targets, tracks texture slots, and updates material texture bindings.
   - `UIPrefaHolder.UseSlim(bool)` switches between `UIMeshData` and `UIMeshDataX`.

5. **Merge + draw**
   - Main-thread merge path: `DataPrefabHolder.Fill(...)` into combined vertex/uv/color/index lists.
   - Job bridge path: `Assets/Scripts/UIDataRender/MeshJobs.cs`, `MeshMergeJobs.cs` + `Assets/OtherPlugins/managedTask/`.
   - Final draw uses `Hidden/UIE-AtlasBlit` (`Assets/Resources/Shader/UIE-AtlasBlit.shader`).

## Key repository conventions

- **UV channel contract is semantic, not just UVs**
  - `uv.xy`: texture coordinates
  - `uv.z`: texture slot index
  - `uv.w`: flags (e.g., text/image path)
  - Keep this contract when changing mesh generation, merge jobs, or shader code.

- **Texture slot conventions are shared across CPU + shader**
  - `UIImage` obtains slot from `UIPrefabManager`; `UIText` commonly uses the font texture slot.
  - Shader currently declares `_MainTex0`..`_MainTex3`; keep indices within shader-supported range unless CPU + shader are updated together.

- **Offset correctness is critical in shared-buffer paths**
  - In `VertexHelperUtils.FillData3`, copied indices must include `vertexOffset` when vertices are written at an offset.
  - Any new bulk-copy path must preserve vertex/index offset coupling.

- **Keep both data-container modes behaviorally aligned**
  - Changes to `UIMeshData` often require matching updates in `UIMeshDataX` (fill, transform, texture-index updates).
  - `UseSlim(bool)` is runtime-switchable and both modes are used by benchmark/test scripts.

- **Tests vs demo scripts**
  - NUnit tests live under `Assets/Tests/` (assembly `Tests`, `TestUIDataRender.asmdef`).
  - `Assets/Scripts/Test*.cs` are runtime demo/benchmark MonoBehaviours, not unit tests.

- **UGUI package internals are intentionally used**
  - `Assets/Scripts/UGUI.Extends/VertexHelperUtils.cs` reads `VertexHelper` internal lists (`m_Positions`, `m_Uv0S`, `m_Indices`, etc.) from the vendored package at `Packages/com.unity.ugui@1.0.0`.
  - Preserve this compatibility when updating UGUI package sources or extension code.

- **Unity assets should keep `.meta` files tracked**
  - New assets under `Assets/` are expected to include their corresponding `.meta` files in version control.
