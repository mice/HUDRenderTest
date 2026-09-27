---
testId: UT_MESH_018
module: Mesh
testType: EditMode
target: VertexHelperUtils
testFile: Tests/EditMode/TestVertexHelperUtils.cs
testMethod: PublicExtraction_Benchmark_ReportsMedianAndTailCosts
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_018

## Requirement
The public UGUI extraction path must provide repeatable CPU and allocation evidence for `PERF-01`.

## Risk
The public API path may spend CPU in vertex channel population or scratch Mesh index extraction even when it allocates no managed memory.

## Steps
1. Create the fixed four-vertex, six-index detailed quad fixture.
2. Warm both extraction paths and their isolated vertex/index operations for 32 iterations.
3. Record 21 samples of 500 iterations for `FillData2`, `FillData3`, vertex channel reads, and index reads.
4. Report min, median, p95, max, Unity version, timer frequency, and warm-up configuration.

## Assertions
`FillData2` and `FillData3` allocate zero managed allocation events after warm-up. The output includes all benchmark series needed for comparison with the archived modified-UGUI baseline.

## Expected Result
The test passes and emits reproducible PERF-01 evidence without changing the indexed mesh or UV metadata contract.

## Latest Run Result
Passed: 119/119 EditMode (2026-09-27); GC.Alloc positive control (4096-byte array) observed, zero target allocation events, recorder saturation checked. CPU median/p95: FillData2 5.025/6.423 us; FillData3 6.083/7.529 us. Evidence: openspec/changes/validate-hud-rc-performance/evidence/hud-rc-evidence.zip.

### Previous record
Passed in `Tools/Invoke-LocalValidation.ps1 -SkipPlayMode`: 119/119 EditMode tests. Unity 2022.3.53f1 reported `FillData2` median 5.380 us/call (p95 7.759), `FillData3` median 6.327 us/call (p95 9.182), and 0 B warm-up allocation for both paths.

## Notes
Added for the `reassess-public-ugui-extraction-performance` OpenSpec change.
