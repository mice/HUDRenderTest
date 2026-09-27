---
testId: UT_MESH_017
module: Mesh
testType: EditMode
target: VertexHelperUtils
testFile: Tests/EditMode/TestVertexHelperUtils.cs
testMethod: FillData2And3_AfterWarmup_DoNotAllocateManagedMemory
status: active
lastRun: "2026-09-27"
lastResult: passed
---

# UT_MESH_017

## Requirement
Capacity-stable VertexHelper extraction performs no additional managed allocations.

## Risk
Replacing direct internal-list access with public APIs could introduce per-frame garbage collection pressure.

## Steps
1. Allocate destination buffers and warm both extraction paths.
2. Execute 10,000 `FillData2` calls and 10,000 `FillData3` calls while measuring current-thread GC.Alloc events and (separately) elapsed ticks.

## Assertions
Both measured loops allocate zero managed allocation events after warm-up; timing values are emitted for baseline comparison.

## Expected Result
The test passes and records comparable CPU measurements for the old and portable implementations.

## Latest Run Result
Passed: 119/119 EditMode (2026-09-27); GC.Alloc positive control (4096-byte array) observed, zero target allocation events, recorder saturation checked. CPU median/p95: FillData2 5.025/6.423 us; FillData3 6.083/7.529 us. Evidence: openspec/changes/validate-hud-rc-performance/evidence/hud-rc-evidence.zip.

### Previous record
Unknown until the next Unity Test Runner execution.

## Notes
Added for the `complete-upm-migration` allocation and performance gate.
