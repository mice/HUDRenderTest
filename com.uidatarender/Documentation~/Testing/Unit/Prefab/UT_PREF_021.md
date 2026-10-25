---
testId: UT_PREF_021
module: Prefab
testType: EditMode
target: UIPrefab lifecycle
testFile: Tests/EditMode/TestUIPrefabHolderCoverage.cs
testMethod: BuildMesh_DisabledDiagnostics_PreserveOutputAndAllocateNothingWhenWarm
status: active
lastRun: ""
lastResult: unknown
---

# UT_PREF_021

## Requirement
Warmed BuildMesh calls with diagnostics disabled preserve all output channels and introduce no managed allocation on Managed and Slim, including direct and Filled fallback generation.

## Risk
Diagnostics could allocate on the normal rendering path, leak previous measurements, or alter geometry. An unavailable GC counter could otherwise produce a false zero-allocation pass.

## Steps
1. Run the EditMode test assembly containing `BuildMesh_DisabledDiagnostics_PreserveOutputAndAllocateNothingWhenWarm`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestUIPrefabHolderCoverage.cs` / `BuildMesh_DisabledDiagnostics_PreserveOutputAndAllocateNothingWhenWarm`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20; full regression 142/142 EditMode and 27/27 PlayMode. Evidence: openspec/changes/archive/2026-10-20-profile-buildmesh-stages/evidence/closure-validation.json.

## Notes
Source: `profile-buildmesh-stages` task 3.1. Both sprite-backed and null-Sprite direct modes run beside fallback and non-UIImage targets. Warm 64 calls, calibrate the current-thread GC.Alloc recorder with a known allocation, then measure 256 calls without assertions inside the region. Compare positions, UVs/texture slots, colors and indices across diagnostic modes. Cold construction, native allocations and whole-application GC are outside this contract.
