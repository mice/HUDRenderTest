## ADDED Requirements

### Requirement: BuildMesh exposes stage timing

The holder generation path SHALL expose aggregate, allocation-free timings for setup, draw-target generation, direct Simple Image generation, fallback generation, and transform work when diagnostics are enabled.

#### Scenario: Stage timings are recorded for a refresh
- **WHEN** the HUD RC runner performs a Partial or Burst refresh with stage diagnostics enabled
- **THEN** the report contains non-negative timings whose measured generation stages are bounded by the existing BuildMesh/generation timing within measurement noise.

#### Scenario: Diagnostics are disabled
- **WHEN** normal rendering calls `BuildMesh` without stage diagnostics
- **THEN** rendering output and public APIs remain unchanged and no per-call diagnostic allocation is introduced.

### Requirement: Simple Image helpers preserve supported output

The application-layer Simple Image implementation SHALL preserve the existing eligible Image output while keeping UGUI fallback for unsupported modes.

#### Scenario: Eligible Simple Image remains equivalent
- **WHEN** a non-null, non-filled, non-sliced, non-tiled Simple Image is generated through the direct path
- **THEN** positions, UVs, colors, texture index, indices, and transform output match the existing UGUI-equivalent fast-path contract.

#### Scenario: Complex Image uses fallback
- **WHEN** an Image is Filled, Sliced, Tiled, custom, null-sprite, or otherwise outside the explicit predicate
- **THEN** `UIImage` uses the inherited UGUI generation path.
