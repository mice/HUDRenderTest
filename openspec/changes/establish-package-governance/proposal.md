## Why

HUDRenderPackage 的 roadmap/TODO 有两套入口，历史完成项与当前队列混放，包版本、发布 tag 与 changelog 尚未形成完整对应。用户已确认 v0.1.2 先完成性能验收，再递增 RC 验证，经用户确认后发布正式版，需要把这些条件写成可执行规则。

## What Changes

- 以 com.uidatarender/package.json 为唯一版本来源；正式目标 0.1.2，候选 0.1.2-rc.N，对应 tag v0.1.2 和 v0.1.2-rc.N。
- 使用 Documentation~/roadmap.md 和 Documentation~/todo.md 作为唯一活动入口；合并独有内容、修复相对链接后移除根副本，不在根保留同名跳转文件。
- 全量审查当前 TODO，保留稳定 ID 和已有证据；历史完成项归档，未完成项具备节点、验收、来源及触发条件。初始快照数量不作为固定约束。
- 将 PERF-01 及代表性 HUD 场景验收、候选测试/覆盖率/CI 列为 v0.1.2 发布依赖；GPU 全矩阵及 Job 对照保持后续范围，除非实际改动涉及这些路径。
- 每个 RC 独立记录版本、tag、功能、修复和验证；失败后递增且不覆盖旧 tag；正式发布必须通过 RC 验收并获用户确认。

## Capabilities

### New Capabilities

- `package-release-governance`: 文档目录、TODO 生命周期、包版本追溯、RC 验证及正式晋升门槛。

### Modified Capabilities

无。现有 UPM 分发及仓库验证要求保持有效；性能场景验收由后续 change validate-hud-rc-performance 承接，原性能微基准 change 保持归档。

## Impact

限于 HUDRenderPackage 的治理文档、包元数据/changelog、OpenSpec 和发布验证记录。Sample bundleVersion 不在范围内。新增性能场景验收由 validate-hud-rc-performance 承接，引用 archive/2026-09-27-reassess-public-ugui-extraction-performance 的既有证据。本 change 定义和检查发布流程，实际候选发布与晋升进入 M5 发布队列。本轮只完善提案，不执行测试或发布。
