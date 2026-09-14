---
testId: UT_UI_006
module: UI
testType: EditMode
target: TMP SDF material binding
testFile: Assets/Tests/EditMode/TestUITMPText.cs
testMethod: BindTmpSdf_CopiesAtlasAndOutlineProperties
status: active
lastRun: ""
lastResult: unknown
---

# UT_UI_006

## Requirement
TMP SDF binding copies the atlas and supported outline properties into the HUD TMP SDF material.

## Risk
Advanced TMP SDF or outline settings could be silently dropped by batch-rendered TMP text.

## Steps
1. Create a HUD TMP SDF material and source TMP material.
2. Bind texture, face color, outline color, and outline width.

## Assertions
The test verifies the target material receives `_MainTex`, `_FaceColor`, `_OutlineColor`, and `_OutlineWidth`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Unknown until the next Unity Test Runner execution.

## Notes
Created for TextMeshPro adapter advanced rendering coverage.
