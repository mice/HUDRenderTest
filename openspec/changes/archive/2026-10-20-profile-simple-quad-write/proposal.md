## Why

The null-Sprite direct quad removed UGUI fallback but only reduced generation by about 4%. Its mesh write stage is still measurable, and the current report cannot distinguish storage checks from vertex, UV, color, or index writes.

## What Changes

- Add allocation-free opt-in stage timings for `WriteSimpleQuad` in managed and Slim mesh data.
- Export the substage timings in the HUD RC report.
- Preserve geometry and public interfaces.
- Record Xiaomi 10 evidence to choose the next optimization.

## Capabilities

### New Capabilities

- `simple-quad-write-profiling`: Substage timing for direct quad writes.

### Modified Capabilities

None.

## Impact

Only diagnostics, `UIMeshData`, `UIMeshDataX`, `UIImage`, and the performance report are affected.
