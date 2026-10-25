# Quad write diagnostics validation — 2026-10-20

## Archive closure

This change and `profile-buildmesh-stages` have been synced to main specs and archived separately. The subsequent full regression passed **142/142 EditMode**, **27/27 PlayMode**, governance **169/169** and layout; see [BuildMesh closure](../2026-10-20-profile-buildmesh-stages/evidence.md). That closure also validates stage containment in every preserved device frame and verifies 137 Runtime/runner source hashes. The local/device sections below retain their original measurement scope; the BuildMesh verification proposed in the device report is now complete. The next investigation is Slim merge-fill.

## Implemented behavior

- Restored per-stage timestamp sampling in Managed and Slim writers behind both `BuildMeshDiagnostics.Enabled` and `ProfileSimpleQuadWrites`. Fine-grained timing is independently opt-in; existing profiler markers remain.
- Each measured write records one sample. Writers, UIImage and holders clear stale results when disabled or falling back. Counts and timings propagate through holder aggregation.
- HUD RC reports now use `hud-rc-v2`. `configuration.profileQuadWrites`, `quadWriteTimestampFrequency`, `buildMeshWriteSamples` and `buildMeshWriteAvailable` document the measurement mode and completeness. Unrequested, idle or incomplete measurements use JSON `-1` / CSV `unavailable`.
- The runner captures its configuration and restores both previous diagnostic flags on reset, completion or failure. Aggregate mesh-write timing remains available independently.
- Timestamp and marker overhead is included in enclosing timings. Substage values are diagnostic estimates; this change does not establish a runtime performance improvement.

## Local validation

Environment: Unity `2022.3.53f1`, Windows Editor/Mono, working tree based on `3a6f553`. This is local validation of uncommitted changes, not an exact release candidate.

| Check | Result |
|---|---|
| EditMode | 140/140 passed, 0 skipped |
| PlayMode | 27/27 passed, 0 skipped |
| Package layout | Passed |
| Test governance | 167 tests / 167 records |
| OpenSpec strict | 12/12 |
| Android | 8/8 device runs; 14,400 frames and paired CSVs validated |
| Coverage / Jenkins | Not run |

Commands: `Tools/Invoke-LocalValidation.ps1` (EditMode passed, initial PlayMode 26/27), then `Tools/Invoke-LocalValidation.ps1 -SkipEditMode -SkipStructure` (full PlayMode passed). The initial export test used exact equality across JSON and CSV parsing, which differed by about `10^-18 ms`; the test now permits `10^-12 ms` round-trip error. Runtime code did not change between the passing runs.

Preserved evidence: [validation summary and source hashes](evidence/local-validation.json), [EditMode NUnit XML](evidence/editmode-results.xml), [PlayMode NUnit XML](evidence/playmode-results.xml).

New governed coverage:

- `UT_MESH_027`: both switches, sampled counts, stale-result clearing and full output-channel equivalence for Managed/Slim.
- `UT_MESH_028`: warmed writes allocate no managed memory with diagnostics disabled, aggregate-only and full quad timing. Every case positively calibrates the `GC.Alloc` recorder.
- `UT_UIIMAGE_007`: two-draw aggregation, null-Sprite Managed/Slim output under translated/rotated parent, fallback and disabled-diagnostic clearing. This also supplies the previously missing null-Sprite parent-transform test combination.
- `UT_PERF_016`: four runner configurations (Managed/Slim × timing off/on), captured configuration, JSON/CSV availability and counts for refresh and idle frames. Existing `UT_PERF_014` now also checks diagnostic setting restoration.

## Device verification

After the initial device deferral, the user connected and authorized Xiaomi Mi 10. Four Release IL2CPP ARM64 APKs were built from the same source fingerprint and each run twice in reverse order, using Vulkan, 256 holders × 3 quads, 60 FPS, 60 warm-up frames and 600 samples per workload. All 14,400 frames and paired CSVs passed availability, counts, sentinel, configuration, geometry-count and deterministic selection checks.

See [full device results and next-task decision](evidence/android-results.md), [summary](evidence/android-summary.json), [source manifest](evidence/android-sources.sha256), [APK provenance](evidence/android-builds.json), [raw reports and validation scripts](evidence/android-reports.zip), and [artifact hashes](evidence/android-artifacts.sha256).

- Fine-grained timestamps increased Partial aggregate write medians by about 0.221–0.230 ms and Burst refresh write medians by 2.204–2.286 ms. Stage shares include instrumentation overhead; keep this mode off for baselines.
- With quad timing off, Partial regeneration medians were 1.448–1.467 ms Managed and 3.029–3.044 ms Slim. The useful follow-up is merge-fill: Slim 2.028–2.059 ms versus Managed 0.466–0.468 ms, while generation/write times were similar.
- Battery temperature was 28.7–29.2°C, thermal status 0; all accepted frame intervals were below 23 ms. Sleep-interrupted and wrong-variant preflight reports were excluded. The temporary stay-awake setting was restored to its original value.
- Original scene and ProjectSettings remain unchanged. The Android build helper uses a temporary scene copy and exposes explicit matrix parameters. All 12 implementation/test/dependency hashes in the earlier local-validation record still match; the four APK builds also compiled the changed Editor helper.

Task 2.2 and this change are now 5/5. Next, close the missing full BuildMesh stage-accounting/disabled warmed-allocation verification before reviewing both profiling changes for spec sync/archive. Investigate Slim merge-fill before speculative quad-write optimization. PERF-04 remains open: the diagnostic scene exceeds the 1 ms reference, scoped device allocation is unavailable, and this dirty snapshot is not a release candidate.
