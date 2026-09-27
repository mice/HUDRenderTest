## Context

The package now targets stock Unity 2022.3 UGUI and uses public APIs in `VertexHelperUtils`. The archived migration baseline measured the former modified-UGUI path at about 0.164 us/call for `FillData2` and 0.183 us/call for `FillData3`; the public path measured 4.924–8.285 us/call and 6.038–8.405 us/call, while reporting 0 B managed allocation after warm-up. The current evidence contains two runs but does not isolate the cost of channel extraction from triangle extraction or establish a current decision under the M3 roadmap node.

The work must preserve indexed geometry, packed UV metadata, non-zero vertex/index offsets, stock UGUI compatibility, and the existing package/Sample ownership boundary. Performance evidence must be reproducible in Unity rather than inferred from `dotnet build`.

## Goals / Non-Goals

**Goals:**

- Produce a repeatable benchmark for `FillData2` and `FillData3` with documented environment and warm-up rules.
- Measure steady-state managed allocation and CPU cost, including separate measurements for vertex channels, indices, and full extraction where practical.
- Compare median and tail results with the archived baseline and acceptance references.
- Decide whether a safe implementation optimization is justified, or record an explicit accepted budget and follow-up condition.
- Preserve or add meaningful EditMode coverage for correctness and zero-allocation behavior.

**Non-Goals:**

- Reintroducing the embedded or modified UGUI package.
- Changing the `IUIData`, `VertexHelperUtils`, or mesh index contract for convenience.
- Optimizing unrelated batching, TMP, Burst, shader, or scene performance work.
- Claiming a performance improvement from editor-only or non-representative measurements.

## Decisions

### 1. Benchmark in the Unity EditMode environment

Use the existing package EditMode benchmark/test assembly and Unity 2022.3.53f1. Capture Unity version, platform, scripting backend, test assembly, vertex/index fixture, warm-up count, measured iteration count, and timer source in the evidence file. This keeps the measurement close to the code and avoids a managed-only benchmark that omits Unity native costs.

**Alternative considered:** a standalone .NET benchmark. Rejected because it cannot execute `VertexHelper`, `Mesh`, and Unity allocation behavior under the target editor.

### 2. Keep correctness and allocation gates independent from the CPU decision

The extraction path must continue to preserve vertex channels, triangle order, offsets, and zero additional managed allocation after warm-up. CPU acceptance is a separate decision: either meet the documented reference gates, or explicitly accept the measured regression with a usage budget and a follow-up trigger.

**Alternative considered:** treat zero GC as sufficient acceptance. Rejected because the archived evidence already shows a material CPU regression despite zero GC.

### 3. Isolate likely cost centers before changing code

Benchmark full extraction and, where the API permits without changing production behavior, profile or measure the vertex-channel loop, scratch mesh/index extraction, and destination writes. Use the results to choose a targeted optimization. Keep the existing extension contract and reusable scratch state.

**Alternative considered:** rewrite the extractor speculatively. Rejected because the dominant cost may be native `FillMesh`/index access and a broad rewrite could damage correctness or allocations.

### 4. Require evidence before closing PERF-01

The change closes only when the benchmark artifact, comparison, decision, and remaining risk are recorded. If code changes are made, they also require focused tests and package validation. If no safe optimization meets the gates, the accepted decision must be explicit and `PERF-01` must be archived with the residual risk rather than left indefinitely active.

## Risks / Trade-offs

- **[Risk] Editor timing noise obscures a small change** → Use warm-up, repeated samples, median/tail reporting, and a fixed environment; repeat suspicious results.
- **[Risk] A benchmark optimization changes the renderer contract** → Keep existing channel/index/offset tests as a required gate and inspect representative quad geometry.
- **[Risk] `FillMesh` or `GetTriangles` cost cannot be removed with public APIs** → Document the limitation and define a practical UI regeneration budget instead of restoring forbidden internal access.
- **[Risk] A local machine result is mistaken for a universal device guarantee** → Label the evidence environment and avoid broad claims beyond the measured platform.

## Migration Plan

1. Add or refine governed benchmark coverage and run the current implementation.
2. Record measurements and profile breakdown in change evidence.
3. If justified, implement one targeted optimization, rerun correctness/allocation/performance checks, and compare against the pre-change result.
4. Run package layout and relevant Unity validation.
5. Update the M3 roadmap node and archive/clean `PERF-01` in `todo.md` only after the node exit review.

Rollback is limited to the implementation change; benchmark evidence remains valid and is retained even if an optimization is reverted.

## Open Questions

- Which reference gate should be authoritative for release: the archived 5 us / 30x proposal or a revised budget based on representative HUD scenes?
- Is a device-level frame-time budget needed in addition to the extraction microbenchmark before accepting the path?
