## Context

Both Slim fill methods compare every stored color with `default(Color32)`. The sampled IL2CPP implementation boxes the argument and invokes `ValueType_Equals`; geometry storage itself is ordinary managed arrays. The renderer calls holder Fill for all meshes in a rebuilt batch, so even a Partial refresh processes the full merged geometry.

## Goals / Non-Goals

**Goals:** remove this allocation/cost while preserving exact legacy Slim color fallback, transforms, UV channels and index rebasing; prove it with pre/post tests, generated-code inspection and device A/B.

**Non-Goals:** changing transparent-black sentinel semantics, List growth policy, storage layout, batching, diagnostics, Managed output or whole-application GC guarantees.

## Decisions

Read the stored Color32 once and check all four bytes for zero. Share a private helper between FillToDrawData and FillWithMatrix. Avoid unsafe reinterpretation, generic equality or changes to loops/List APIs; the minimal diff isolates the confirmed boxing source.

Capture the old generated functions and run the new allocation regression before changing Runtime. Use a positively calibrated current-thread GC.Alloc recorder, preallocated output lists and warmed calls. Verify default, single nonzero channel, opaque black and transparent colored values, with nonzero shared offsets, seeded destinations and nontrivial transforms.

Build a fixed Slim Release IL2CPP ARM64 APK with the existing 256×3/60 FPS/600-frame workload and quad timing off. Compare old/new/new/old on the same Mi 10; preserve APK/source hashes, reports and temperature. The earlier baseline APK is identified by its existing source manifest, not relabelled as a current clean candidate.

## Risks / Trade-offs

- [All-zero color currently means white, including transparent black] → Preserve and test it explicitly; semantic redesign needs a separate change.
- [Instrumentation and device drift] → Keep configuration fixed, use ABBA repeats, record thermal state; distinguish Editor allocation evidence from unavailable IL2CPP counters.
- [Unrelated working-tree changes] → Compare Runtime/runner hashes to the baseline and document the exact changed file.

## Migration Plan

No public API migration. Revert the color-check helper and its two call sites to roll back; retain evidence and tests documenting the allocation regression.
