## 1. Plan cache implementation

- [x] 1.1 Add reusable structural fingerprinting and cache hit/miss state to `MergeBatcher` without changing its public planning contract.
- [x] 1.2 Cache `RenderBatch` local slot maps and invalidate them when used slots change.
- [x] 1.3 Expose lightweight plan cache counters/state for renderer profiling and ensure empty inputs clear stale cached references.

## 2. Verification and evidence

- [x] 2.1 Add EditMode tests covering stable cache hits, geometry-only stability, holder/order invalidation, texture-slot invalidation, and cached output equivalence.
- [x] 2.2 Run package EditMode/layout validation and record the result in the change evidence.
- [x] 2.3 Update roadmap/todo records with the optimization result and remaining Xiaomi 10 performance follow-up.
