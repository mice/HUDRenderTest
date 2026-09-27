## Context

PERF-01 已归档，证据为 openspec/changes/archive/2026-09-27-reassess-public-ugui-extraction-performance/evidence/perf-01-benchmark.md，记录 FillData2 median 5.380 us/p95 7.759 us，FillData3 median 6.327 us/p95 9.182 us，0 B warm allocation，119/119 EditMode。这些为已有报告内容，不是本次重新执行结果。

原报告提出 median < 10 us、p95 < 15 us 和 HUD regeneration 1 ms p95 预算；它没有场景实测，也不替代本轮流程要求的用户例外确认。原归档保持不变，后续证据可验证、补充或限定旧结论。

## Goals / Non-Goals

**Goals:** 为首个 v0.1.2 RC 补充代表性 HUD 实测、使用预算、风险与验收结果，并关联原微基准证据。

**Non-Goals:** 不重做完整 GPU/Burst 矩阵，不因微基准数值推断普遍设备保障，不自行放宽门槛，不在本 change 内执行候选发布。

## Decisions

1. 新增专用 Scene_Perf_HUD_RC.unity，位于 HUDRenderTestSample/Assets/Scenes，直接作为可打开、可批处理验证的验收入口。测量 driver/config 位于 Sample，复用 PerfBaselineRunner、PerfProbe 等现有能力中适用的部分，但不修改既有业务演示场景来承担本次唯一验收入口。记录设备/CPU、OS、Unity 版本、Editor/Player、backend、负载数量、更新频率、预热和采样。使用当前环境建立有限适用范围，不虚构目标设备承诺。
2. 测量 regeneration CPU、主线程/帧时间 median/p95、分配及必要的帧上下文。将场景其他系统分配和 extraction 分配区分；零 extraction GC 不等于整个应用零 GC。
3. 有优化时同环境比较前后；没有运行时变化则明确为当前路径实测，不宣称改善。异常或不稳定样本需调查并保留采样配置。
4. 5 us / 30x 保持比较参考，但验收分为环境有效性门和性能结果门。只有 Player/目标设备、有效计数器、重复采样和稳定性满足要求后，超出参考才进入预算例外流程。Editor/Mono、计数器 unavailable 或重复性不足的结果标记为 `inconclusive/environment-limited`，不能直接判定产品失败，也不能用用户例外替代环境复测。旧报告的 accepted 文案不能自动解除门槛。预算数值由实测支持，不把旧 1 ms 设为无证据的新硬指标。
5. 已归档 PERF-01 不复用，新分配后续 ID 并链接原记录。Documentation~ 为唯一治理入口。完成后交接到 establish-package-governance 定义的 M5 发布队列，RC 的 clean install、全量测试、覆盖率及真实 CI 由候选验证负责。

### 专用场景契约

- 提交场景、所需配置及 .meta，默认配置可以直接进入 Play Mode；通过实际包的 holder/mesh/batching 路径执行更新，不能以空操作或模拟等待代替 regeneration。数据输出必须证明实际对象、顶点/索引和更新工作非零。
- 提供三种可重放负载：静态基线（不持续标脏）、持续局部更新（固定比例和顺序）、集中刷新（预定帧触发整批更新）。记录元素数量、网格复杂度、更新比例、频率和 seed。不同规模作为同场景配置而非复制场景；具体数量由实施阶段的有效负载确定并固定到配置，不作为性能门槛凭空指定。
- 显式分离准备、预热、采样和导出。内存预分配及资源准备不计入稳态采样；默认测量区间不进行 UI 文本刷新、Debug 日志或文件写入，导出在采样后完成。零 GC 要求仅对预热后的 extraction 路径判定，其他分配分别归因。
- 每次运行输出配置、环境、提交/版本标识（无法读取时明确 unknown）、逐帧样本和 median/p95 摘要。包含帧号、模式、实际更新数量、regeneration 耗时、主线程/帧时间和分配口径；不支持的计数器标为 unavailable，不能用 0 冒充。集中刷新帧单独汇总，避免被静态帧稀释。
- 可重复 Run/Reset，恢复修改的 timeScale、vSync、targetFrameRate 等设置并清理创建对象；原始 CSV/JSON 写入仓库既有忽略的输出目录，证据文档记录命令和可获取的结果位置。
- 场景接线/运行验证检查引用完整、实际更新数量、模式切换、Reset 清理及报告配置对应；不把噪声敏感的毫秒阈值变成无条件 CI 单元断言。测试 ID 和记录遵循现有 Sample PlayMode 治理。

## Risks / Trade-offs

- Editor 结果被推广到设备 → 明确采样平台与适用边界，证据不足时不做跨设备承诺。
- 已接受旧预算造成误关门槛 → 新任务明确记录用户确认或 pending，不修改历史原文。
- 工作扩张至完整 PERF-03 → 本次只完成代表性发布验证；其他矩阵条目继续保持活动。
- 并行性能工作覆盖文档 → 实施前重读状态和归档，合并而非覆盖。

## Migration Plan

建立后续 ID；设计固定负载配置并新增专用场景及测量入口；验证接线与重置；运行三类负载并输出原始数据；分析并确认预算；提交证据；在 Documentation~ 记录节点验收并清理已完成后续项；交接 M5。测试/工具变更遵循现有 TestId 治理；没有运行时优化时不制造优化结论。

## Open Questions

专用场景方向已确认，无需再次询问是否新增。实施需固定有代表性的负载参数并记录测量口径，用实测支持预算；未达参考门槛的例外须在完整证据形成后请用户确认。
