---
testId: UT_PERF_018
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: StageDiagnosticsOff_PreservesWorkload_ReportsUnavailable_AndRestoresSettings
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_018

## Requirement
Allow matched measurement without per-holder/Image timing overhead while retaining outer whole-update timers.

## Risk
Disabled stage counters could be reported as zero, alter the workload, or leak global diagnostic settings.

## Steps
Run the same configuration with diagnostics on/off; compare all frame workloads and geometry counts; export CSV; finish and cancel runs.

## Assertions
Selection checksums, updated counts and geometry counts match. Disabled stages have availability=false and sentinel -1; CSV says unavailable. Whole-update timers remain valid. Configuration is snapshotted and global switches are restored.

## Expected Result
All assertions pass without wall-clock performance thresholds.

## Latest Run Result
2026-10-25: passed; full regression 150/150 EditMode and 29/29 PlayMode.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-25-optimize-128-holder-generation/design.md
