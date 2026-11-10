# UI Data Render

`com.uidatarender` provides the reusable UGUI and TextMeshPro mesh extraction, batching, rendering, and prefab lifecycle components used by the HUD renderer.

## Layout

- `Runtime/`: runtime assemblies, UIE shaders, TMP resources, and managed-task support code.
- `Tests/EditMode/`: deterministic package tests.
- `Documentation~/`: package design and test-governance records.
- `Third Party Notices.md`: notices for bundled font and sprite resources.

The repository's `HUDRenderTestSample` project references this package through a local `file:` dependency and contains scenes, prefabs, sample drivers, and PlayMode integration tests.

## Dependencies

The package targets Unity 2022.3 and declares Burst 1.8.28, TextMesh Pro 3.0.6, and stock UGUI 1.0.0. `VertexHelperUtils` provides package-owned extraction through public UGUI APIs. VERIFY-02 validation on Unity 2022.3.53f1 passed in both the custom Sample and actual stock UGUI consumer: 13 extraction tests, 196 complete EditMode tests and 61 D3D11 PlayMode tests per environment, with no failures or skips. Warmed extraction recorded zero managed allocation events with calibrated positive controls.

Stock index extraction uses a reusable Mesh and index list through `VertexHelper.FillMesh` / `Mesh.GetTriangles`. It preserves indexed topology and offsets, and inherits `FillMesh`'s limit of fewer than 65,000 vertices for nonempty index streams. It performs extra mesh work; historical custom-UGUI performance measurements do not describe this fallback. Scratch capacity grows with geometry and is retained until release. Extraction and cleanup must run on the Unity main thread.

An actual stock/custom Mi 10 comparison accepted eight runs and 72,048 frames of the same Sprite/TMP workload at 2340×1080, with all intrusive probes off. Mean per-run Burst update medians were 6.2247375 ms custom and 8.5542625 ms stock (+37.42%); this is a dependency-path scene observation. See [source, timing, allocation scope and remaining gates](../Documentation~/perf-04-stock-scene-results-2026-11-11.md).

A compatible public `VertexHelper.CopyIndices` instance method, such as the repository Sample's custom UGUI, takes precedence over the extension and retains the direct-copy path. Calling `VertexHelperUtils.CopyIndices(helper, ...)` explicitly always exercises the stock fallback. `VertexHelperUtils.ReleaseIndexScratch()` releases fallback storage; the next call recreates it. Editor reload/exit and runtime subsystem reset/application quit also release it, using deferred Mesh destruction while playing.

The package retains the TMP settings, default font/fallback chain, atlas, sprite resource, line-breaking data, and shaders required by clean-install runtime and test behavior. Sample-only TMP material presets belong to `HUDRenderTestSample`.

## Validation

The Sample runner also supports an opt-in repository fixture with three distinct Sprite textures and six primary-atlas TMP glyphs per holder. Build with `Tools/Invoke-HudRcAndroidValidation.ps1 -RepositoryResources -UseSlim -GenerationBatch -DisableBuildMeshDiagnostics -BucketStrategy Rotating -SampleFrames 3000 -WarmupFrames 60 -TargetFrameRate 60`. Existing null-Sprite defaults remain available. Build tooling binds resources in a temporary scene and records resource hashes, source fingerprint, build identity and UGUI provenance.

For a separate diagnostic APK, add `-Development -AllocationEvents` and choose a different `-ApkPath`. Allocation-event counts require a known allocation positive control; bytes additionally require a Bytes unit and byte calibration. Marker durations cannot represent allocated bytes. Missing or saturated counters remain unavailable. An optional persistentDataPath `hud_rc_request.json` containing `requestId` and the complete `configuration` overrides embedded settings at startup; reports hash its original bytes. See [current device conclusions and remaining PERF-04 gates](../Documentation~/perf-04-stock-scene-results-2026-11-11.md).

For rotating generation attribution, the startup request can set `configuration.profileBucketGeneration=true` while retaining `disableBuildMeshDiagnostics=true`. This switch defaults to false. JSON frames include completed-holder counts, four TMP phase timings, stage validity and actual Unity Screen/camera pixel dimensions. Idle or disabled stages are unavailable. Compare probe on/off using the same APK before interpreting inclusive phase costs; collect GC evidence in a separate Unity Profiler session so its overhead does not enter Release timing. This diagnostic does not change text regeneration or cache geometry.

From the repository root, use `.\Tools\Test-PackageLayout.ps1` for fast structural checks and `.\Tools\Invoke-LocalValidation.ps1` for the working-tree suite. Add `-SkipPlayMode -TestFilter TestVertexHelperUtils` for the extraction tests.

`.\Tools\Invoke-CleanValidation.ps1 -PrepareOnly` prepares one reusable stock consumer in a detached worktree at `D:/workspace/HUDRenderPackageValidation` without launching Unity. It overlays trackable working-tree content through a temporary index, excludes the custom UGUI source, requests UGUI 1.0.0, and omits optional agent tooling from the exported manifest. Real Git metadata and source fingerprints retain Sample report traceability. The source Sample remains unchanged. Unowned existing directories are rejected. Initial preparation starts without a Library; later runs reuse it and record that fact.

After obtaining editor launch permission where required, run `.\Tools\Invoke-CleanValidation.ps1 -TestFilter TestVertexHelperUtils`, followed by the required regressions (`-TestPlatform Both -WithGraphics` for the full graphical suite). Missing, empty, failing, or skipped test results fail acceptance. Local `TestResults/CleanValidation` artifacts record package source hashes and Unity's actual resolved UGUI path, source hash, lock entry and custom-method absence; preparation alone is not compilation evidence.
