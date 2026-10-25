# UIImage Null Sprite Quad Specification

## Purpose

Define direct quad generation for eligible null-Sprite Simple Images while preserving UGUI output and fallback behavior.

## Requirements

### Requirement: Generate null-Sprite Simple Images directly

`UIImage` SHALL generate a null-Sprite Simple Image as a pixel-adjusted four-vertex quad with UV 0..1, the current color, texture index -1, and indices 0,1,2,2,3,0.

#### Scenario: Null-Sprite direct output matches UGUI
- **WHEN** a Simple Image has no Sprite and `useSpriteMesh` is false
- **THEN** direct output matches the inherited UGUI fallback for positions, UVs, colors, indices, texture index, and parent transform.

#### Scenario: Unsupported modes remain fallback
- **WHEN** the Image is Filled, Sliced, Tiled, sprite-mesh, or sprite-backed outside the existing predicate
- **THEN** `UIImage` uses the existing supported path or UGUI fallback without changing output.
