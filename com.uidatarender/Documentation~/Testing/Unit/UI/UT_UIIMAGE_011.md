---
testId: UT_UIIMAGE_011
module: UI
testType: EditMode
target: UI draw target
testFile: Tests/EditMode/TestUIImage.cs
testMethod: GenerationBatch_WarmScopesAndGeneration_DoNotAllocate
status: active
lastRun: ""
lastResult: unknown
---

# UT_UIIMAGE_011

## Requirement
Warmed batch scope creation, generation and disposal allocate no managed memory for the supported Slim/null-Sprite workload.

## Risk
Cached hierarchy state could become stale or scope lifetime could affect later generation.

## Steps
Exercise the governed test method using owned objects and dispose all generated meshes.

## Assertions
Calibrate GC.Alloc with a positive control, then verify zero allocation events across 128 warm scope/generation/disposal cycles.

## Expected Result
All assertions pass.

## Latest Run Result
2026-10-25: passed; full regression 150/150 EditMode and 29/29 PlayMode.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-25-optimize-128-holder-generation/design.md
