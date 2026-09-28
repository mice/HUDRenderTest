## Context

`UIImage.DoGenerate` currently invokes Unity UI geometry generation and then copies the resulting `VertexHelper` data into package mesh storage. The Xiaomi 10 Release IL2CPP profile shows this repeated path dominates full refresh generation. A standard Simple Image has a stable rectangular topology and can be emitted directly, while Image modes such as Filled, Sliced, and Tiled require UGUI's established geometry rules.

## Goals / Non-Goals

**Goals:**

- Detect only the safe standard Simple Image case.
- Write equivalent vertices, UVs, colors, indices, and texture index data into the existing `IUIData` path.
- Preserve transform handling and the existing UGUI fallback.
- Make the fast path observable and covered by deterministic tests.

**Non-Goals:**

- Modifying Unity's `Image`, `Graphic`, `Graphics`, or `VertexHelper` source.
- Replacing text, filled, sliced, tiled, or custom mesh generation.
- Introducing Burst or NativeArray code into Unity UI callbacks.

## Decisions

### Fast path at `UIImage` application layer

The branch belongs in `UIImage.DoGenerate`, before `UpdateGeometry`, so supported images avoid `Graphics.UpdateGeometry` and the VertexHelper copy. The existing call remains the fallback. This limits behavior changes to `UIImage` and avoids maintaining a fork of UGUI.

### Explicit eligibility predicate

The fast path is allowed only for a non-null sprite with `Image.Type.Simple`, no preserving-aspect behavior that changes the rectangle, and the package's supported rectangular sprite geometry. Any uncertain state falls back to UGUI. The predicate is tested independently so future Image property changes cannot silently broaden the optimization.

### Existing mesh-data write contract

The implementation uses a small application-layer write method on the mesh-data abstraction (or an equivalent existing method) so both `UIMeshData` and `UIMeshDataX` receive the same canonical four-vertex/six-index result. Transform application remains after extraction, as in the current path.

### Equivalence before performance

Tests compare the fast path and UGUI fallback for positions, UVs, colors, indices, texture index, and transformed output. Performance evidence is collected only after equivalence tests pass.

## Risks / Trade-offs

- [Sprite UV or pivot mismatch] → Use Unity sprite outer UV and rect/pivot data; fall back when the supported assumptions do not hold.
- [Image property combinations expand over time] → Keep the eligibility predicate explicit and add a regression test for each unsupported mode.
- [Fast path changes package output] → Compare against the fallback in EditMode before accepting performance results.
- [Scene workload uses unsupported images] → Keep fallback intact and report fast-path hit rate in profiling.

## Migration Plan

Implement behind the internal predicate, run package EditMode tests, then run the HUD sample on Xiaomi 10 with the same Release IL2CPP configuration. If equivalence or performance regresses, disable the predicate and retain the unchanged fallback.

## Open Questions

- Whether `preserveAspect` must always force fallback or can be represented exactly for the target workload.
- Whether the direct write API should be a new interface member or an internal helper shared by the two mesh-data implementations.
