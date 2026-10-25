# Mi 10 Slim fill comparison — 2026-10-20

## Protocol and validation

Release / IL2CPP / ARM64 / Vulkan on Mi 10, Android 11. Old/new/new/old order; all four reports and paired CSVs passed validation (7,200 frames). Each mode has 600 samples; Burst-refresh has 20 actual refreshes per run. Percentiles use nearest rank `ceil(n*p)-1`, matching the runner. Regeneration is computed per frame as generation + merge before percentile calculation.

Configuration: 256 holders × 3 quads; Partial 10%; Burst every 30 frames; seed 12345; warm-up 60 frames per mode; target 60 FPS; Slim on; fine quad-write timestamps off. Geometry, batches, update counts and selection checksums match for every frame across all four runs.

Both APK/source fingerprints match their respective build records. Only `UIMeshDataX.cs` differs among runtime/runner sources; test/document differences are enumerated in [android-builds.json](android-builds.json). The old APK matches the preserved previous baseline byte for byte.

Battery temperature was 27.2–27.7 °C, battery 100%, reported thermal status 0 throughout. Frame-interval p95 across groups was at most 17.076 ms and the largest interval was 21.021 ms. The capture temporarily kept the phone awake and restored its original value (0). After ABBA, the fixed APK was installed again successfully without starting another measured run.

## Measured CPU times

All cells below are **median / p95, milliseconds**.

| Run | Mode | Merge-fill | Generation | Regeneration |
|---|---|---:|---:|---:|
| 1 slim-baseline | Partial | 1.7185 / 2.3173 | 0.7069 / 0.7777 | 2.7571 / 3.3900 |
| 1 slim-baseline | Burst-refresh | 2.7831 / 2.8927 | 6.5219 / 6.6649 | 9.5898 / 9.8088 |
| 2 slim-fixed | Partial | 0.2277 / 0.2396 | 0.7073 / 0.7807 | 1.2090 / 1.3013 |
| 2 slim-fixed | Burst-refresh | 0.2330 / 0.2354 | 6.5064 / 6.6066 | 7.0359 / 7.1391 |
| 3 slim-fixed | Partial | 0.2293 / 0.2371 | 0.7136 / 0.7881 | 1.2184 / 1.3207 |
| 3 slim-fixed | Burst-refresh | 0.2325 / 0.2360 | 6.5517 / 6.6918 | 7.0925 / 7.2291 |
| 4 slim-baseline | Partial | 2.0383 / 2.3904 | 0.7006 / 0.7763 | 3.0226 / 3.4131 |
| 4 slim-baseline | Burst-refresh | 2.2513 / 2.9398 | 6.5416 / 6.6839 | 9.2461 / 9.8191 |

Pairing runs 1→2 and 4→3, Partial merge-fill median decreases 86.8% and 88.8%; Partial regeneration decreases 56.1% and 59.7%. Burst-refresh merge-fill decreases 91.6% and 89.7%; regeneration decreases 26.6% and 23.3%. Generation remains essentially unchanged (Partial within 1.9%, Burst within 0.3%).

The two old runs vary (Partial fill 1.7185 vs 2.0383 ms; Burst fill 2.7831 vs 2.2513 ms), while both fixed runs consistently reach about 0.23 ms. This supports retaining the bounded fix, but two repeats are not a statistical confidence interval or a guarantee for other devices/workloads. This comparison does not remeasure Managed.

## Allocation and remaining work

All 7,200 frames have a valid main-thread counter. Scoped allocation and frame GC counters are unavailable in all device frames: their numeric fields stay -1 and CSV cells say unavailable. Fine quad-write fields likewise remain unavailable with zero measured writes. Therefore the device evidence demonstrates CPU improvement, not device/whole-application zero GC; bounded allocation evidence comes from the positively calibrated Editor test and generated IL2CPP code.

Fixed Partial regeneration is 1.2090–1.2184 ms median / 1.3013–1.3207 ms p95; Burst-refresh is 7.0359–7.0925 ms median / 7.1391–7.2291 ms p95. PERF-04 and its existing 1 ms reference remain unresolved; there is no budget exception or release acceptance.

Next: return to generation. Fixed Burst generation is 6.5064–6.5517 ms, with BuildMesh 5.6422–5.6801 ms and DoGenerate 4.3575–4.3763 ms. Drawing dimensions account for 1.2889–1.3340 ms, versus aggregate quad writing 0.54–0.55 ms. First close the existing cached-plan geometry-change/cold-vs-warm output-equivalence gap; then isolate repeated layout/property reads in UIImage generation before choosing a new optimization. Keep diagnostic switches fixed for A/B comparisons.

## Reproduction and artifacts

- [Raw reports](android-reports.zip): four JSON/CSV/metadata triples plus both source manifests and build metadata. APK files remain in the ignored Builds directory, identified by SHA256.
- [Summary](android-summary.json): all per-run metrics, available-counter counts and paired reductions.
- [Analyzer](analyze-slim-fill.py): extract the ZIP to a directory and run `python analyze-slim-fill.py <directory> <summary.json>`.
- [Capture script](capture-slim-fill.ps1): saved exact capture protocol; local repo/APK paths are explicit.
- [Artifact hashes](artifacts.sha256): integrity of the saved evidence.
- [Local and generated-code evidence](../evidence.md): scope, tests and cause.
