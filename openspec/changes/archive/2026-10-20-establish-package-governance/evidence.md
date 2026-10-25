# 治理归档记录 — 2026-10-20

- 用户在审查方案后回复“继续”，确认同步并归档本 change 与 `optimize-uiimage-null-texture`。
- 本 change 的范围是治理规则与文档生命周期。主规格为 `openspec/specs/package-release-governance/spec.md`；归档不代表候选或正式发布已验收。
- 完整复核 `Documentation~/todo.md`，现有 12 项全部保持活动：PERF-04、GOV-002、GOV-004、VERIFY-02、VERIFY-01、PERF-02、PERF-03、JOB-01、TMP-01、TMP-02、TMP-03、MAINT-01。原 GOV-001/GOV-003 的历史完成记录保留在 M5，没有恢复旧任务或复用 ID。
- 新发现的 UGUI 分发契约缺口由 VERIFY-02 承接；诊断和测试缺口见 `Documentation~/change-review-2026-10-20.md`。归档没有修改这些事项的验收条件。
- 包版本仍为 `0.1.2` / Unreleased。本次审查没有发现本地 tag，远端 tags 查询成功返回空；历史 0.1.0 映射由 GOV-002 继续跟踪。没有创建 tag 或发布。
- 同步主规格后完成任务 4.3、迁移完整 change（含 `.openspec.yaml`），并更新 roadmap、TODO、changelog 引用。M5 性能、覆盖率、clean validation、精确候选 Jenkins run 和正式晋升要求继续有效。
- 检查范围为 OpenSpec strict、包布局、测试治理、归档文件和引用、原始 JSON 哈希以及 diff。历史 Unity 测试/覆盖率结果不作为当前 RC 结果。
- 归档后结果：OpenSpec strict 12/12（6 个活动 change + 6 个主规格）、包布局通过、测试治理 163 tests / 163 records、51 个相关本地链接可解析、两个归档任务清单均完成、旧活动路径引用清零、`git diff --check` 通过。TODO 仍为 12 项；未运行新的 Unity/真机测试。
