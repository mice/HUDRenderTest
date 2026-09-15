---
testId: UT_UI_004
module: UI
testType: EditMode
target: UI draw target
testFile: Tests/EditMode/TestUITMPText.cs
testMethod: DoGenerate_WithRootChild_AppliesRelativeTransform
status: active
lastRun: ""
lastResult: unknown
---

# UT_UI_004

## Requirement
UITMPText applies root-relative transforms consistently with the legacy Text path.

## Risk
TMP text could render at incorrect positions when batched under a holder root.

## Steps
1. Create root and child transforms.
2. Generate TMP text mesh data with the root supplied.

## Assertions
The test verifies one transform application and expected translation matrix values.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Unknown until the next Unity Test Runner execution.

## Notes
Created for TextMeshPro adapter coverage.
