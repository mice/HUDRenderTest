# 128-holder diagnostic overhead — 2026-10-25

Same-source Mi 10 ABBA comparison, Release IL2CPP ARM64 Vulkan, 128 holders × 3 null-Sprite Simple quads, Slim, seed 12345, 60 FPS, 60 warm-up + 600 sampled frames per mode, Partial 10%, full refresh every 30 frames. Quad-write substage timing disabled in every run. The only configuration difference is `disableBuildMeshDiagnostics`. Both APKs have source fingerprint `6D26E5C212DAF4DA4A07F734874E80F35F9296B8159B81A48F887B6A46439C41`.

| Run | Stage timing | Generation median | Merge median | Whole refresh median | Whole refresh p95 |
|---|---|---:|---:|---:|---:|
| 1 | on | 3.3080 | 0.3181 | 3.6334 | 3.6814 |
| 2 | off | 1.4413 | 0.3191 | 1.7711 | 1.8267 |
| 3 | off | 1.4332 | 0.3142 | 1.7558 | 1.8250 |
| 4 | on | 3.2522 | 0.3122 | 3.5649 | 3.6839 |

All numbers in ms. Each run has 20 full refreshes; p95 is nearest rank. These runs isolate measurement overhead, not an implementation speedup and not the 100-refresh acceptance matrix. Turning off per-holder/Image timestamps and stage aggregation roughly halves measured refresh time; the production workload still exceeds the 1.5 ms target. Outer generation + merge timers, rendering, dimensions, update selection and geometry counts remain unchanged. Disabled stage fields are unavailable/-1 in JSON and unavailable in CSV. Scoped allocation and frame GC counters remain unavailable on this device; no device zero-GC conclusion.

A preliminary off run (p95 ~1.93 ms) preceded the ABBA sequence and is excluded from this table. Initial capture attempts were interrupted by ADB server restarts while Unity was launching. The completed ABBA runs below are the validated sequence; no Unity process was launched for this project during that sequence.

Local hotspot probe: 38,400 operations on an inactive template measured GetPixelAdjustedRect 26.6988 ms, direct rect 4.2487 ms, relative matrix calculation 15.1691 ms, timestamp reads 2.2136 ms. This Editor probe selects a query-reuse candidate; it is not a device cost prediction. See [raw probe](editor-query-probe.json), reproducible through `HudRcSceneBuilder.MeasureGenerationQueriesBatch -hudQueryProbeOutput <path>` in Unity batch mode.

Evidence: [analysis](stages-summary.json), [raw JSON/CSV and provenance](stages-reports.zip), [build identities](android-builds.json), [source manifest](stages-sources.sha256), [analyzer](analyze-stages.py), [capture](capture-stages.ps1), [integrity hashes](artifacts.sha256). Extract the zip, then run `python analyze-stages.py <directory> <output.json>` to reproduce the summary.
