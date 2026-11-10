---
testId: UT_PERF_023
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: AllocationEvents_CalibrateCountBytesRejectSaturationAndMeasureHudScope
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_023

## Requirement
Calibrate allocation events with a known allocation, distinguish byte availability and reject recorder saturation.

## Risk
A marker duration or missing/saturated event stream could be mistaken for allocation bytes or zero GC.

## Steps
Observe a known 8192-byte allocation; test capacity-one saturation; run resource-backed HUD with diagnostics enabled.

## Assertions
Positive-control event count, bytes only if separately calibrated, saturation unavailable, at least one refreshed holder and zero events in every warmed representative HUD frame after TMP scratch reuse.

## Expected Result
Pass without a device timing assertion or an application-wide allocation claim.

## Latest Run Result
2026-11-08: original complete EditMode 204/204 and D3D11 PlayMode 64/64, stock UGUI related EditMode 12/12 and runner PlayMode 11/11 passed with no skips. Positive controls and saturation rejection pass; warmed representative HUD scopes record zero events after TMP scratch reuse. Current evidence is local under TestResults/TmpScratch/. Historical 2026-11-06 baseline failures/results remain under TestResults/Perf04/.

## Notes
SourceEvidence: openspec/changes/archive/2026-11-11-measure-representative-hud-workloads/specs/hud-rc-performance-acceptance/spec.md
