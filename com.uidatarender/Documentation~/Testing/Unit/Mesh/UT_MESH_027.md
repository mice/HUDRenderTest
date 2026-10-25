---
testId: UT_MESH_027
module: Mesh
testType: EditMode
target: UIMeshData
testFile: Tests/EditMode/TestUIMeshData.cs
testMethod: QuadDiagnostics_RequireBothSwitches_AndPreserveBothMeshOutputs
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_027

## Requirement
Quad timing requires both diagnostic switches. Each measured Managed/Slim write records one sample; disabled writes clear stale timing while preserving all geometry channels.

## Risk
A previously measured result could be reused after disabling profiling, or diagnostics could alter vertices, UVs, colors or topology. The regression originally exported unmeasured default zeros.

## Steps
1. Run the EditMode test assembly containing `QuadDiagnostics_RequireBothSwitches_AndPreserveBothMeshOutputs`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestUIMeshData.cs` / `QuadDiagnostics_RequireBothSwitches_AndPreserveBothMeshOutputs`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20. Full NUnit XML and source hashes are preserved in openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence at the repository root.

## Notes
Source: OpenSpec `profile-simple-quad-write`. Assertions use sample counts and output equivalence, not positive elapsed-time thresholds; legitimate sub-resolution zero durations remain valid.
