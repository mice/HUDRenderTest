---
testId: UT_PERF_020
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: BucketStrategies_PreserveRequestsAndConvergedOutput_AndReportSecondFrameWork
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_020

## Requirement
Compare three strategies using identical requests, include the second refresh frame, report coalesced completions, and reach identical final geometry after bounded drain.

## Risk
A faster result could omit second-frame work or execute a different workload.

## Steps
Run all three strategies with 129 Slim holders through static, partial and burst modes; export and reload reports.

## Assertions
Request holder/version/frame sequences match; final geometry hashes match; every request completes within one extra frame; rotating burst summaries include both refresh frames; drain and command lifetime are explicit.

## Expected Result
Correct report and output equivalence without a timing threshold in automated tests.

## Latest Run Result
2026-10-25: passed; full regression 161/161 EditMode and 31/31 graphical PlayMode. Evidence: openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/local-validation.json.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/specs/holder-bucket-scheduling/spec.md
