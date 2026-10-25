# 分桶交付后的路线图复核 — 2026-10-25

- 范围：`D:/UnityPackages/HUDRenderPackage`；交付包 `com.uidatarender/`，包名 `com.uidatarender`，符合 Unity UPM 项目范围。同仓库 Sample 仅用于验证，没有审查其他仓库。
- 规范入口：`Documentation~/roadmap.md`、`Documentation~/todo.md`。未发现同名重复入口或根目录 roadmap/TODO；包 changelog 位于 `com.uidatarender/CHANGELOG.md`。
- 当前事实：分桶任务完成 10/10，功能和局部 1.5 ms p95 目标有 [七轮设备证据](bucket-results.md)及 [本地测试](implementation-validation.md)支持。已更新“分桶尚待实现”的当前入口；带历史日期的前期记录保留其当时语境。
- TODO 全量复核：12 项开放、0 项完成留在活动队列。PERF-04/M5 是本次子项所属节点，但原 1 ms、GC 和代表性矩阵未闭环，继续开放。GOV-002 历史发布映射、GOV-004 精确候选 CI 仍 blocked/unverified；VERIFY-02、MAINT-01 继续 M5；VERIFY-01、PERF-02、PERF-03、JOB-01 继续 M3；TMP-01..03 继续 M4。没有关闭、删除或复用任何 TODO ID。
- 版本：包 metadata 为 `0.1.2`，changelog 为 `0.1.2 — Unreleased`，稳定目标 `v0.1.2`。本地没有 release/RC tag，未核实任何已发布稳定 tag；历史 0.1.0 映射继续由 GOV-002 跟踪。下一候选命名应为未使用的 `0.1.2-rc.1`，本次没有准备或发布候选，不把文档版本当作发布证据。
- 剩余发布条件：独立依赖/clean checkout、当前覆盖率、精确候选 Jenkins、原 RC 性能/设备分配证据仍待完成。用户接受后一桶多一帧延迟；这不是对原 RC 预算的例外确认。没有稳定晋级确认或发布动作。
- 修改：根 roadmap/TODO 的当前状态、M5 子项交付记录、包 Unreleased feature 条目、分桶 API 文档及测试记录。验证：161/161 EditMode、31/31 实际图形 PlayMode、192/192 测试治理、包布局及 OpenSpec 严格校验；本轮没有新增 coverage、clean checkout 或 Jenkins 结果。

完整的历史来源与后续触发条件仍保留在活动 TODO 和 roadmap 节点中。后续优先处理 PERF-04 代表性负载/分配证据、VERIFY-02 分发契约及原 1 ms 参考；需要更紧预算时再按 JOB-01 比较 Burst/SIMD，而不把当前轮转结果外推到其他资源与渲染管线。
