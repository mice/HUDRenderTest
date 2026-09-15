# UPM Migration Baseline

Captured on 2026-09-15 before replacing the modified-UGUI extraction path.

## Behavior

- EditMode: 118 total, 118 passed, 0 failed, 0 skipped.
- PlayMode: 22 total, 22 passed, 0 failed, 0 skipped.
- The two new EditMode cases account for the increase from the earlier 116-test baseline.

## VertexHelper Extraction

Environment:

- Unity 2022.3.53f1, EditMode test runner.
- Four vertices and six indices per extraction.
- 32 warm-up calls followed by 10,000 measured calls.
- `Stopwatch.Frequency`: 10,000,000 ticks/second.

Results:

| Path | Total ticks | Approx. time/call | Managed allocation |
|---|---:|---:|---:|
| `FillData2` | 16,446 | 0.164 us | 0 B |
| `FillData3` | 18,256 | 0.183 us | 0 B |

Portable-path acceptance gate:

- No managed allocation after scratch and destination buffers are warmed.
- At most 5 us per four-vertex extraction in the same 10,000-iteration EditMode measurement.
- At most 30 times the matching legacy-path cost.
- A representative performance scene must show no more than 1 ms additional p95 frame time attributable to UI regeneration; otherwise implementation pauses for design review.

## Portable Public-API Candidate

The implementation uses `VertexHelper.PopulateUIVertex` for vertex channels and a reusable scratch `Mesh` plus `GetTriangles(List<int>)` for indices. It passes all 118 EditMode tests and reports zero managed allocation after warm-up.

| Run | `FillData2` | Relative to baseline | `FillData3` | Relative to baseline | Managed allocation |
|---|---:|---:|---:|---:|---:|
| 1 | 4.924 us/call | 29.9x | 6.038 us/call | 33.1x | 0 B |
| 2 | 8.285 us/call | 50.4x | 8.405 us/call | 46.0x | 0 B |

The candidate exceeds both the 5 us absolute limit and the 30x relative limit. An attempted `Mesh.GetIndexData<T>` optimization was rejected because that API is not available on `Mesh` in Unity 2022.3; the code was restored to the tested `GetTriangles(List<int>)` implementation. Dependency removal paused at the required design-review gate. After the comparison was presented, the user instructed the migration to continue. The accepted decision prioritizes stock UGUI compatibility and independent UPM distribution while retaining the measured CPU regression as an explicit delivery risk.

## Dependency Closure

- Removed `HUDRenderTestSample/Packages/com.unity.ugui@1.0.0` after verifying its resolved path was inside the repository workspace.
- Unity regenerated `HUDRenderTestSample/Packages/packages-lock.json` with `com.unity.ugui` version `1.0.0` and source `builtin`.
- Working-tree stock-UGUI EditMode validation: 118 total, 118 passed, 0 failed, 0 skipped.
- Clean validation exported the current repository-trackable state through a temporary Git index. The snapshot initially contained neither `Library` nor an embedded UGUI directory.
- Clean-snapshot stock-UGUI EditMode validation: 118 total, 118 passed, 0 failed, 0 skipped.
- Clean artifacts: `TestResults/CleanValidation/editmode-results.xml`, `TestResults/CleanValidation/unity-editmode.log`, and `TestResults/CleanValidation/package-layout.json`.

## Migration Inventory

- Package root: `com.uidatarender` with three runtime asmdefs and one EditMode test asmdef.
- Sample root: `HUDRenderTestSample` with demo/runtime, editor helper, and PlayMode test asmdefs.
- Sample local dependency: `file:../../com.uidatarender` with `com.uidatarender` listed in `testables`.
- Blocking ignored dependency: `HUDRenderTestSample/Packages/com.unity.ugui@1.0.0`.
- Blocking source modification: the ignored UGUI assembly grants `InternalsVisibleTo("UGui.Extends")`.
- Blocking lock state: `com.unity.ugui` is marked `embedded` even though the embedded directory is ignored.
- Working-tree migration at inventory time: 689 pure renames, 2 renamed-and-modified files, 48 additions, 6 modifications, and 9 untracked paths (including this change and new test records).

## Artifacts

- `HUDRenderTestSample/Logs/EditModeRepoTests.xml`
- `HUDRenderTestSample/Logs/EditModeRepoTests.log`
- `HUDRenderTestSample/Logs/PlayMode_TestsPlayMode.xml`
- `HUDRenderTestSample/Logs/PlayMode_TestsPlayMode.log`
- `TestResults/CleanValidation/editmode-results.xml`
- `TestResults/CleanValidation/unity-editmode.log`
- `TestResults/CleanValidation/package-layout.json`
