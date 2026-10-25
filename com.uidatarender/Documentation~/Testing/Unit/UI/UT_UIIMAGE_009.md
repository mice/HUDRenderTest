---
testId: UT_UIIMAGE_009
module: UI
testType: EditMode
target: UI draw target
testFile: Tests/EditMode/TestUIImage.cs
testMethod: GenerationBatch_MatchesLiveGeneration_AcrossDynamicInputsAndNewHierarchies
status: active
lastRun: ""
lastResult: unknown
---

# UT_UIIMAGE_009

## Requirement
Batch generation matches live output across Sprite/null Sprite, colors, dimensions, child transforms, active/inactive Canvas and hierarchy changes between scopes.

## Risk
Cached hierarchy state could become stale or scope lifetime could affect later generation.

## Steps
Exercise the governed test method using owned objects and dispose all generated meshes.

## Assertions
Compare behavior with independent live generation or validate scope ownership exceptions, without timing thresholds.

## Expected Result
All assertions pass.

## Latest Run Result
2026-10-25: passed; full regression 150/150 EditMode and 29/29 PlayMode.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-25-optimize-128-holder-generation/design.md
