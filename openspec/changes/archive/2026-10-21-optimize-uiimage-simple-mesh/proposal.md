## Why

Xiaomi 10 Release IL2CPP measurements show that Burst refresh generation is still dominated by repeated `UIImage`/UGUI mesh generation, with generation p95 above 10 ms. Standard non-filled, non-sliced Images use a predictable four-vertex topology, so they can avoid the UGUI extraction round trip while preserving the existing fallback path for complex Image types.

## What Changes

- Add an application-layer fast path for standard `UIImage` rectangular meshes.
- Keep UGUI generation as the fallback for filled, sliced, tiled, custom, or otherwise unsupported Image configurations.
- Add equivalence tests for vertices, UVs, colors, indices, texture slots, and transform behavior.
- Add profiler markers and a repeatable performance comparison for the fast and fallback paths.
- Do not modify Unity UGUI source files or change the public behavior of `Image`.

## Capabilities

### New Capabilities

- `ui-image-simple-mesh`: Direct generation and validation of supported standard UIImage geometry.

### Modified Capabilities

None. Existing public UGUI extraction requirements remain unchanged.

## Impact

Affected code is limited to `UIImage`, mesh-data write APIs if required, EditMode test fixtures, and performance evidence. Complex Image modes continue through stock UGUI. No package dependency or shader change is required.
