---
testId: UT_PREF_020
module: Prefab
testType: EditMode
target: UIPrefab lifecycle
testFile: Tests/EditMode/TestUIPrefabHolderCoverage.cs
testMethod: BuildMesh_StagesAggregateAndRespectNestedTimingBounds
status: active
lastRun: ""
lastResult: unknown
---

# UT_PREF_020

## Requirement
BuildMesh aggregates each UIImage stage once and respects nested timing boundaries on Managed and Slim holders, while preserving timing for non-UIImage generation and clearing stale results.

## Risk
Dropped, duplicated or stale stage totals could misidentify a performance hotspot. Adding Transform to Simple/fallback or sprite-cache time to drawing dimensions would double count nested work.

## Steps
1. Run the EditMode test assembly containing `BuildMesh_StagesAggregateAndRespectNestedTimingBounds`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestUIPrefabHolderCoverage.cs` / `BuildMesh_StagesAggregateAndRespectNestedTimingBounds`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20; full regression 142/142 EditMode and 27/27 PlayMode. Evidence: openspec/changes/archive/2026-10-20-profile-buildmesh-stages/evidence/closure-validation.json.

## Notes
Source: `profile-buildmesh-stages` task 3.1. Mixed sprite-backed direct, Filled fallback and non-UIImage targets exercise cold/warm cache, a null-Sprite transition, disable and empty refresh. Assertions use exact aggregation and monotonic containment with floating-point rounding tolerance, not speed thresholds or positive-duration assumptions.
