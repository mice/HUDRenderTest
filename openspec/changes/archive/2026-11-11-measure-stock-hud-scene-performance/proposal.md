## Why

PERF-04 has representative custom UGUI timing and generation attribution, but lacks a comparable stock UGUI scene measurement. A dependency-controlled comparison is needed before assessing the remaining generation cost or broadening compatibility claims.

## What Changes

- Measure the existing repository Sprite/TMP/multi-texture workload on real custom and stock UGUI Release Players with all diagnostic probes disabled.
- Freeze and audit executable inputs, resolved UGUI identity, APK identity, requests, scheduling, geometry and actual rendering dimensions before accepting comparisons.
- Validate stock functionality and inspect scoped Main Thread allocations through a separate Unity Profiler capture using temporary local tooling.
- Publish run-level timing conclusions and maintain PERF-04 follow-up scope without adding production GC instrumentation.

## Capabilities

### New Capabilities

- `stock-hud-scene-performance`: Reproducible representative scene comparison and bounded allocation evidence for the actual stock dependency path.

### Modified Capabilities

None.

## Impact

Uses the existing sample, Unity 2022.3.53f1, reusable stock validation worktree and Mi 10. Raw captures, APKs and local analysis tools remain ignored; specifications and final conclusions are repository documents. No runtime API, package version, performance budget, PERF-04/M5 exit, RC, tag or release approval changes are included.
