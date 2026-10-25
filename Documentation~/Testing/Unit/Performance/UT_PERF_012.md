---
testId: UT_PERF_012
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: DedicatedScene_HasBindings_AndProducesRealGeometry
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_012

## Requirement
Committed scene bindings and nonzero production geometry

## Risk
Invalid workload, stale state or misleading measurements could incorrectly approve an RC.

## Steps
Load the committed scene, run a small suite, check exact mesh counts and samples.

## Assertions
See the named method; timing values are reported, not asserted against machine-dependent limits.

## Expected Result
The test passes; resources/settings are restored and observed workload matches report metadata.

## Latest Run Result
Passed in PlayMode-20260927_153137_078.xml (26/26 suite). Evidence: openspec/changes/archive/2026-10-21-validate-hud-rc-performance/evidence/hud-rc-evidence.zip.

## Notes
Traceability: validate-hud-rc-performance / dedicated scene, measurement attribution and reset requirements.
