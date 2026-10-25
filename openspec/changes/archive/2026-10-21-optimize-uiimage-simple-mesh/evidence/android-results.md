# Sprite-backed direct/fallback comparison — 2026-10-20

Four ABBA runs on Mi 10, Release IL2CPP ARM64 Vulkan. Same source fingerprint for both APKs: `FE92EA68130DEEBBDDA772399C97D40DAB4C4CA14AA02C543A43A2B1F9B0C88C`. Only the temporary scene force-fallback flag differs. Slim enabled, 256 holders × 3 quads, Partial 10%, Burst every 30 frames, seed 12345, warm-up 60 and samples 600 per mode, target 60 FPS, fine quad timestamps off.

All 7,200 frames and paired CSVs passed configuration, workload/geometry/checksum and source/APK hash checks. Each template has three actual Sprite-backed Images. Preparation reports 3/3 direct hits for direct builds and 0/3 for fallback; on every refresh frame the selected path has positive stage time and the other has zero. Existing production profiler markers wrap those same branches; this is branch/stage evidence, not a saved Profiler timeline.

Each run has 600 Partial samples and 20 Burst-refresh samples. Cells are **median / p95 (ms)**, nearest-rank percentiles; regeneration is generation + merge per frame.

| Run | Mode | Generation | Regeneration | Direct path | UGUI fallback |
|---|---|---:|---:|---:|---:|
| 1 sprite-fallback | Partial | 0.7915 / 0.8689 | 1.2901 / 1.3844 | 0.0000 / 0.0000 | 0.4574 / 0.5038 |
| 1 sprite-fallback | Burst-refresh | 7.4510 / 7.5509 | 7.9941 / 8.0869 | 0.0000 / 0.0000 | 4.2547 / 4.3305 |
| 2 sprite-direct | Partial | 0.7930 / 0.8663 | 1.3064 / 1.3949 | 0.4577 / 0.5027 | 0.0000 / 0.0000 |
| 2 sprite-direct | Burst-refresh | 7.3527 / 7.4913 | 7.9216 / 8.0345 | 4.2393 / 4.3795 | 0.0000 / 0.0000 |
| 3 sprite-direct | Partial | 0.8034 / 0.8790 | 1.3151 / 1.4368 | 0.4599 / 0.4971 | 0.0000 / 0.0000 |
| 3 sprite-direct | Burst-refresh | 7.4504 / 7.5898 | 8.0029 / 8.1404 | 4.2570 / 4.3291 | 0.0000 / 0.0000 |
| 4 sprite-fallback | Partial | 0.7930 / 0.8911 | 1.2998 / 1.4368 | 0.0000 / 0.0000 | 0.4591 / 0.5206 |
| 4 sprite-fallback | Burst-refresh | 7.4484 / 7.5336 | 7.9867 / 8.0717 | 0.0000 / 0.0000 | 4.2154 / 4.3328 |

## Decision and limitations

Direct Sprite generation is **behaviorally equivalent but has no demonstrated end-to-end speedup in this workload**. Partial regeneration is 1.2901/1.2998 ms for fallback and 1.3064/1.3151 ms for direct; paired changes are about 1.2% slower. Burst is 7.9941/7.9867 ms versus 7.9216/8.0029 ms (about −0.9% / +0.2%). These differences are small relative to repeat variation; retain the equivalent, observable/reversible path without claiming a performance gain. The diagnostic force-fallback option remains available. This does not meet the overall 1 ms reference.

The source/APK comparison is current-path direct versus forced fallback, not a historical before/after optimization claim. Device tests here cover Slim; both Managed and Slim correctness is covered locally. The shared FullRect Sprite uses the built-in white texture; atlas packing/complex sprites are outside this workload. All device scoped/frame-GC counters remain unavailable, and fine quad-write counters remain disabled; no zero-device-GC claim.

## Environment

| Run | Battery before/after (0.1 °C) | Thermal before/after | Largest frame interval (ms) |
|---|---|---|---:|
| 1 | temperature: 270 → temperature: 272 | Thermal Status: 0 → Thermal Status: 0 | 18.9611 |
| 2 | temperature: 272 → temperature: 272 | Thermal Status: 0 → Thermal Status: 0 | 18.2347 |
| 3 | temperature: 272 → temperature: 275 | Thermal Status: 0 → Thermal Status: 0 | 21.0187 |
| 4 | temperature: 275 → temperature: 277 | Thermal Status: 0 → Thermal Status: 0 | 17.8436 |

Temporary stay-awake state was restored after sampling; direct APK was reinstalled successfully. Both builds remain dirty working-tree evidence, not RC validation.

## Artifacts

- [Raw JSON/CSV/metadata and source/build records](android-reports.zip).
- [Summary](android-summary.json), [build hashes](android-builds.json), [source manifest](sprite-sources.sha256).
- [Analyzer](analyze-sprite.py): extract the ZIP, then run `python analyze-sprite.py <directory> <summary.json>`.
- [Capture script](capture-sprite.ps1) and [integrity hashes](artifacts.sha256).
