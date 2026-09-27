## 1. Benchmark setup and baseline

- [x] 1.1 Inspect the existing `VertexHelperUtils` tests and archived baseline, then define the fixed Unity 2022.3.53f1 fixture, warm-up, iteration, timer, and result format for `FillData2` and `FillData3`.
- [x] 1.2 Add or refine a governed EditMode benchmark that measures both full extraction paths and verifies the fixture uses indexed geometry with representative vertex channels and non-zero offsets.
- [x] 1.3 Run the benchmark on the current implementation and record median/tail CPU measurements, managed allocation after warm-up, Unity/environment details, and comparison with the archived modified-UGUI baseline.

## 2. Cost isolation and implementation decision

- [x] 2.1 Profile or measure vertex-channel extraction, scratch mesh/index extraction, and destination writes sufficiently to identify the dominant cost center without changing the production contract.
- [x] 2.2 Decide whether a safe targeted optimization is justified; document the selected CPU gate, stock UGUI constraints, and the expected acceptance outcome in the evidence record.
- [x] 2.3 Evaluate a targeted optimization; no safe rewrite is justified because public UGUI does not expose indexed data without the measured scratch Mesh path and alternatives would change the indexed contract.
- [x] 2.4 Add or update focused EditMode assertions for correctness and zero steady-state managed allocation for any changed path; existing assertions plus the new benchmark cover this path.

## 3. Validation and evidence

- [x] 3.1 Re-run the benchmark before and after any implementation change using the same fixture and environment; report CPU summary statistics and allocation results.
- [x] 3.2 Run `Tools/Test-PackageLayout.ps1` and the relevant package EditMode validation; investigate any failure before closing the change.
- [x] 3.3 Write a change evidence record containing the measurements, comparison, decision, usage budget, residual risk, and follow-up trigger.
- [x] 3.4 Update `Documentation~/roadmap.md` with the M3 node outcome and archive `PERF-01` from `Documentation~/todo.md` only after reviewing the entire active TODO; carry forward any unresolved work under a new node-bound ID.
- [x] 3.5 Run OpenSpec validation and confirm all change tasks and required artifacts are complete before implementation is considered finished.
