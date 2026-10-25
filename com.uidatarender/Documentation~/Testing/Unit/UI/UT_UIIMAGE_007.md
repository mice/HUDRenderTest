---
testId: UT_UIIMAGE_007
module: UI
testType: EditMode
target: UI draw target
testFile: Tests/EditMode/TestUIImage.cs
testMethod: NullSpriteHolder_DiagnosticsAggregate_AndClearAfterFallbackOrDisable
status: active
lastRun: ""
lastResult: unknown
---

# UT_UIIMAGE_007

## Requirement
Null-Sprite Simple Images preserve UGUI-equivalent parent-relative geometry for Managed and Slim holders. Counts aggregate across draws and stale timings are cleared on fallback or diagnostic disable.

## Risk
Holder aggregation could lose write samples or expose the previous direct-path result after fallback. Slim offsets or parent transforms could diverge from the UGUI output.

## Steps
1. Run the EditMode test assembly containing `NullSpriteHolder_DiagnosticsAggregate_AndClearAfterFallbackOrDisable`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestUIImage.cs` / `NullSpriteHolder_DiagnosticsAggregate_AndClearAfterFallbackOrDisable`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20. Full NUnit XML and source hashes are preserved in openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence at the repository root.

## Notes
Sources: `profile-simple-quad-write` aggregation and `optimize-uiimage-null-sprite` task 2.1. The fixture compares all output channels using a translated and rotated parent and two draws on both mesh implementations.
