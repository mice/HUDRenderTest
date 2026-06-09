## Overview

The migration adds a parallel TMP text path instead of replacing `UIText` immediately. The first implementation stage proves that `TextMeshProUGUI` output can enter the current `IUIDrawTarget` and `IUIData` pipeline and render through the existing batch renderer with Text-equivalent results.

## Current Constraints

- `UIText` captures legacy `Text` geometry through `OnPopulateMesh(VertexHelper)`.
- `uv0.z` stores texture slot and `uv0.w` stores a text flag.
- `Hidden/UIE-AtlasBlit` treats text as an alpha font atlas bound to `_MainTex0`.
- `MergeBatcher` only splits on `TextureIndex > 0`, so current text is outside image texture slot accounting.
- TMP can generate multiple submeshes for fallback fonts, sprite assets, and material variants; the first stage will not attempt to flatten all of those cases.

## Proposed Architecture

Add a TMP-specific draw target, tentatively `UITMPText : TextMeshProUGUI, IUIDrawTarget`. Its `DoGenerate` implementation will call TMP mesh generation, read TMP mesh data, and populate an `IUIData` implementation or adapter suitable for the current merge path.

For the low-level goal, TMP text will be constrained to one TMP font asset and one material atlas. The adapter will copy vertices, colors, uv0, and triangles from the primary TMP mesh. Texture registration will bind the TMP font atlas as the text texture for rendering. Legacy `UIText` remains unchanged and can coexist with TMP draw targets in the same project.

For the advanced goal, introduce a TMP-aware shader path rather than forcing SDF through the current alpha-only shader. This stage will preserve SDF sampling behavior and expose a path for outline support. Multi-atlas fallback and TMP sprites should be treated as explicit follow-up requirements because they require either per-submesh materials or an expanded texture slot model.

## Migration Strategy

1. Add TMP dependency and imported resources.
2. Add new TMP draw target and tests without touching existing `UIText` behavior.
3. Update registration APIs to target a common text interface where needed.
4. Add a dedicated sample scene or duplicate an existing scene using TMP.
5. Compare TMP output against legacy Text for basic text, color, alignment, and transform cases.
6. Extend shader support for SDF/outline after the low-level target is stable.

## Risks

- TMP package resolution and Essential Resources must be present in CI and clean clones.
- TMP uses font assets, materials, atlas textures, and submesh data that do not map one-to-one to legacy `Font`.
- Reusing `uv0.z/w` may conflict with TMP shader needs; advanced SDF support may require additional vertex streams or different packing.
- Scene/prefab migration is serialized Unity asset work and must be done on the feature branch only.
- Outline behavior differs: legacy `UIOutline` is a UGUI modifier, while TMP outline is primarily material/shader-driven.

## Validation

- OpenSpec validation for the change.
- Unity package resolution confirms `com.unity.textmeshpro` in lock file and PackageCache.
- EditMode tests for TMP mesh extraction and `SetText` regeneration.
- PlayMode or sample-scene test for visible batch rendering.
- Regression validation keeps current legacy Text tests passing.
