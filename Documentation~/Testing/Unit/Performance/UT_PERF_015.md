---
testId: UT_PERF_015
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: Report_PreservesConfiguration_AndSeparatesBurstRefreshFrames
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_015

## Requirement
Traceable reports without diluted burst statistics

## Risk
Invalid workload, stale state or misleading measurements could incorrectly approve an RC.

## Steps
Mutate inspector configuration mid-run, export and read JSON/CSV, check frozen config, sample counts and burst-only summary.

## Assertions
See the named method; timing values are reported, not asserted against machine-dependent limits.

## Expected Result
The test passes; resources/settings are restored and observed workload matches report metadata.

## Latest Run Result
Passed in PlayMode-20260927_153137_078.xml (26/26 suite). Evidence: openspec/changes/validate-hud-rc-performance/evidence/hud-rc-evidence.zip.

## Notes
Traceability: validate-hud-rc-performance / dedicated scene, measurement attribution and reset requirements.
