---
testId: UT_MESH_028
module: Mesh
testType: EditMode
target: UIMeshData
testFile: Tests/EditMode/TestUIMeshData.cs
testMethod: QuadDiagnostics_WarmedWrites_AllocateNoManagedMemory
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_028

## Requirement
Repeated warmed Managed/Slim quad writes must introduce no managed allocation with diagnostics disabled, aggregate-only, or full quad timing enabled.

## Risk
Diagnostic sampling could introduce per-quad garbage, while a broken allocation counter could falsely report zero.

## Steps
1. Run the EditMode test assembly containing `QuadDiagnostics_WarmedWrites_AllocateNoManagedMemory`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestUIMeshData.cs` / `QuadDiagnostics_WarmedWrites_AllocateNoManagedMemory`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20. Full NUnit XML and source hashes are preserved in openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence at the repository root.

## Notes
Source: OpenSpec `profile-simple-quad-write`. GC.Alloc recording is positively calibrated with a 4096-byte allocation before each measured loop; no wall-clock performance threshold is asserted.
