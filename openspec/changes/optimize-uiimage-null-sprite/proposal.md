## Why

The Xiaomi 10 stage report shows the real HUD workload has standard `UIImage` components without Sprites, so they enter UGUI's generic quad fallback. That fallback is the largest measured `BuildMesh` stage even though its output is a fixed four-vertex rectangle.

## What Changes

- Add an application-layer direct quad path for null-Sprite Simple Images.
- Match UGUI `Graphic.OnPopulateMesh` rectangle, UV, color, and index output.
- Keep Filled, Sliced, Tiled, sprite-mesh, and other unsupported modes on the existing fallback.
- Add managed and Slim equivalence tests and Xiaomi 10 stage evidence.

## Capabilities

### New Capabilities

- `ui-image-null-sprite-quad`: Direct generation for the null-Sprite Simple Image case.

### Modified Capabilities

None.

## Impact

Only `UIImage`, its stage diagnostics, and EditMode tests are affected. No Unity UGUI source or public interface changes.
