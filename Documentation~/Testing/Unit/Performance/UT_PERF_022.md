---
testId: UT_PERF_022
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: RepositoryFixture_RejectsMissingRepeatedResourcesAndGeometryOverflow
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_022

## Requirement
Reject incomplete, duplicate-texture or oversized repository fixture configuration before sampling.

## Risk
A fallback fixture or truncated geometry could be reported as representative evidence.

## Steps
Attempt missing resources, repeated texture references and a 2000-holder mixed fixture.

## Assertions
Run fails before samples; no synthetic substitution and no vertex limit overflow.

## Expected Result
Pass without a device timing assertion or an application-wide allocation claim.

## Latest Run Result
2026-11-06: runner suite 11/11, complete EditMode 196/196 and D3D11 PlayMode 64/64 passed; no failures or skips. First failures and final evidence retained locally under TestResults/Perf04/.

## Notes
SourceEvidence: openspec/changes/measure-representative-hud-workloads/specs/hud-rc-performance-acceptance/spec.md
