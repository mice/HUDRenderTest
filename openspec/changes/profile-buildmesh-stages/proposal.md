## Why

The current `BuildMesh` report exposes only one aggregate duration, while Xiaomi 10 data shows it is the dominant generation cost. Without stage timings, repeated `DoGenerate`, mesh-data initialization, and holder-loop overhead cannot be ranked or optimized safely.

## What Changes

- Add allocation-free, opt-in stage instrumentation around `DataPrefabHolder.BuildMesh`.
- Record setup/resize, mesh-data initialization, draw-target generation, and loop overhead separately in the HUD RC report.
- Preserve the existing `BuildMesh` behavior and public API when diagnostics are disabled.
- Add EditMode coverage proving stage totals remain valid for managed and slim mesh data.
- Add stage-level evidence from the Xiaomi 10 Release IL2CPP runner.

## Capabilities

### New Capabilities

- `buildmesh-stage-profiling`: Stage-level timing and reporting for holder mesh generation.

### Modified Capabilities

None. This adds diagnostics and report detail without changing rendering semantics.

## Impact

Affected code is `DataPrefabHolder<T>`, `HudRcPerformanceRunner`, and related EditMode tests/evidence. The report schema gains stage timing fields. No Unity UGUI source, shader, or holder API changes are required.
