## Why

TMP scratch reuse removed observed warm HUD allocation events, but representative rotating generation still costs about 8 ms per refresh frame. Existing generation timing includes submission, scheduling and publication, while rotating mode disables detailed stages, so current evidence cannot identify the remaining cost safely.

## What Changes

- Add default-off rotating-stage diagnostics using existing timing infrastructure and actual completed-holder aggregates, including Canvas update, TMP mesh regeneration, extraction and transform boundaries.
- Preserve deterministic workload and output with diagnostics on/off, report actual rendering dimensions and quantify probe overhead with the same APK before interpreting phase timings.
- Accept GC through a separate Unity Profiler capture and known-allocation control, without adding production allocation-counting or byte-measurement machinery. Unsupported scoped bytes remain unavailable.
- Record a bounded Mi 10 comparison and remaining stock-scene, budget, exact-RC and coverage gates. This is measurement work, not a generation optimization or release acceptance.

## Capabilities

### New Capabilities

- `representative-generation-profiling`: Valid, opt-in generation phase evidence for rotating representative workloads, with trace equivalence, overhead controls and separate Profiler GC acceptance.

### Modified Capabilities

None. Existing buildmesh profiling and HUD acceptance contracts continue to apply.

## Impact

Runtime diagnostic timing contracts, `UITMPText`, holder stage aggregation, Sample rotating runner/reporting and build configuration, governed correctness tests, local capture/analysis artifacts and PERF-04 documents. Reuse Unity 2022.3.53f1 and the existing stock validation worktree; no dependency/version changes, runtime GC counters, cached geometry, budget changes, Jenkins run, RC/tag or publication.
