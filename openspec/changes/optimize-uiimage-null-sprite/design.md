## Context

Unity `Image.OnPopulateMesh` delegates to `Graphic.OnPopulateMesh` when `activeSprite` is null. That implementation emits a pixel-adjusted rectangle with UV 0..1 and two triangles. The current `UIImage` excludes null sprites from its direct path and pays for the VertexHelper extraction round trip.

## Goals / Non-Goals

**Goals:**

- Reproduce the null-Sprite Simple Image output in `UIImage` using the existing `IUIData.WriteSimpleQuad` contract.
- Keep transform handling, texture index `-1`, color, and fallback behavior unchanged.
- Make the direct path visible in existing stage timings.

**Non-Goals:**

- Changing sprite-backed Image behavior.
- Replacing Filled, Sliced, Tiled, or `useSpriteMesh` generation.
- Forking Unity UGUI source.

## Decisions

### Use Graphic's rectangle contract

For a null Sprite, use `GetPixelAdjustedRect()` directly and UV `(0,0,1,1)`, matching the inherited `Graphic.OnPopulateMesh` implementation. `preserveAspect` has no effect in this UGUI branch and does not block the null-Sprite path.

### Keep eligibility explicit

The path requires `type == Simple`, `sprite == null`, and `!useSpriteMesh`. All other cases continue through the current fallback or sprite-backed fast path.

### Verify before measuring

Tests compare all geometry channels and transformed output against fallback for managed and Slim mesh data before collecting device evidence.

## Risks / Trade-offs

- [Pixel adjustment differs under unusual Canvas settings] → Compare direct and fallback output in EditMode using the same RectTransform and Canvas setup.
- [Future UGUI changes alter null-sprite behavior] → Keep the equivalence test and explicit fallback switch.

## Migration Plan

Implement behind the existing fast-path switch, run tests, then run the same Xiaomi 10 Release IL2CPP workload and compare UGUI fallback versus direct-path stage shares.
