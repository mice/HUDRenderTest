---
testId: UT_UI_003
module: UI
testType: EditMode
target: UITMPText
testFile: Assets/Tests/EditMode/TestUITMPText.cs
testMethod: DoGenerate_FillsMeshDataFromTMP
status: active
lastRun: ""
lastResult: unknown
---

# UT_UI_003

## Requirement
UITMPText generates mesh data from TextMeshProUGUI output.

## Risk
TMP adaptation could produce empty or unusable mesh data for the batch renderer.

## Steps
1. Create a UITMPText component with default TMP resources.
2. Call `DoGenerate` with capture data.

## Assertions
The test verifies non-empty vertices and indices, text flag assignment, texture index, and text atlas availability.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Unknown until the next Unity Test Runner execution.

## Notes
Created for TextMeshPro adapter coverage.
