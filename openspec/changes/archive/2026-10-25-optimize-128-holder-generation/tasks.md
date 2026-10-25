## 1. Baseline and hotspot experiments

- [x] 1.1 Preserve two 128-holder Mi 10 baseline runs, per-stage timings, source/APK/report identity and allocation availability in the linked bucket evidence.
- [x] 1.2 Inspect current Burst merge and managed generation boundaries; record feasibility, priority and the 1.5 ms whole-update target.
- [x] 1.3 Measure repeated Canvas/RectTransform/matrix queries and preparation costs with reversible probes; verify diagnostic overhead and select the first optimization from evidence.

## 2. Generation optimization

- [x] 2.1 Implement the selected query/preparation optimization with explicit invalidation and compatibility behavior.
- [x] 2.2 Evaluate whether a pure-data Burst/Job prototype is needed for the 1.5 ms target using matched device evidence; record the decision and carry optional experiments to JOB-01.
- [x] 2.3 Retain the measured synchronous query-reuse strategy, preserve fallback and document the stable-hierarchy contract; explicitly record that Burst/SIMD and ARM64 vectorization inspection are deferred, not implemented.

## 3. Correctness and acceptance

- [x] 3.1 Add governed output/invalidation/fallback tests and warm-allocation checks; run EditMode, PlayMode, package layout and governance validation.
- [x] 3.2 Run matched Mi 10 ABBA comparisons with at least 100 full refreshes per run, confirm actual path/workload/output, and retain raw evidence and hashes.
- [x] 3.3 Evaluate every candidate run against whole-update p95 ≤ 1.5 ms, report remaining bottlenecks if unmet, and update PERF-04 and the deferred bucket plan without changing RC acceptance status.

2026-10-25：查询复用候选的本地回归 150/150 EditMode、29/29 PlayMode 和治理 179/179 通过；3.1 当前覆盖该候选，若后续引入 Burst 内核需补充相应测试并重新验证。诊断开关证据见 [stage overhead](evidence/stages-results.md)。

2026-10-25 计划调整：两轮候选各 100 次全量刷新 p95 为 1.2971 / 1.2310 ms，当前用户优先目标已由查询复用满足。原 2.2 的 Burst 原型和原 2.3 的 ARM64 SIMD 检查从本 change 的必做项调整为 JOB-01 后续候选；上述勾选表示完成技术选择和移交，不表示实现了 Burst/Job 或验证了 SIMD。预算仍为 p95 ≤ 1.5 ms，未放宽；单次峰值 1.5241 ms 已明确记录。完整证据见 [query results](evidence/query-results.md)。分桶尚未实施，RC 总体验收仍未通过。
