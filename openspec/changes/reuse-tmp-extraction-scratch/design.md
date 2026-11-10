## Context

TMPMeshDataUtility.Fill constructs and disposes VertexHelper for each extraction. PERF-04 observes one event per completed representative holder. Unity APIs and existing extraction scratch are main-thread APIs; both custom and stock UGUI must remain supported.

## Goals / Non-Goals

Goals: Remove warm nonnested scratch allocations, preserve every emitted channel and topology, cover changed/empty text and both storage modes, and retain trustworthy allocation measurements.

Non-goals: Caching text geometry, skipping ForceMeshUpdate, changing fixture configuration or performance budgets, accepting RC or closing PERF-04.

## Decisions

- Retain one initialized VertexHelper in the utility. Component-owned helpers would multiply retained capacity and omit direct Fill callers. Initialize through AddVert/Clear so custom UGUI's empty CopyIndices path sees initialized lists.
- Clear in finally; nested extraction uses a separate disposable helper. Cleanup requested during extraction is deferred until the outer Fill completes. Public ReleaseScratch is main-thread-only and idempotent. Register subsystem, application quit, editor reload and editor quit cleanup.
- Compare output against independently traversed TMP character/mesh data. Positive-controlled GC.Alloc event counts isolate Fill, then full fixed/changing DoGenerate. No duration values are converted to bytes.
- Preserve baseline APKs; build candidate APKs separately. Use current-session Release ABBA comparisons and separate Development allocation checks, verify deterministic requests/checksums, thermal state and restored application state.

## Risks / Trade-offs

- Retained high-water helper capacity → explicit release and lifecycle hooks; document pooling may retain underlying lists.
- Nested calls can allocate → bound the zero-event guarantee to warmed nonnested extraction; verify outer geometry survives nesting and exceptions.
- TMP internals or storage growth can allocate → distinguish extraction from full generation and fixed-capacity from growth measurements.
- Device noise → matched configuration, interleaved binaries and raw local evidence; no claim based on historical timing alone.

## Migration Plan

Add a failing allocation regression, implement reuse, run focused and full original/stock validation, then paired device measurements. Reverting the utility change restores previous allocation behavior; no content migration is needed.

## Open Questions

Whether full DoGenerate has remaining allocations or a measurable CPU improvement is determined by validation and device results.
