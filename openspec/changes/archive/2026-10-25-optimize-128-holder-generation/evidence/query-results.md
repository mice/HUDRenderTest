# 128-holder query reuse — 2026-10-25

**Result: the specified 128-holder synchronous workload meets the p95 ≤ 1.5 ms target in both candidate runs.** This is a scoped CPU optimization result; the broader RC acceptance matrix remains open. Burst/SIMD generation and bucket scheduling were not implemented.

Mi 10, Release IL2CPP ARM64 Vulkan, Slim, 128 holders × 3 null-Sprite Simple quads, seed 12345, Partial 10%, full refresh every 30 frames, target 60 FPS. Each mode warms for 60 frames and samples 3,000 frames. ABBA order: live queries, batch query reuse, batch query reuse, live queries. Each run has 100 full refreshes; 36,000 sampled frames total. Per-holder/Image and quad-write timing are disabled in both variants, while outer generation + merge/upload timers remain enabled. Scope preparation/disposal is inside the generation timer.

| Run | Variant | Full refreshes | Generation median | Merge median | Whole median | Whole p95 | Whole max |
|---|---|---:|---:|---:|---:|---:|---:|
| 1 | query-live | 100 | 1.5364 | 0.3175 | 1.8660 | 1.9776 | 2.0480 |
| 2 | query-batch | 100 | 0.8460 | 0.3140 | 1.1609 | 1.2971 | 1.5241 |
| 3 | query-batch | 100 | 0.8589 | 0.3161 | 1.1754 | 1.2310 | 1.3368 |
| 4 | query-live | 100 | 1.5403 | 0.3164 | 1.8609 | 1.9853 | 2.0452 |

All numbers in ms; p95 uses nearest rank. Paired whole-refresh median reductions: **37.8% / 36.8%**. Candidate p95 values are 1.2971 / 1.2310 ms. The first candidate run has one refresh above 1.5 ms (max 1.5241 ms); the second has none. This is a p95 pass, not a hard per-frame maximum guarantee.

Partial whole-update medians are 0.4978 / 0.4975 ms for live queries and 0.4409 / 0.4421 ms for batch reuse. Generation, rather than merge/upload, accounts for the full-refresh improvement. Every refresh updates 128 holders, produces 1,536 vertices / 2,304 indices, and retains matching update selection/checksums and batch counts. Candidate refresh frames record three active generation scopes; reference frames record zero. Local full-output comparisons cover Managed/Slim, Sprite/null Sprite, live dimensions/color/child transforms, Canvas/root changes between scopes and fallback geometry.

Both APKs use source fingerprint `D0F4E64471CB76132051873BDF08BF169376D3CB9EBBDCAF88E4095C836ADD11`; hashes are in [build records](query-builds.json). Source status is an explicitly recorded dirty working tree, not a release candidate. Battery temperature across the sequence was approximately 30.2–31.2°C; thermal status stayed 0. Main-thread counters were available; scoped allocation and frame GC counters were unavailable. The warm zero-allocation result is local test evidence only.

## Implementation and decision

`UIImage.BeginGenerationBatch(root)` caches Canvas lookup, ancestor membership and root inverse matrix for a caller-bounded batch. Image state and child transforms remain live. The caller must preserve hierarchy/Canvas availability/root transform until disposal and begin a new scope after changes. The API remains opt-in; ordinary calls preserve their behavior. See [package usage](../../../../../com.uidatarender/Documentation~/ImageGenerationBatch.md).

The earlier diagnostic ABBA sequence showed that per-holder/Image instrumentation nearly doubled measured refresh cost; see [diagnostic evidence](stages-results.md). That reduction is not counted as the runtime optimization gain above, because both query variants use the same diagnostic settings.

The original implementation plan preselected a pure-data Burst prototype and ARM64 inspection. The measured query optimization already meets the user's prioritized 1.5 ms target, so those experiments are deferred to JOB-01 instead of becoming mandatory work in this change. No Burst kernel, scheduled generation Job or SIMD machine-code verification is claimed. The retained strategy is synchronous query reuse. Two-frame bucket scheduling remains a separate planned change and has not been performance-validated.

## Reproduction and validation

- [Raw JSON/CSV/metadata and provenance](query-reports.zip), [summary](query-summary.json), [source manifest](query-sources.sha256), [capture script](capture-queries.ps1), [analyzer](analyze-queries.py), [integrity hashes](artifacts.sha256).
- Extract the zip, then run `python analyze-queries.py <directory> <summary.json>`; configuration, APK/source/report hashes, geometry/workload, availability flags, CSV correspondence and 100 full refreshes per run are checked.
- Local regression: [150/150 EditMode](query-editmode.xml), [29/29 PlayMode](query-playmode.xml); [source hashes](query-validation.json). Package layout, governance 179/179 and OpenSpec strict validation passed. No new coverage, clean-checkout, Jenkins or release/tag evidence is claimed.
- `UT_UIIMAGE_009/010/011` cover output, scope lifetime and positively calibrated warm allocation; `UT_PERF_018` covers runner configuration snapshots, unchanged workload, scope execution, unavailable fields/CSV and global state restoration.

One build attempt was canceled at its owned SDK network query; the successful rebuild used per-process Java connection/read timeouts. The two completed comparison builds have identical source manifests. No SDK installation or committed scene was changed for the experiment.
