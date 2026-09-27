## ADDED Requirements

### Requirement: Package version authority
治理 SHALL 仅以 com.uidatarender/package.json 的 version 为版本权威，使用包内 CHANGELOG.md。任何应用 bundleVersion MUST NOT 用于包治理。

#### Scenario: Candidate matches its tag
- **WHEN** 准备 v0.1.2-rc.N
- **THEN** tagged package.json 为 0.1.2-rc.N，changelog 对应精确 tag，正式目标仍为 v0.1.2

### Requirement: Documentation directory owns governance
Documentation~/roadmap.md 和 Documentation~/todo.md SHALL 是唯一活动入口。根目录 MUST NOT 保留同名文档或跳转占位；迁移 MUST 合并独有内容并修复链接。

#### Scenario: Copies have diverged
- **WHEN** 性能工作已更新 Documentation~ 而根副本仍含旧内容
- **THEN** 保留最新证据并合并独有内容，校验引用后移除根副本

#### Scenario: Documentation directory is absent
- **WHEN** 尚无专用文档目录
- **THEN** 请用户确认路径后创建专用目录，不把 roadmap/todo 放在根目录

### Requirement: Current TODO lifecycle
任务 SHALL 有稳定 ID、动作、节点、状态、验收和来源，延期/阻塞 MUST 含原因与触发条件。节点出入口 SHALL 全量审查实际队列，归档结果和证据后移出完成项；ID MUST NOT 复用。

#### Scenario: An initial task has since completed
- **WHEN** 旧快照的任务已有验收归档
- **THEN** 保留历史追溯，不为保持旧数量而恢复任务，新的发布要求使用后续 ID

### Requirement: Performance acceptance before first RC
首个 RC 发布前 SHALL 完成提取微基准、成本分析、正确性、0 B warm allocation 和代表性 HUD 场景验收。未达参考门槛时 MUST 有预算、风险、重开条件及用户明确例外确认，不得自行放宽。

#### Scenario: Microbenchmark alone exists
- **WHEN** PERF-01 已归档但缺场景证据或所需预算确认
- **THEN** 建立后续验收任务，保留原记录，不声明发布性能门槛通过

#### Scenario: Other performance paths are unchanged
- **WHEN** 本轮未触及 GPU 槽位或 Job 路径
- **THEN** 完整 GPU/Job 对照及场景矩阵可留后续；触及对应路径时补相关验证

### Requirement: Immutable release correspondence
每个 RC 和正式 tag SHALL 有唯一 changelog 条目，包括功能、修复（空则 None）和验证引用。tag MUST 指向版本一致的提交，提交可通过 tag 解析，日期和状态 MUST 有证据。已发布 tag MUST NOT 覆盖、移动或复用。

#### Scenario: Candidate needs a fix
- **WHEN** rc.N 验证失败或需要修复
- **THEN** 保留原 tag/结果，使用递增且未用过的 rc 序号发布新候选，独立记录 changelog 和验证

#### Scenario: Release evidence is missing
- **WHEN** 历史条目无 tag、tag 无条目或远端无法核实
- **THEN** 记录缺口及后续任务，不虚构发布状态

### Requirement: Exact candidate validation
RC 验收 SHALL 覆盖干净安装/编译、布局、EditMode、PlayMode、治理、UIDataRender 行覆盖率至少 70% 和真实 CI。当前真实 CI 执行渠道为 Jenkins；证据 MUST 绑定精确提交。

#### Scenario: Tags do not trigger the workflow
- **WHEN** workflow 仅监听分支 push/PR
- **THEN** 使用同提交已有 CI 结果或建立精确 ref 运行入口，不用其他提交旧 run 代替

### Requirement: Confirmed stable promotion
正式 v0.1.2 SHALL 在 RC 验收通过并经用户确认后发布，保持 RC 功能代码，仅允许版本和发布记录调整，并检查布局和晋升差异。

#### Scenario: Functional changes follow RC acceptance
- **WHEN** 正式准备阶段发生功能代码修改
- **THEN** 生成新 RC 并重新验证，不直接发布正式 tag

#### Scenario: Promotion is confirmed
- **WHEN** RC 验收完整且用户确认
- **THEN** package.json 调整至 0.1.2，正式 changelog 关联 RC 及晋升检查，发布 v0.1.2

### Requirement: Governance completion differs from release completion
治理验收 SHALL 检查链接、ID/节点、归档互斥、版本、布局、OpenSpec 和 diff。归档治理 change MUST NOT 自动关闭发布任务，历史测试 MUST NOT 冒充当前 RC 结果。

#### Scenario: Documentation work finishes first
- **WHEN** 文档治理完成而 RC 未验收
- **THEN** 可归档治理项，M5 发布任务保持活动，不标记正式发布
