---
testId: UT_PERF_013
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: Modes_ReplayWorkload_ThroughBothMeshPaths
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_013

## Requirement
Deterministic mode workload on UIMeshData and UIMeshDataX

## Risk
Invalid workload, stale state or misleading measurements could incorrectly approve an RC.

## Steps
Run static, partial and burst windows twice across mesh paths; compare update counts, geometry and selection checksums.

## Assertions
See the named method; timing values are reported, not asserted against machine-dependent limits.

## Expected Result
The test passes; resources/settings are restored and observed workload matches report metadata.

## Latest Run Result
Passed in PlayMode-20260927_153137_078.xml (26/26 suite). Evidence: openspec/changes/validate-hud-rc-performance/evidence/hud-rc-evidence.zip.

## Notes
Traceability: validate-hud-rc-performance / dedicated scene, measurement attribution and reset requirements.
