## Why

已归档 PERF-01 提供了提取微基准、正确性和零分配证据，但没有代表性 HUD 场景结果。用户已确认 v0.1.2 必须办好性能验收后进入递增 RC 验证，因此需要单独补齐场景证据和未达参考门槛时的预算确认，不重写已完成的历史 change。

## What Changes

- 复用 archive/2026-09-27-reassess-public-ugui-extraction-performance 的微基准与决策，建立稳定后续 TODO ID。
- 新增专用 `HUDRenderTestSample/Assets/Scenes/Scene_Perf_HUD_RC.unity`，使用可配置且可重放的固定负载，分别覆盖静态基线、持续局部更新和集中刷新。
- 通过真实包渲染/更新路径测量 regeneration、主线程/帧时间 median/p95、负载与分配归因，输出原始样本与摘要；复用适合的既有测量组件，不复用既有场景作为唯一验收资产。
- 对未达 5 us / 30x 参考的路径给出场景支持的使用预算、风险与重开条件，取得用户明确例外确认后才通过 RC 前性能门槛。
- 将结果交接给 M5 的精确候选测试和 CI 验证；完整 GPU/Job 对照及场景矩阵仍属后续工作，除非本轮实际修改涉及这些路径。

## Capabilities

### New Capabilities

- `hud-rc-performance-acceptance`: 代表性 HUD 场景测量、预算例外确认和 RC 性能验收交接。

### Modified Capabilities

无。已有 public-ugui-performance 微基准规范保持不变。

## Impact

涉及新增 Sample 场景、Sample 测量/配置入口和有意义的场景接线验证、证据及 Documentation~/roadmap.md 和 todo.md。新资产必须包含 Unity .meta；场景与 driver 不进入可分发包。原 benchmark 和 TestId 保留。运行时优化仅在数据支持时提出并明确范围，不因本提案推测改写；不发布 tag，也不修改已归档 change。本轮仅更新提案。
