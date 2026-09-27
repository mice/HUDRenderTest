## Why

The stock UGUI migration preserved correctness and zero steady-state managed allocation, but the recorded public `VertexHelper` extraction path is materially slower than the former internal-field path. `PERF-01` needs a repeatable Unity 2022.3 measurement and an explicit decision so the repository either improves the extraction path or accepts and documents the measured cost with a clear usage budget.

## What Changes

- Re-run the `FillData2` and `FillData3` benchmark with a fixed Unity/editor configuration, warmed buffers, representative indexed geometry, and repeated samples.
- Separate vertex-channel extraction, triangle-index extraction, and destination-copy costs so the dominant source of CPU time is identified.
- Preserve the zero additional managed allocation requirement after warm-up.
- Compare current results with the archived modified-UGUI baseline and the documented 5 us / 30x reference gates.
- If a safe optimization is supported by measurements, implement it behind the existing `VertexHelperUtils` contract and add focused regression coverage.
- If the gates cannot be met without violating stock UGUI compatibility or correctness, record an explicit acceptance decision, supported usage budget, and follow-up condition.
- Update the M3 roadmap node and archive `PERF-01` from the active TODO only after the evidence and decision are recorded.

## Capabilities

### New Capabilities

- `public-ugui-performance`: Defines the measurement, allocation, comparison, and decision contract for the package-owned public UGUI extraction path.

### Modified Capabilities

- None. This change adds performance evidence and an acceptance contract; it does not intentionally change the renderer's public behavior.

## Impact

- `com.uidatarender/Runtime/UGUI.Extends/VertexHelperUtils.cs` and its EditMode tests may change if profiling identifies a safe optimization.
- Package performance tests and governed records may gain or update benchmark coverage.
- A new evidence document will record environment, samples, measurements, allocation results, comparison, decision, and remaining risk.
- `Documentation~/roadmap.md` and `Documentation~/todo.md` will be updated at node exit according to the repository document lifecycle.
