# PERF-01 Public UGUI Extraction Benchmark

## Environment and fixture

- Unity: 2022.3.53f1
- Test platform: EditMode, repository `Tests` assembly
- Fixture: indexed four-vertex, six-index detailed quad with non-default positions, colors, and UV0 values
- Warm-up: 32 iterations
- Measured samples: 21 samples × 500 iterations per sample
- Timer: `System.Diagnostics.Stopwatch`, frequency 10,000,000 ticks/second
- Allocation measurement: `GC.GetAllocatedBytesForCurrentThread` after GC collection and warm-up
- Test: `UT_MESH_018` / `PublicExtraction_Benchmark_ReportsMedianAndTailCosts`
- Result artifact: `HUDRenderTestSample/Logs/EditModeRepoTests.xml`

## Current public API results

| Operation | Min us/call | Median us/call | P95 us/call | Max us/call | Warm allocation |
|---|---:|---:|---:|---:|---:|
| `FillData2` | 4.541 | 5.380 | 7.759 | 8.711 | 0 B |
| `FillData3` | 4.440 | 6.327 | 9.182 | 11.775 | 0 B |
| Vertex channel reads | 0.126 | 0.126 | 0.138 | 0.414 | not measured separately |
| Triangle index extraction | 4.270 | 4.944 | 5.845 | 6.017 | not measured separately |

The benchmark passed with 119/119 EditMode tests. The existing correctness tests continue to cover indexed topology, UV metadata, and non-zero vertex/index offsets.

## Comparison with archived baseline

The archived migration baseline recorded the former modified-UGUI path at approximately 0.164 us/call for `FillData2` and 0.183 us/call for `FillData3`. The stock-UGUI public path therefore remains materially slower, while preserving the required zero managed allocation after warm-up.

The previous reference gates were 5 us/call and 30× relative cost. The current median is near or above 5 us for both methods and the p95 is above it; the relative cost is above 30× for the median and tail measurements.

## Cost isolation

Vertex channel population is a small part of the measured operation. Triangle extraction through the reusable scratch `Mesh`, `FillMesh`, and `GetTriangles` path accounts for most of the full extraction time. The public UGUI contract does not expose the internal index list, and `GetUIVertexStream` would expand indexed geometry into a triangle stream and violate the existing indexed mesh data contract.

## Decision

Do not introduce a speculative extraction rewrite in PERF-01. Accept the public API path for stock UGUI distribution with the following operational budget:

- zero managed allocation after warm-up;
- correctness for vertex channels, packed UV metadata, indexed topology, and non-zero offsets;
- target median under 10 us/call and p95 under 15 us/call for the four-vertex/six-index fixture on the documented Unity 2022.3.53f1 editor benchmark;
- investigate again if a supported Unity public API exposes indexed data without triangle expansion, or if representative HUD scene regeneration exceeds the 1 ms p95 frame-time budget.

This accepts a known CPU regression in exchange for stock UGUI compatibility and independent package distribution. The budget is a measurement guardrail, not a universal device guarantee.

## Validation

- `Tools/Test-PackageLayout.ps1`: passed
- `Tools/UnityTestRunner/Sync-UnitTestGovernance.ps1 -Scope All`: passed, 141 tests and 141 records
- `Tools/Invoke-LocalValidation.ps1 -SkipPlayMode`: passed, 119/119 EditMode tests
