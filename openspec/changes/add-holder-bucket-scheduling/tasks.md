## 1. Explore and baseline

- [x] 1.1 Review current generation/merge/update entry points and preserved 256-holder evidence; separate logical buckets from texture batches.
- [x] 1.2 Record the user's acceptance of two-frame rotation and one extra frame of visible latency for the second bucket.
- [x] 1.3 Measure 128-holder synchronous whole updates on Mi 10 with the existing Slim/null-Sprite workload, two repeats, preserved APK/source/report hashes and per-stage median/p95.
- [x] 1.4 Use the 128-holder result to finalize bucket capacity, update-state API, latest-value semantics, structural invalidation and ordering design before leaving explore.

## 2. Implementation after exploration

- [ ] 2.1 Add an explicit opt-in scheduler with ordered membership epochs, value-state snapshots, latest-wins versions, Tick cutoff and fair dirty rotation; retain existing synchronous APIs.
- [ ] 2.2 Implement reusable front/back renderers with atomic publication, fault recovery and structural/resource barriers; add opt-in contiguous texture batching and ordered Built-in camera command submission.
- [ ] 2.3 Test membership boundaries, stale handles, shared-template snapshots, cutoff/latency/fairness, failure atomicity, resource lifetime and warmed allocation; verify transparent overlap/order on an actual graphics device.

## 3. Matched device evaluation

- [ ] 3.1 Extend Sample reports with request/completion versions and frames, first-pending/latest-value ages, bucket/mesh counts, state preparation through publication CPU, draw submission CPU, full two-frame windows, barriers and bounded drain.
- [ ] 3.2 Compare 256 single-bucket synchronous, 2×128 same-frame, and 2×128 rotating using the same request sequence/query reuse/diagnostics/shader/submission path; run six balanced device captures with at least 100 burst requests each and verify converged output.
- [ ] 3.3 Record measured benefits and remaining limits under PERF-04/M5 without relaxing the budget or treating unavailable counters as passed.

Current mode: exploration complete, implementation pending (4/10). No bucket scheduling implementation has started.


2026-10-21 prioritization: the user set the 128-holder whole-update target to 1.5 ms and requested optimization first. Bucket implementation remains planned/deferred; acceptance of its one-frame delay remains valid. Baseline evidence: [128-holder results](evidence/holders128-results.md).

优先级更新（2026-10-21）：先推进 [128-holder 生成优化](../archive/2026-10-25-optimize-128-holder-generation/proposal.md)，同步整体更新 p95 ≤ 1.5 ms 为工作目标。本分桶 change 保留为后续计划，未实施调度功能。

2026-10-25：128-holder 查询复用的 p95 已达到 1.2971 / 1.2310 ms，优化 change 已归档。1.4 状态/API/失效和排序设计已完成，见 [实施设计](design.md)。下一步为 2.1；双桶普通刷新帧 update CPU p95 ≤ 1.5 ms 仍需实测，原 RC 预算和设备计数器要求不变。
