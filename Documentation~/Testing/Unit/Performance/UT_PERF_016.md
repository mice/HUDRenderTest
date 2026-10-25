---
testId: UT_PERF_016
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: QuadWriteReport_ExportsAvailability_AndCountsThroughBothPaths
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_016

## Requirement
HUD RC v2 reports preserve captured configuration and serialize quad sample counts and validity consistently in JSON and CSV for Managed/Slim with profiling off/on.

## Risk
Disabled or idle frames could masquerade as zero-cost measurements; an Inspector edit could change the recorded measurement settings during a run.

## Steps
1. Run the PlayMode test assembly containing `QuadWriteReport_ExportsAvailability_AndCountsThroughBothPaths`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs` / `QuadWriteReport_ExportsAvailability_AndCountsThroughBothPaths`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20. Full NUnit XML and source hashes are preserved in openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence at the repository root.

## Notes
Source: OpenSpec `profile-simple-quad-write`. Four small runs cover both mesh paths and timing modes. Every stage is checked after JSON round-trip and CSV export; unavailable values must be -1/unavailable, including idle frames.
