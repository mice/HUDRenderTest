## Context

`UIImage` now emits null-Sprite Simple Images directly. The aggregate `SimpleMeshWriteMs` field does not show which array writes dominate on the target device.

## Goals / Non-Goals

**Goals:** record storage, vertex, UV, color, and index timing for both mesh data implementations when diagnostics are enabled.

**Non-Goals:** change output, introduce allocations, or alter UGUI fallback behavior.

## Decisions

Use a diagnostics-only struct and interface owned by the package. `UIImage` reads the result after `WriteSimpleQuad`; existing `IUIData` remains unchanged. Timing is disabled by default.

## Risks / Trade-offs

- [Instrumentation overhead] → Enabled only by the HUD RC runner and reported separately from normal rendering.
- [Tiny stages below timer resolution] → Treat values as diagnostic estimates and compare medians across repeated runs.
