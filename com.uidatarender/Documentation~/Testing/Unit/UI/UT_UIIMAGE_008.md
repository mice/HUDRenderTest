---
testId: UT_UIIMAGE_008
module: UI
testType: EditMode
target: UI draw target
testFile: Tests/EditMode/TestUIImage.cs
testMethod: SpriteHolder_FastAndFallbackMatchForManagedAndSlimUnderParentTransform
status: active
lastRun: ""
lastResult: unknown
---

# UT_UIIMAGE_008

## Requirement
Sprite-backed fast and forced fallback output match through Managed and Slim holders under parent transforms.

## Risk
Slim Sprite geometry or color/UV/topology could diverge from the fallback.

## Steps
Compare complete output for both mesh data implementations with nontrivial transforms and color; restore diagnostic switches and destroy owned objects.

## Assertions
See the governed test method; no timing threshold is asserted.

## Expected Result
All output, state and lifetime assertions pass.

## Latest Run Result
2026-10-20: passed in full EditMode 147/147 and PlayMode 28/28 regression.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-21-optimize-uiimage-simple-mesh/evidence.md
