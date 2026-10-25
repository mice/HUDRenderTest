# 128-holder synchronous baseline — 2026-10-21

Two repeat runs on Mi 10, Release IL2CPP ARM64 Vulkan. Slim, no Sprite, 128 holders × 3 quads; 1536 vertices / 2304 indices. Partial updates 13 holders per frame; Burst updates all 128 in the same frame every 30 frames. Each mode warms for 60 frames and records 600 frames at target 60 FPS; seed 12345; fine quad timestamps off. No bucketing or deferred updates are implemented.

Both reports and paired CSVs passed validation: 3,600 frames, including 20 actual full refreshes per run, identical workload/selection and APK/source fingerprint. APK build and installation succeeded. The only new build-tool behavior is forwarding HolderCount to the temporary scene; committed scene/PlayerSettings are preserved. Full local regression immediately before this build-option addition was 147/147 EditMode and 28/28 PlayMode; the new option is verified by the actual Player report configuration, not a claimed new full test run.

## Results

Cells are **median / p95 (ms)**, nearest rank. Regeneration is generation + merge for each frame before quantile calculation; merge includes fill and upload.

| Run | Mode | Generation | Merge | Regeneration |
|---|---|---:|---:|---:|
| 1 | Partial | 0.3845 / 0.4296 | 0.2907 / 0.3243 | 0.6795 / 0.7527 |
| 1 | Burst-refresh | 3.3193 / 3.4275 | 0.3173 / 0.3653 | 3.6591 / 3.7467 |
| 2 | Partial | 0.3812 / 0.4324 | 0.2882 / 0.3228 | 0.6719 / 0.7595 |
| 2 | Burst-refresh | 3.3260 / 3.4472 | 0.3181 / 0.3286 | 3.6510 / 3.7664 |

Full-refresh generation is 3.3193–3.3260 ms median, merge 0.3173–0.3181 ms median. Merge-fill is only 0.1206–0.1212 ms and upload 0.0940–0.0948 ms median. Generation dominates; optimizing merge alone cannot reach the new whole-update target. Partial maxima are 1.1121/1.1194 ms, so a sub-1-ms median/p95 is not a hard worst-case guarantee.

## Decision and user target

The user set the next optimization target to **128 holders, whole-update 1.5 ms**, and prioritized optimization over implementing buckets. Continue the existing p95 measurement convention: generation + merge p95 ≤1.5 ms is the working optimization criterion, not merely per-mesh fill time. From current 3.7467/3.7664 ms p95 this needs about 60% reduction. This is a new optimization target, not acceptance of the current build or an RC budget exception.

2×128 two-frame rotation remains a planned follow-up, with one extra frame of latency accepted for the second bucket. The 128-holder synchronous result does not measure scheduler overhead, two meshes being drawn together, or deferred-state correctness; it must not be advertised as measured bucket performance. Historical 256-holder 7.04–7.09 ms results provide context only: no new matched 256 build was run here.

## Environment and limitations

Battery 100%, 27.5–27.7 °C; thermal status 0 before/after both runs. Maximum frame intervals were 17.9528/18.0743 ms. Main-thread counter valid for all frames; scoped allocation and frame GC unavailable in every device frame (-1 / unavailable), so no device zero-GC claim. Stay-awake setting restored to original 0; the 128-holder APK remains installed and stopped. Dirty working tree based on 3a6f553, no RC, coverage or Jenkins acceptance.

## Reproduce

- [Raw JSON/CSV/metadata and provenance](holders128-reports.zip).
- [Summary](holders128-summary.json), [APK metadata](android-builds.json), [source manifest](holders128-sources.sha256).
- Extract the ZIP and run [analyzer](analyze-holders128.py) with `<directory> <summary.json>`.
- [Capture script](capture-holders128.ps1), [integrity hashes](artifacts.sha256).
