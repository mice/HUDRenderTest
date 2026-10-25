## Context

`UIImage.DoGenerate` already bypasses UGUI for supported Simple Images, but the direct path still combines source cache lookup, drawing rectangle calculation, mesh-data writes, and transform handling inside one profiler scope. Xiaomi 10 stage data cannot identify which part of the fast path remains expensive. The implementation must remain compatible with complex Image modes and avoid maintaining a full Unity `Image` fork.

## Goals / Non-Goals

**Goals:**

- Keep the direct Simple Image path in `UIImage` and split it into independently timed stages.
- Reuse the Image-equivalent drawing-dimension and UV rules for the supported Simple case.
- Expose per-stage timings in the HUD RC report without adding allocations.
- Preserve UGUI fallback for Filled, Sliced, Tiled, custom, and unsupported configurations. Eligible null-Sprite Simple Images now use the equivalent direct path from `optimize-uiimage-null-sprite`.

**Non-Goals:**

- Copying all Unity `Image` internals into the package.
- Replacing UGUI behavior for complex Image types.
- Changing the `IUIDrawTarget` or `IUIData` public contracts.
- Optimizing transform matrix calculation in this change.

## Decisions

### Expand only the supported Simple path

`UIImage` will own small methods equivalent to the Image operations needed by the existing fast path: cached sprite data, pixel-adjusted drawing dimensions, quad vertex/UV emission, and transform application. The existing `base.OnPopulateMesh` path remains untouched for unsupported modes.

### Stage timings stay at the application layer

`DataPrefabHolder.BuildMesh` will expose a reusable per-call diagnostic result populated by `UIImage` or other draw targets when enabled. The runner will aggregate setup, target dispatch, direct simple generation, fallback generation, and transform time. The default path leaves diagnostics disabled and uses the current profiler markers only.

### No full Image source fork

Copying Unity's entire `Image` implementation would increase maintenance risk and would not improve unsupported paths. Only the stable Simple Image contract needed by `WriteSimpleQuad` is expanded in `UIImage`; changes to UGUI's complex geometry remain handled by inheritance.

### Report stage totals, not per-image records

The runner has up to 768 draw targets per refresh. It will record aggregate stage milliseconds for the current BuildMesh call and preserve per-image profiler markers for interactive diagnosis. This keeps report size bounded and makes Xiaomi comparisons repeatable.

## Risks / Trade-offs

- [Direct geometry diverges from UGUI under a property combination] → Keep the existing eligibility predicate narrow and retain fallback tests.
- [Instrumentation changes timing] → Use timestamp counters only when the runner enables diagnostics; compare with the existing aggregate fields.
- [A target does not expose stage data] → Count it under dispatch/other and preserve generation totals.
- [Report schema changes break old readers] → Preserve aggregate stage fields; the subsequent `hud-rc-v2` contract uses explicit availability/counts and -1/unavailable for unsampled quad substages.

### Accounting verification (2026-10-20)

Test Managed and Slim holders with distinct direct, Filled fallback and non-UIImage targets. Verify exact per-image aggregation, outer monotonic timing bounds, cold/warm sprite-cache calls, null-Sprite transition, empty refresh and diagnostic disable. Transform is nested inside Simple/fallback; sprite lookup is nested inside drawing dimensions. Do not double count those nested stages or impose machine-speed thresholds.

Measure 256 warmed disabled-diagnostic holder calls outside NUnit assertions with a positively calibrated current-thread GC.Alloc recorder. Compare all output channels before and after disabling diagnostics, for sprite-backed and null-Sprite direct targets alongside fallback targets. This proves the tested warm path, not cold construction or whole-application zero GC.

## Migration Plan

Implement stage timing and Simple-path helpers, add equivalence tests, run EditMode validation, then build the Xiaomi 10 Release IL2CPP APK. Compare each BuildMesh stage against the current `2a9d23e` and `6d1d0cd` reports. If equivalence fails, keep the helpers for profiling but disable any new direct generation branch.
