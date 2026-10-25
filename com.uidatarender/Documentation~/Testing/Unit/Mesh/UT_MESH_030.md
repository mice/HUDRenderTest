---
testId: UT_MESH_030
module: Mesh
testType: EditMode
target: UIMeshData
testFile: Tests/EditMode/TestUIMeshDataX.cs
testMethod: SlimFill_WarmedPreallocatedOutputs_AllocateNoManagedMemory
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_030

## Requirement
Repeated warmed Slim FillToDrawData and FillWithMatrix calls allocate no managed memory when output lists have sufficient capacity.

## Risk
Per-vertex Color32 boxing can dominate merge fill. A missing allocation counter or List growth could respectively hide or misattribute allocations.

## Steps
1. Run the EditMode test assembly containing `SlimFill_WarmedPreallocatedOutputs_AllocateNoManagedMemory`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestUIMeshDataX.cs` / `SlimFill_WarmedPreallocatedOutputs_AllocateNoManagedMemory`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20. Full regression: 144/144 EditMode and 27/27 PlayMode. Evidence: openspec/changes/archive/2026-10-21-optimize-slim-fill-color-check/evidence/.

## Notes
Source: optimize-slim-fill-color-check. Calibrate current-thread GC.Alloc with a known allocation; warm 64 calls and measure 256 calls for each method with preallocated lists. Inputs include default and non-default colors. Old implementation produced 2,048 allocation events per method, preserved in the baseline NUnit XML. Cold initialization, List capacity growth, native memory and whole-application allocation are outside this test.
