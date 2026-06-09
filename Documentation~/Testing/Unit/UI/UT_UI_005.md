---
testId: UT_UI_005
module: UI
testType: EditMode
target: IUITextDrawTarget
testFile: Assets/Tests/EditMode/TestUITMPText.cs
testMethod: TextProperty_UpdatesTMPText
status: active
lastRun: ""
lastResult: unknown
---

# UT_UI_005

## Requirement
The shared text draw target abstraction updates TMP component text.

## Risk
Prefab text mutation could continue to work for legacy Text but fail for TMP text.

## Steps
1. Create a UITMPText component.
2. Set text through `IUITextDrawTarget.Text`.

## Assertions
The test verifies the underlying TMP `text` value changes.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Unknown until the next Unity Test Runner execution.

## Notes
Created for TextMeshPro adapter coverage.
