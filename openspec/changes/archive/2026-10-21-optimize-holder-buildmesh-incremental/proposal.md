## Why

Xiaomi 10 Release IL2CPP profiling shows that repeated holder refreshes spend measurable CPU time re-planning the same texture batches and rebuilding identical local slot maps. The holder list and each holder's texture-slot membership normally remain stable while geometry is refreshed, so this work can be reused safely until that structure changes.

## What Changes

- Cache `MergeBatcher.Plan` results while holder identity, order, mesh count, and texture-slot membership remain unchanged.
- Cache each `RenderBatch` local slot ordering and map until its slot set changes.
- Preserve the existing full planning path whenever the holder structure or slot membership changes.
- Add deterministic tests for cache hits, invalidation, and equivalent batch output.
- Add profiler-visible plan-cache hit information for the Xiaomi 10 performance evidence.

## Capabilities

### New Capabilities

- `holder-batch-plan-cache`: Reuse holder batch planning and local slot maps when their structural inputs are unchanged.

### Modified Capabilities

None. Existing rendering and batch partition behavior remains unchanged.

## Impact

Affected runtime code is limited to `MergeBatcher`, `RenderBatch`, and their renderer tests. No Unity UGUI source, shader, public holder API, or batch partition rule changes. The cache retains holder and slot metadata for the renderer lifetime and is invalidated by structural changes.
