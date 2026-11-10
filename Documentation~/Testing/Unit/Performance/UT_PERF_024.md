---
testId: UT_PERF_024
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: RotatingGenerationProfile_PreservesOutputAndClearsIdleSamples
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_024

## Requirement
Rotating profile on/off preserves workload and outputs, counts completed holders, clears idle samples and records actual dimensions.

## Risk
Stale or incomplete diagnostics can be mistaken for valid measured results.

## Steps
Exercise the configured conditions and compare recorded data.

## Assertions
Rotating profile on/off preserves workload and outputs, counts completed holders, clears idle samples and records actual dimensions.

## Expected Result
Pass with no stale timings or altered workload.

## Latest Run Result
Passed in original-project D3D11 focused PlayMode (12/12) and complete PlayMode (65/65), with no failures or skips. Probe-on/off requests, completions, final geometry, stage counts and per-frame rendering dimensions remained equivalent in the tested fixture. Local evidence: TestResults/GenerationProfile; target-device timing and separate Profiler GC acceptance are additional checks.

## Notes
SourceEvidence: openspec/changes/profile-representative-hud-generation/specs/representative-generation-profiling/spec.md
