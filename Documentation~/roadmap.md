# HUDRenderPackage Roadmap

> 当前路线图唯一入口。更新时间：2026-09-27。状态以仓库当前结构、现有测试记录和发布证据为准；`Documentation~/Legacy/` 下的计划与审计文件属于历史资料，活动任务以本文和 [todo.md](todo.md) 为准。

## 项目定位

`HUDRenderPackage` 为 Unity 2022.3 提供可独立分发的 UPM UI 数据渲染包 `com.uidatarender`，并用同仓库的 `HUDRenderTestSample` 验证 UGUI、TextMeshPro、纹理分批、Job/Burst 和性能诊断路径。

## 当前基线

- **包结构**：`com.uidatarender` 是独立 UPM 包；Sample 通过 `file:../../com.uidatarender` 引用它，依赖闭包使用 stock UGUI、Burst 和 TextMeshPro。
- **核心渲染**：公共 `VertexHelper` 提取、`UIMeshData`/`UIMeshDataX`、几何池、`TextureSlotTable`、`MergeBatcher`、`HolderBatchRenderer`、材质绑定和纹理超容量分批已经落地。
- **观测与 Job**：`PerfProbe` 已接入 Fill/Merge/Draw 路径并支持 CSV；Burst SoA 路径已有输出等价测试，旧的托管 Job 路径仍保留。
- **TMP**：`UITMPText` 已进入现有批渲染链路，基础 SDF/描边材质路径和 Sample 场景已存在；多材质、fallback atlas、sprite 与高级 TMP 效果仍有限制。
- **验证**：最近交付证据记录了 EditMode 118/118、PlayMode 22/22、`UIDataRender` 行覆盖率 81.5% 和 clean-checkout 验证通过。后续真实 CI 由 Jenkins 执行；当前尚未记录绑定候选提交的 Jenkins run。
- **包版本**：`com.uidatarender/package.json` 当前为 `0.1.2`；`com.uidatarender/CHANGELOG.md` 的 `0.1.2` 仍为 Unreleased，预期 tag 为 `v0.1.2`，实际发布尚未核实。

## 里程碑

### M0：UPM 独立分发与可复现验证 —— 已完成

**节点 ID**：`M0`

**TODO lifecycle**：本节点的历史实施任务已在迁移变更归档中完成并退出活动队列；当前活动 TODO 从 `M3` 开始。后续节点继续使用“节点入口审视 → 节点出口整体审视 → 归档并清理活动 TODO”的流程。

**归档记录**：`HIST-001`（UPM 包边界、依赖闭包和 clean-checkout 验证）对应 [UPM 交付总结](../openspec/changes/archive/2026-09-16-complete-upm-migration/evidence/delivery-summary.md)。这是历史迁移引用，不是当前活动任务。

**结果**

- 完成 `com.uidatarender` / `HUDRenderTestSample` 包与 Sample 边界。
- 移除对仓库内嵌 UGUI 和内部字段的编译依赖，改用公共 UGUI API。
- 补齐包元数据、依赖锁定、资源归属、`.meta` 完整性、结构校验和 clean validation。
- 建立 Package EditMode、Sample PlayMode、覆盖率和测试治理记录。

**证据**：[UPM 交付总结](../openspec/changes/archive/2026-09-16-complete-upm-migration/evidence/delivery-summary.md)、[UPM 规格](../openspec/specs/upm-package-distribution/spec.md)、[仓库验证规格](../openspec/specs/repository-validation/spec.md)。

### M1：核心批渲染能力 —— 已完成

**节点 ID**：`M1`

**TODO lifecycle**：核心实现项已完成并记录在[实施状态审计](Legacy/实施状态审计.md)；性能证据不足的事项没有伪装成完成项，已转入 `M3` 的活动 TODO。

**归档记录**：`HIST-002`（核心批渲染能力）和 `HIST-003`（诊断、Burst 等价和测试治理基础）对应[实施状态审计](Legacy/实施状态审计.md)及现有测试记录，均为历史迁移引用。

**结果**

- 几何缓冲区和空闲链表分离，顶点相关并行数组统一扩容。
- `UIMeshDataX` 的矩阵、顶点、UV、颜色和索引输出与共享几何池语义对齐。
- `TextureSlotTable`、`HolderNotifier`、`OwnerRegistry`、`HolderLifecycle` 和 Facade 化 `UIPrefabManager` 已落地。
- `MergeBatcher` 支持按纹理集合分批；`HolderBatchRenderer` 统一 Slim 与非 Slim Holder 路径。
- 8 槽 Shader/C# 开关、Burst SoA 实现和 `PerfProbe` 运行时埋点已具备。

**仍需证据闭环**：8 槽路径的 GPU 成本和 Burst 相对托管 Job 的实际收益见 [todo.md](todo.md)；公共 UGUI 提取的 CPU 回归已在 M3 归档并接受当前预算。

**证据**：[实施状态审计](Legacy/实施状态审计.md)、[实施计划（历史）](Legacy/实施计划.md)。

### M2：TMP 基础适配 —— 已完成（基础范围）

**节点 ID**：`M2`

**TODO lifecycle**：基础 TMP 适配已完成；fallback atlas、sprite、多材质和高级材质效果保留在活动 TODO，等待后续节点定义验收范围。

**归档记录**：`HIST-004`（TMP 基础适配）对应[TextMeshPro 适配说明](../com.uidatarender/Documentation~/TextMeshProAdapter.md)、`UT_SCENE_004` 和已归档 TMP 变更。高级 TMP 能力仍属于 M4 活动规划。

**结果**

- `UITMPText` 和 `TMPMeshDataUtility` 可将单字体、主材质 atlas 的 TMP 网格接入 `IUIData` 与批渲染器。
- `UIPrefabHolder.SetText` 同时支持 legacy `UIText` 和 `UITMPText`。
- `Hidden/UIE-TMP-SDF` 提供基础 SDF、面色和描边色/宽度路径。
- Sample 和 PlayMode 测试覆盖 TMP 网格重建与批网格生成。

**当前边界**：fallback font atlas、TMP sprite、多材质子网格、underlay/glow/bevel/masking 和完整 rich material effect 尚未纳入批渲染契约。

**证据**：[TextMeshPro 适配说明](../com.uidatarender/Documentation~/TextMeshProAdapter.md)、[TMP 规格](../openspec/specs/tmp-ui-rendering/spec.md)。

### M3：性能与交付证据收口 —— 进行中

**节点 ID**：`M3`；**目标版本**：未分配，性能证据不因 v0.1.2 治理周期而关闭；真实 CI 由 Jenkins 发布流程承接。

**TODO lifecycle**：节点入口已建立当前活动项：`VERIFY-01`、`PERF-02`、`PERF-03`、`JOB-01`。`PERF-01` 已归档；`CI-01` 因 CI 执行渠道改为 Jenkins 而废止，已记录在本节点的归档决定中。节点关闭前必须整体复核 `todo.md`，并在本节点下记录每个已归档 ID、验收证据、遗留项及其后续节点；完成项随后从活动 `todo.md` 清理。

**已归档 TODO**：`PERF-01` 已完成节点级整体审视并归档。证据为 [PERF-01 benchmark](../openspec/changes/archive/2026-09-27-reassess-public-ugui-extraction-performance/evidence/perf-01-benchmark.md)、`UT_MESH_018` 和 EditMode 119/119。结论是接受 stock UGUI 公共 API 路径的 CPU 回归，保留 0 B warm-up allocation 与索引契约，当前预算为 median < 10 us/call、p95 < 15 us/call；若出现无分配的索引 API 或 HUD regeneration 超过 1 ms p95，再重新打开性能调查。其余 M3 条目继续保持活动状态。

`CI-01` 已于 2026-09-27 归档为废止项：原 GitHub Actions 一次性验证任务不再执行，后续真实 CI 验证改由 Jenkins 任务承接。该归档不表示 CI 验证已通过；候选发布仍需记录绑定精确提交的 Jenkins run，并由 `GOV-004` 跟踪。

目标是让已实现的路径拥有可重复、可比较、可审计的性能和 CI 证据。

1. 公共 UGUI `VertexHelper` 提取的 CPU 回归已完成复测和决策；`FillData2` 约 4.924–8.285 us、`FillData3` 约 6.038–8.405 us，超过旧路径的 5 us / 30x 参考门槛，但满足当前接受的预算和 0 B warm-up allocation 条件，见已归档 `PERF-01`。
2. 通过 Jenkins 触发一次真实 CI run，确认 License、结构校验、EditMode、PlayMode、覆盖率门禁和 artifact 上传全部成功。
3. 用 Unity Profiler 或可用的帧捕获补齐 3 槽与 8 槽 Shader 的 GPU 对照；据此决定是否调整默认纹理槽数。
4. 建立 UGUI、`UIMeshData`、`UIMeshDataX`、Job 和高并发压力的统一场景/CSV 基线。
5. 在当前提交上重跑覆盖率与治理同步，确认 70% 门禁和记录数量仍与代码一致。

### M4：TMP 高级能力 —— 计划

**节点 ID**：`M4`；**目标版本**：未分配。

**TODO lifecycle**：进入本节点前，重新整体审视 `todo.md`，为 fallback atlas、sprite、多材质和 SDF 效果建立新的稳定 TODO ID。节点结束时按同一出口流程归档完成项。

在 M2 的单字体/单材质约束稳定后分阶段推进：

- 多材质和 fallback atlas 的子网格、纹理注册及分批策略。
- TMP sprite asset 的批渲染支持。
- SDF underlay、glow、bevel、masking、rich text material effect 的明确支持范围。
- TMP 专用视觉回归样例和性能基线；保持 legacy `UIText` 场景兼容。

每一项都需要新增能力说明、EditMode/PlayMode 覆盖和 Sample 证据，避免把未支持的 TMP 资源静默带入生产路径。

### M5：稳定维护与发布 —— 持续

**节点 ID**：`M5`；**目标版本**：`v0.1.2` 治理周期；不等同于已发布。

**TODO lifecycle**：本周期入口快照为 2026-09-27 的活动 TODO：`PERF-04`、`GOV-001`、`GOV-002`、`GOV-003`、`GOV-004`、`CI-01`、`VERIFY-01`、`PERF-02`、`PERF-03`、`JOB-01`、`TMP-01`、`TMP-02`、`TMP-03`、`MAINT-01`。该快照保留当时的入场状态；随后 `CI-01` 按渠道决策归档为废止项，Jenkins 验证要求由 `GOV-004` 继续跟踪。节点出口必须整体审视 TODO，将已完成或废止治理项记录在本节点后清理，性能和 TMP 未完成项继续保留原节点。

**入场审查记录（2026-09-27）**：已重读 `Documentation~/todo.md` 全文，共 14 个活动条目；其中 M5 承诺 `PERF-04`、`GOV-001..004`、`MAINT-01`，M3 的 `CI-01`、`VERIFY-01`、`PERF-02`、`PERF-03`、`JOB-01` 继续承诺，M4 的 `TMP-01..03` 继续后续规划。已确认没有完成项留在活动队列，`PERF-01` 仅通过归档记录保留。M5 目标版本为未发布的 `v0.1.2`，其余条目标记为 future 或未分配版本；节点出口将再次整体复核并记录每个归档 ID、证据和后续节点。该记录是入场快照；随后 `CI-01` 已因渠道决策废止。

**已归档 TODO（2026-09-27）**：`GOV-001` 已通过文档唯一入口、历史索引、节点记录和全量 TODO 复核；`GOV-003` 已通过 `package.json`、`CHANGELOG.md` 与未创建 tag 的一致性检查。证据为本节点入场记录、`openspec validate --all`、`Test-PackageLayout.ps1` 和 `git diff --check`。`GOV-002` 保持 `blocked/unverified`，`GOV-004` 等待首个 RC 的精确提交和 Jenkins 证据；性能、覆盖率和 TMP 工作继续留在各自节点。

**变更后整体复核（2026-09-27）**：用户决定移除 `CI-01` 的 GitHub Actions 一次性任务后，重新读取 `todo.md` 全文。当前活动队列为 11 项：`PERF-04`、`GOV-002`、`GOV-004`、`VERIFY-01`、`PERF-02`、`PERF-03`、`JOB-01`、`TMP-01..03`、`MAINT-01`。`CI-01` 已在本节点归档为废止项，未来 Jenkins 候选验证由 `GOV-004` 承接；没有其他完成项或失去节点归属的条目。

**PERF-04 中间验收（2026-09-27）**：专用场景、普通/Slim 双路径的 64/256 HUD 三模式实测和 26/26 PlayMode 已完成；见 [完整记录与原始证据](../openspec/changes/validate-hud-rc-performance/evidence/hud-scene-results.md)。场景运行于 Editor PlayMode/Mono/D3D11，scoped 字节计数器不可用，因此状态调整为 `inconclusive/environment-limited`，不能直接认定产品性能未达标。已用独立 GC.Alloc 正向校准补齐 extraction 零分配证据，119/119 EditMode。PERF-04 保留活动队列，下一步先做目标 Player/设备复测；只有环境有效且仍超出参考门槛时才进入预算例外流程。

**v0.1.2 治理范围**：版本元数据、文档唯一入口、TODO 生命周期、历史发布映射和验证记录。实际创建 tag、推送和发布需要单独授权；本节点不声称包已发布。

**发布规则**：`package.json` 是包版本唯一来源；候选使用递增且不可复用的 `0.1.2-rc.N` / `v0.1.2-rc.N`，每个候选绑定精确提交和验证证据。CI 不因 tag 推送自动视为通过；正式 `v0.1.2` 只能在 RC 验收完成并获得用户明确确认后发布。

- 每次 Runtime、Shader、包依赖或 Sample 场景变更后执行结构校验、相关 Unity 测试、治理同步和必要的 clean validation。
- 保持 package/Sample 资产边界、第三方 notices、`.meta` GUID 和文档路径一致。
- 将已确认的性能数据、已知限制和兼容性决策回写到包 README、适配说明和版本变更记录。
- 对明确的产品需求再评估 Canvas masking、层级排序、9 宫格等当前设计限制；在需求确认前不把它们作为当前交付阻塞项。

## 优先级顺序

1. **P0**：公共 UGUI CPU 回归、Jenkins 真实 CI run、当前覆盖率/治理复核。
2. **P1**：8 槽 GPU 证据和统一性能场景，确定默认槽数与 Job 路径选择。
3. **P2**：TMP 高级材质/资源能力、低风险代码清理和可选 UI 能力扩展。

## 日常验证入口

```powershell
.\Tools\Test-PackageLayout.ps1
.\Tools\Invoke-LocalValidation.ps1
.\Tools\Invoke-CleanValidation.ps1 -TestPlatform Both
.\Tools\Invoke-LocalValidation.ps1 -WithCoverage -CoverageThreshold 70
.\Tools\UnityTestRunner\Sync-UnitTestGovernance.ps1 -Scope All
```

具体任务、优先级、验收条件和来源见 [todo.md](todo.md)。

