---
testId: UT_PERF_021
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: RepositoryFixture_PreservesTextureSlotsTextAndConvergedBucketGeometry
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_021

## Requirement
Representative repository sprites and primary-atlas TMP must preserve requests and final output across bucket strategies.

## Risk
Texture remapping, missing text or delayed text state could invalidate performance evidence.

## Steps
Run 129 Slim holders with all three strategies; inspect UV flags, material bindings, geometry, reports and cleanup.

## Assertions
Three distinct image slots, 24 text vertices per holder, identical converged hashes and preserved borrowed resources.

## Expected Result
Pass without a device timing assertion or an application-wide allocation claim.

## Latest Run Result
2026-11-06: runner suite 11/11, complete EditMode 196/196 and D3D11 PlayMode 64/64 passed; no failures or skips. First failures and final evidence retained locally under TestResults/Perf04/.

## Notes
SourceEvidence: openspec/changes/measure-representative-hud-workloads/specs/hud-rc-performance-acceptance/spec.md
