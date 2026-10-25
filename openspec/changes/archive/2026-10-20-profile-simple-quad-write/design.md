## Context

`UIImage` now emits null-Sprite Simple Images directly. The aggregate `SimpleMeshWriteMs` field does not show which array writes dominate on the target device.

## Goals / Non-Goals

**Goals:** record storage, vertex, UV, color, and index timing for both mesh data implementations when diagnostics are enabled.

**Non-Goals:** change output, introduce allocations, or alter UGUI fallback behavior.

## Decisions

Use a diagnostics-only struct and interface owned by the package. `UIImage` reads the result after `WriteSimpleQuad`; existing `IUIData` remains unchanged. Timing is disabled by default.

### Measurement validity repair (2026-10-20)

- Keep profiler markers and restore timestamp sampling behind both `BuildMeshDiagnostics.Enabled` and the separate `ProfileSimpleQuadWrites` opt-in. The runner snapshots `configuration.profileQuadWrites` (default false), and restores both prior diagnostic settings on completion, reset or failure.
- Each successfully measured quad records `RecordedWrites=1`; disabled calls clear the previous result. UIImage and holders also clear prior timings when disabled or falling back, preventing stale data from crossing frames.
- Aggregate counts with timings through UIImage and holders. A frame is valid only when profiling was requested and all expected quads were recorded. Idle frames and disabled/incomplete measurement use `buildMeshWriteAvailable=false`, JSON values `-1`, and CSV `unavailable`.
- Use report schema `hud-rc-v2` to distinguish this validity contract from legacy default zeros. Export sample counts, the captured configuration and `Stopwatch.Frequency`; retain aggregate mesh-write timing.
- Timestamp and marker overhead remain part of enclosing timings. Compare the same Managed/Slim workload with profiling off/on and repeat each run; substage measurements guide investigation and do not independently establish a product performance gain.

## Risks / Trade-offs

- [Instrumentation overhead] → Enabled only by the HUD RC runner and reported separately from normal rendering.
- [Tiny stages below timer resolution] → Treat values as diagnostic estimates and compare medians across repeated runs.
