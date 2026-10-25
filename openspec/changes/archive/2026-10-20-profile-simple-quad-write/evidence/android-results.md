# Xiaomi 10 quad-write comparison — 2026-10-20

## Result and limits

The eight accepted runs validate Managed/Slim × quad timestamps off/on, twice in reverse order. All 14,400 frames and paired CSV rows passed configuration, availability, count, sentinel and deterministic workload checks. All APKs and reports share the same source fingerprint. Task 2.2 is complete; this is diagnostic evidence, not a performance improvement or release acceptance.

Fine-grained timestamps materially increase the enclosing write time. The five measured stages include timing/marker overhead and must not be interpreted as uninstrumented instruction costs. The off variants still enable aggregate BuildMesh diagnostics. Scoped allocation counters are unavailable in this IL2CPP run; Editor allocation tests do not turn them into device zero-GC evidence.

## Reproduction and provenance

- Xiaomi Mi 10 (`umi`), Android 11, Unity 2022.3.53f1, Release IL2CPP ARM64, Vulkan.
- 256 holders × 3 null-Sprite Simple Image quads, 10% partial refresh (26 holders), full refresh every 30 frames, seed 12345, target 60 FPS.
- Per run: 60 warm-up frames and 600 samples for each of Static, Partial and Burst. Each Burst run contributes 20 refresh frames. Refresh-only results exclude idle Burst frames.
- Order: Managed off → Managed on → Slim off → Slim on → Slim on → Slim off → Managed on → Managed off. Ten seconds between accepted runs. Two repeats expose variation but do not establish statistical confidence.
- USB/AC charging, battery 100%; temporary stay-awake setting restored after capture. Preflight reports affected by screen sleep or the wrong installed variant are excluded.
- Source fingerprint: `31F163F85D75E1A5B1630F1759146E13F893F60BDBCC6973F6C58C0C7A45A8E7`. Base commit `3a6f553ef3f03ed648aa6130e76ed16a80eb62b3` with uncommitted changes; this is not a clean RC.
- [Source manifest](android-sources.sha256), [APK hashes/build settings](android-builds.json), [machine-readable summary](android-summary.json), [raw reports and capture scripts](android-reports.zip).
- `HudRcSceneBuilder.BuildAndroidBatch` builds a temporary scene copy and restores backend/architecture; the committed performance scene and ProjectSettings have no resulting diff. The Android build script now accepts `-UseSlim`, `-ProfileQuadWrites`, `-SampleFrames 600 -WarmupFrames 60 -TargetFrameRate 60`.
- To independently validate: extract `android-reports.zip`, then run `python analyze-quad-reports.py <extracted-directory> <output-summary.json>`. Percentiles use nearest rank `ceil(n*p)-1`, matching the runner.

## Per-run measurements

All values are milliseconds, **median / p95**. These are measured totals, not sums of independently calculated medians.

### Partial

| Run | Variant | Generation | BuildMesh | Aggregate quad write | Merge | Generation + merge |
|---|---|---:|---:|---:|---:|---:|
| 1 | managed-off | 0.7135 / 0.7812 | 0.6144 / 0.6741 | 0.0587 / 0.0657 | 0.7481 / 0.8089 | 1.4668 / 1.5740 |
| 2 | managed-on | 0.9251 / 1.0120 | 0.8270 / 0.9003 | 0.2798 / 0.3049 | 0.7404 / 0.7920 | 1.6710 / 1.8162 |
| 3 | slim-off | 0.7138 / 0.7858 | 0.6145 / 0.6738 | 0.0625 / 0.0688 | 2.3117 / 2.7711 | 3.0291 / 3.4899 |
| 4 | slim-on | 0.9332 / 1.0251 | 0.8349 / 0.9122 | 0.2843 / 0.3091 | 2.2970 / 2.7703 | 3.2313 / 3.7025 |
| 5 | slim-on | 0.9386 / 1.0483 | 0.8391 / 0.9173 | 0.2880 / 0.3149 | 2.3145 / 2.8739 | 3.2661 / 3.8687 |
| 6 | slim-off | 0.7006 / 0.7806 | 0.6028 / 0.6664 | 0.0579 / 0.0637 | 2.3380 / 2.6870 | 3.0439 / 3.4047 |
| 7 | managed-on | 0.9279 / 1.0288 | 0.8298 / 0.9048 | 0.2832 / 0.2971 | 0.7511 / 0.8067 | 1.6835 / 1.8385 |
| 8 | managed-off | 0.7027 / 0.7735 | 0.6043 / 0.6640 | 0.0588 / 0.0651 | 0.7401 / 0.7953 | 1.4478 / 1.6036 |

### Burst-refresh

| Run | Variant | Generation | BuildMesh | Aggregate quad write | Merge | Generation + merge |
|---|---|---:|---:|---:|---:|---:|
| 1 | managed-off | 6.5790 / 6.6728 | 5.7065 / 5.8185 | 0.5248 / 0.5641 | 0.8060 / 0.8317 | 7.3826 / 7.4745 |
| 2 | managed-on | 8.7655 / 8.8687 | 7.8886 / 7.9844 | 2.7471 / 2.8051 | 0.7918 / 0.8309 | 9.5987 / 9.6589 |
| 3 | slim-off | 6.5336 / 6.7103 | 5.6756 / 5.7931 | 0.5838 / 0.6078 | 3.0977 / 3.2035 | 9.6025 / 9.8455 |
| 4 | slim-on | 8.8169 / 8.9993 | 7.9634 / 8.1420 | 2.7883 / 2.9435 | 2.5729 / 3.1077 | 11.4626 / 12.0608 |
| 5 | slim-on | 8.8472 / 8.9612 | 7.9796 / 8.0916 | 2.8185 / 2.8942 | 2.5730 / 3.2290 | 11.4316 / 12.1283 |
| 6 | slim-off | 6.4728 / 6.6427 | 5.6144 / 5.7949 | 0.5328 / 0.5740 | 2.7222 / 5.8696 | 9.2088 / 12.5613 |
| 7 | managed-on | 8.8187 / 8.9150 | 7.9580 / 8.0631 | 2.7908 / 2.9116 | 0.7946 / 0.8221 | 9.6174 / 9.6928 |
| 8 | managed-off | 6.5265 / 6.6632 | 5.6685 / 5.8106 | 0.5402 / 0.5644 | 0.7805 / 0.9384 | 7.3139 / 7.4776 |

### Partial merge breakdown, quad timestamps off

| Run | Path | Merge plan | Merge fill | Mesh upload |
|---|---|---:|---:|---:|
| 1 | managed-off | 0.1270 / 0.1383 | 0.4684 / 0.5042 | 0.1258 / 0.1418 |
| 3 | slim-off | 0.1244 / 0.1324 | 2.0284 / 2.4615 | 0.1269 / 0.1392 |
| 6 | slim-off | 0.1255 / 0.1320 | 2.0590 / 2.3991 | 0.1239 / 0.1404 |
| 8 | managed-off | 0.1280 / 0.1373 | 0.4660 / 0.4998 | 0.1210 / 0.1384 |

The two off repeats show similar generation and quad-write costs, while Slim merge-fill medians are 2.0284–2.0590 ms versus Managed 0.4660–0.4684 ms (about 4.3–4.4×). This localizes a useful follow-up measurement to Slim's merge-fill path; it does not yet identify the responsible operation or prove a fix.

## Measured stage shares

Each cell is a percentage of the **sum of the five sampled stage times** across refresh frames in that run. It is not a percentage of frame time, generation or uninstrumented write cost.

| Run | Path | Workload | Storage | Vertex | UV | Color | Index |
|---|---|---|---:|---:|---:|---:|---:|
| 2 | managed-on | Partial | 24.7% | 18.5% | 19.4% | 19.1% | 18.3% |
| 2 | managed-on | Burst-refresh | 24.1% | 18.5% | 19.9% | 18.9% | 18.5% |
| 4 | slim-on | Partial | 18.3% | 21.3% | 20.4% | 19.8% | 20.3% |
| 4 | slim-on | Burst-refresh | 18.0% | 21.7% | 20.3% | 19.9% | 20.1% |
| 5 | slim-on | Partial | 18.8% | 20.7% | 20.3% | 20.1% | 20.1% |
| 5 | slim-on | Burst-refresh | 18.7% | 20.9% | 20.3% | 19.6% | 20.5% |
| 7 | managed-on | Partial | 26.4% | 18.1% | 19.2% | 18.5% | 17.7% |
| 7 | managed-on | Burst-refresh | 25.7% | 18.3% | 19.1% | 18.9% | 18.0% |

### Stage timings (ms, median / p95)

| Run | Path | Workload | Storage | Vertex | UV | Color | Index |
|---|---|---|---:|---:|---:|---:|---:|
| 2 | managed-on | Partial | 0.0287 / 0.0331 | 0.0228 / 0.0242 | 0.0239 / 0.0258 | 0.0233 / 0.0245 | 0.0229 / 0.0239 |
| 2 | managed-on | Burst-refresh | 0.2905 / 0.3121 | 0.2257 / 0.2291 | 0.2403 / 0.2733 | 0.2312 / 0.2326 | 0.2251 / 0.2283 |
| 4 | slim-on | Partial | 0.0235 / 0.0247 | 0.0269 / 0.0286 | 0.0258 / 0.0276 | 0.0250 / 0.0266 | 0.0261 / 0.0276 |
| 4 | slim-on | Burst-refresh | 0.2290 / 0.2338 | 0.2650 / 0.2841 | 0.2556 / 0.2739 | 0.2489 / 0.2763 | 0.2545 / 0.2612 |
| 5 | slim-on | Partial | 0.0237 / 0.0250 | 0.0264 / 0.0284 | 0.0259 / 0.0280 | 0.0258 / 0.0275 | 0.0262 / 0.0278 |
| 5 | slim-on | Burst-refresh | 0.2304 / 0.2659 | 0.2651 / 0.2824 | 0.2582 / 0.2806 | 0.2521 / 0.2580 | 0.2529 / 0.2788 |
| 7 | managed-on | Partial | 0.0311 / 0.0360 | 0.0233 / 0.0246 | 0.0239 / 0.0261 | 0.0233 / 0.0244 | 0.0227 / 0.0237 |
| 7 | managed-on | Burst-refresh | 0.3209 / 0.3489 | 0.2306 / 0.2371 | 0.2404 / 0.2471 | 0.2320 / 0.2351 | 0.2252 / 0.2474 |

Per-stage median/p95 (including min/max), DoGenerate, frame intervals and counter availability are retained for every run in `android-summary.json`.

## On/off sensitivity

Adjacent paired runs compare the same path; these differences include run-to-run noise. They are diagnostic overhead estimates, not calibrated timer-only costs.

| Path | Off → on runs | Workload | Write median Δ | Generation median Δ | Regeneration median Δ |
|---|---|---|---:|---:|---:|
| managed | 1 → 2 | Partial | +0.2211 ms (+376.7%) | +0.2116 ms (+29.7%) | +0.2042 ms (+13.9%) |
| managed | 1 → 2 | Burst-refresh | +2.2223 ms (+423.5%) | +2.1865 ms (+33.2%) | +2.2161 ms (+30.0%) |
| slim | 3 → 4 | Partial | +0.2218 ms (+354.9%) | +0.2194 ms (+30.7%) | +0.2022 ms (+6.7%) |
| slim | 3 → 4 | Burst-refresh | +2.2045 ms (+377.6%) | +2.2833 ms (+34.9%) | +1.8601 ms (+19.4%) |
| slim | 6 → 5 | Partial | +0.2301 ms (+397.4%) | +0.2380 ms (+34.0%) | +0.2222 ms (+7.3%) |
| slim | 6 → 5 | Burst-refresh | +2.2857 ms (+429.0%) | +2.3744 ms (+36.7%) | +2.2228 ms (+24.1%) |
| managed | 8 → 7 | Partial | +0.2244 ms (+381.6%) | +0.2252 ms (+32.0%) | +0.2357 ms (+16.3%) |
| managed | 8 → 7 | Burst-refresh | +2.2506 ms (+416.6%) | +2.2922 ms (+35.1%) | +2.3035 ms (+31.5%) |

## Thermal and pacing checks

| Run | Battery °C before → after | Thermal status before → after | Largest frame interval (ms) |
|---|---|---|---:|
| 1 | 28.7 → 28.7 | Thermal Status: 0 → Thermal Status: 0 | 19.868 |
| 2 | 28.7 → 29.0 | Thermal Status: 0 → Thermal Status: 0 | 17.741 |
| 3 | 29.0 → 29.0 | Thermal Status: 0 → Thermal Status: 0 | 17.485 |
| 4 | 29.0 → 29.2 | Thermal Status: 0 → Thermal Status: 0 | 19.120 |
| 5 | 29.2 → 29.2 | Thermal Status: 0 → Thermal Status: 0 | 18.129 |
| 6 | 29.2 → 29.2 | Thermal Status: 0 → Thermal Status: 0 | 19.883 |
| 7 | 29.2 → 29.2 | Thermal Status: 0 → Thermal Status: 0 | 22.867 |
| 8 | 29.2 → 29.2 | Thermal Status: 0 → Thermal Status: 0 | 17.305 |

## Decision and next task

- Keep fine-grained timestamps opt-in and off for performance baselines. The measured write stages do not establish a stable uninstrumented hotspot; no speculative write micro-optimization is justified by this comparison.
- Finish the missing full BuildMesh stage-accounting and diagnostics-disabled warmed allocation verification in `profile-buildmesh-stages`, then review the two profiling changes together for spec sync/archive. Existing task checkmarks alone do not replace the gaps recorded in the change review.
- Prioritize investigating the Slim merge-fill difference with fine-grained quad timing disabled before changing quad writes. Preserve both paths' output equivalence and distinguish copying/conversion from job scheduling or other fill costs before choosing an optimization.
- Keep PERF-04 open. The current diagnostic workload still exceeds the 1 ms reference budget; a clean candidate, unambiguous measurement mode, current coverage and Jenkins evidence remain separate requirements.
