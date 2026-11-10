---
testId: UT_UI_012
module: UI
testType: EditMode
target: UITMPText.DoGenerate
testFile: Tests/EditMode/TestUITMPTextExtraction.cs
testMethod: DoGenerate_StageTimings_ClearOnNullDisabledAndFailure
status: active
lastRun: "2026-11-10"
lastResult: passed
---

# UT_UI_012

## Requirement
TMP timings clear on null, disabled and failed generation; changed and empty text produce samples.

## Risk
Stale or incomplete diagnostics can be mistaken for valid measured results.

## Steps
Exercise the configured conditions and compare recorded data.

## Assertions
TMP timings clear on null, disabled and failed generation; changed and empty text produce samples.

## Expected Result
Pass with no stale timings or altered workload.

## Latest Run Result
Passed in original-project focused EditMode (9/9) and complete EditMode (205/205), with no failures or skips. Full original-project UIDataRender line coverage was 90.2% (3413/3782), above the existing 70% gate. Local evidence: TestResults/GenerationProfile; this is working-tree validation, not exact-RC acceptance.

## Notes
SourceEvidence: openspec/changes/profile-representative-hud-generation/specs/representative-generation-profiling/spec.md
