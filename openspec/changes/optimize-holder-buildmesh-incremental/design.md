## Context

`HolderBatchRenderer.Rebuild` currently calls `MergeBatcher.Plan` and `RenderBatch.BuildLocalSlotMap` on every rebuild. The holder collection is supplied by callers as an ordered read-only list, while each holder exposes mesh data whose `TextureIndex` values determine partitioning. Geometry changes do not necessarily change this structural input. The optimization must retain the existing greedy partition algorithm and avoid modifying UGUI or holder-facing APIs.

## Goals / Non-Goals

**Goals:**

- Detect when the ordered holder identities, mesh-data counts, and texture-slot membership are unchanged.
- Reuse the exact `RenderBatch` objects and their local slot maps for those rebuilds.
- Invalidate and recompute the cache when any structural input changes.
- Make cache hits measurable without adding allocations to the steady-state path.

**Non-Goals:**

- Incrementally updating only part of a Unity `Mesh` upload.
- Changing batch ordering, slot limits, or texture binding semantics.
- Guessing geometry dirtiness from mutable draw target state.
- Changing the public `HolderBatchRenderer.Rebuild` signature.

## Decisions

### Cache ownership stays in `MergeBatcher`

`MergeBatcher` already owns the pooled `RenderBatch` instances and is the single authority for partitioning. It will retain a structural fingerprint of the last holder list and compare it before planning. A renderer-level cache would duplicate the partitioning contract and make direct batcher tests harder.

### Fingerprint uses identity and slot values

The fingerprint compares holder reference identity and order, each holder's `UIMeshDatas.Count`, and every non-zero `TextureIndex` value in order. This detects changes that affect `CollectSlots` while allowing ordinary vertex and transform changes to reuse planning. It uses reusable arrays/lists and no LINQ or per-frame hash allocation.

### Local slot maps are cached by RenderBatch

`RenderBatch.BuildLocalSlotMap` will maintain a dirty flag set when `Reset` or `AddSlots` changes `UsedSlots`. If clean, it returns the existing dictionary directly. Sorting occurs only after a structural change.

### Cache miss remains behaviorally identical

On a miss, `Plan` clears and repopulates batches using the current greedy first-fit logic, then records the fingerprint. On a hit, it returns the existing batch list without calling `CollectSlots`, `TryAdd`, or resetting pooled batches. This keeps batch identity and ordering stable.

### Observability is a lightweight counter

`MergeBatcher` exposes `LastPlanCacheHit` and cumulative hit/miss counters for tests and profiler integration. The renderer wraps the plan operation with the existing marker and does not add a new marker per holder.

## Risks / Trade-offs

- [A mutable mesh data changes a slot without a rebuild-triggered miss] → Fingerprint is checked on every `Plan` call and includes all mesh slot values.
- [Holder list contents are mutated in place] → Compare count and ordered object references every call.
- [Dictionary order affects local slot mapping] → Existing sorted global slot order is preserved; cached output is identical to a fresh build.
- [Cache retains references longer than one plan] → It is bounded by the latest holder list and cleared when the batcher is disposed or receives an empty list.

## Migration Plan

Implement the cache behind the existing APIs, add EditMode equivalence and invalidation tests, run package validation, then collect a Xiaomi 10 Release IL2CPP measurement. If any mismatch appears, disable reuse by forcing a plan miss while retaining the original planner.

## Open Questions

None for this scoped change. Incremental geometry generation and partial mesh uploads remain separate work.
