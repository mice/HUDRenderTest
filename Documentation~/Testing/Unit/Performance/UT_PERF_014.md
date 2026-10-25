---
testId: UT_PERF_014
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: ResetDuringSampling_RestoresSettings_AndCleansObjects
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_014

## Requirement
Safe cancellation and repeatable runtime lifecycle

## Risk
Invalid workload, stale state or misleading measurements could incorrectly approve an RC.

## Steps
Set nondefault global settings, cancel an active run, then repeat to completion and verify cleanup and restoration.

## Assertions
See the named method; timing values are reported, not asserted against machine-dependent limits.

## Expected Result
The test passes; resources/settings are restored and observed workload matches report metadata.

## Latest Run Result
Passed in PlayMode-20260927_153137_078.xml (26/26 suite). Evidence: openspec/changes/archive/2026-10-21-validate-hud-rc-performance/evidence/hud-rc-evidence.zip.

## Notes
Traceability: validate-hud-rc-performance / dedicated scene, measurement attribution and reset requirements.
