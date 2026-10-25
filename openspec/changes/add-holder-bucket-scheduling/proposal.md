## Why

256-holder 的整体目标尚未通过。128-holder 已先完成同步查询复用优化，Mi 10 全量更新 p95 为 1.2971 / 1.2310 ms；这不能外推为双桶达标。历史约 7 ms 的 256-holder 数据包含分段诊断开销，新的 256-holder 对照必须重测。用户已确认按 128 个一桶分到两帧更新，后一桶额外延迟一帧。

## What Changes

- 探索稳定的逻辑 holder 分桶：目标负载为 256 holders、每桶 128；桶内继续遵守纹理槽分批规则，因此逻辑桶不保证永远只对应一个 mesh。
- 探索每帧处理一个脏桶，覆盖生成、合并和上传，而不只是把现有全量合并拆成两个 mesh。
- 按值保存更新快照，采用成员 epoch、最新值合并、帧截止点和公平轮转；首版普通更新覆盖宽度、颜色和位置，资源/拓扑变更走显式屏障。
- 复用每桶 front/back 网格，成功后原子发布；未刷新桶保持上一版有效输出，故障不冒充完成。
- 以连续纹理分批和单一 Built-in HUD camera 的有序命令提交保持透明顺序；验证额外内存和 Draw Calls。
- 比较单桶同帧、双桶同帧、双桶跨帧三种策略，分别记录每帧峰值、两帧总工作量、更新年龄、Draw Calls 和分配可用性。
- 承接旧总体验收中“256 个目标未满足”的优化调查；设备计数器、预算决策和精确候选验证仍由 PERF-04/M5 保持开放。

用户要求的 **OpenSpec explore 已收敛**，进度 4/10。本 change 尚未实现分桶；下一步实施显式状态队列与调度器。双桶普通刷新帧 update CPU p95 ≤ 1.5 ms 是工作目标，不代表原 RC 1 ms 参考通过；接受一帧延迟不等于接受性能预算例外。

## Capabilities

### New Capabilities

- `holder-bucket-scheduling`: 有界 holder 分桶、跨帧脏桶调度、可见状态和公平性能对照。

### Modified Capabilities

无。保留 `holder-batch-plan-cache`、`hud-rc-performance-acceptance` 的原有要求；现有全量 Rebuild 调用不应被悄悄改为延迟执行。

## Impact

接入点是 `HolderBatchRenderer` 外层的显式状态调度器、`MergeBatcher` 的可选连续分批模式、Sample HUD camera 提交及配置/报告。初版范围 1..256 holders、固定容量 128；不改变现有同步 API 和默认 first-fit 分批。接口、原子发布、资源寿命与排序方案见 [设计](design.md)。本轮只完成文档，不引入 Job/GPU 并行或新的依赖。

旧结果与承接边界见 [已归档总体验收交接](../archive/2026-10-21-validate-hud-rc-performance/evidence/closure-handoff-2026-10-20.md)。

优先级更新（2026-10-21）：先推进 [128-holder 生成优化](../archive/2026-10-25-optimize-128-holder-generation/proposal.md)，同步整体更新 p95 ≤ 1.5 ms 为工作目标。本分桶 change 保留为后续计划，未实施调度功能。

2026-10-25：128-holder 优化已归档，分桶设计完成。下一步推进任务 2.1；分桶实现与双桶预算仍待验证。
