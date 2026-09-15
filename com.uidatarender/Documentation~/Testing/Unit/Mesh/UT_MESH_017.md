---
testId: UT_MESH_017
module: Mesh
testType: EditMode
target: VertexHelperUtils
testFile: Tests/EditMode/TestVertexHelperUtils.cs
testMethod: FillData2And3_AfterWarmup_DoNotAllocateManagedMemory
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_017

## Requirement
Capacity-stable VertexHelper extraction performs no additional managed allocations.

## Risk
Replacing direct internal-list access with public APIs could introduce per-frame garbage collection pressure.

## Steps
1. Allocate destination buffers and warm both extraction paths.
2. Execute 10,000 `FillData2` calls and 10,000 `FillData3` calls while measuring thread allocations and elapsed ticks.

## Assertions
Both measured loops allocate zero managed bytes after warm-up; timing values are emitted for baseline comparison.

## Expected Result
The test passes and records comparable CPU measurements for the old and portable implementations.

## Latest Run Result
Unknown until the next Unity Test Runner execution.

## Notes
Added for the `complete-upm-migration` allocation and performance gate.
