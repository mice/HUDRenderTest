## Why

The Xiaomi 10 HUD workload uses null-Sprite Images, whose texture index is always -1. `UIImage.GetTextureIndex` still rewrites cache state and reads the global texture version on every `DoGenerate`, and `DataPrefabHolder.BuildMesh` repeatedly resolves the same target Transform for every draw.

## What Changes

- Add an early return for an already-known null-sprite texture state.
- Resolve the holder target Transform once per `BuildMesh` call.
- Preserve texture registration behavior when a Sprite or texture appears later.
- Add regression coverage and Xiaomi stage evidence.

## Capabilities

### New Capabilities

- `ui-image-null-texture-cache`: Avoid repeated null-texture lookup work while preserving invalidation.

### Modified Capabilities

None.

## Impact

Affected runtime code is `UIImage` and `DataPrefabHolder<T>`. No public API or geometry output changes.
