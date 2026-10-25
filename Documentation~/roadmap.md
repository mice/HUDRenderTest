# HUDRenderPackage Roadmap

> 当前路线图唯一入口。更新时间：2026-10-25。状态以仓库当前结构、现有测试记录和发布证据为准；`Documentation~/Legacy/` 下的计划与审计文件属于历史资料，活动任务以本文和 [todo.md](todo.md) 为准。

## 项目定位

`HUDRenderPackage` 为 Unity 2022.3 提供可独立分发的 UPM UI 数据渲染包 `com.uidatarender`，并用同仓库的 `HUDRenderTestSample` 验证 UGUI、TextMeshPro、纹理分批、Job/Burst 和性能诊断路径。

## 当前基线

- **包结构**：`com.uidatarender` 是 UPM 交付包，Sample 通过 `file:../../com.uidatarender` 引用；当前 Sample 使用仓库内定制 UGUI，Runtime 已调用其 `CopyIndices`。与 stock UGUI 独立分发规格的对齐由 `VERIFY-02` 跟踪，不能沿用迁移时的兼容结论。
- **核心渲染**：公共 `VertexHelper` 提取、`UIMeshData`/`UIMeshDataX`、几何池、`TextureSlotTable`、`MergeBatcher`、`HolderBatchRenderer`、材质绑定和纹理超容量分批已经落地。
- **观测与 Job**：`PerfProbe` 已接入 Fill/Merge/Draw 路径并支持 CSV；Burst SoA 路径已有输出等价测试，旧的托管 Job 路径仍保留。
- **TMP**：`UITMPText` 已进入现有批渲染链路，基础 SDF/描边材质路径和 Sample 场景已存在；多材质、fallback atlas、sprite 与高级 TMP 效果仍有限制。
- **验证**：2026-10-20 收尾本地 EditMode 147/147、PlayMode 28/28、包布局及治理 175/175 通过；Sprite direct/fallback 四轮 7,200 帧及 CSV 校验通过，见 [共同测试记录](../openspec/changes/archive/2026-10-21-optimize-holder-buildmesh-incremental/evidence/closure-validation.json)和 [Sprite 对照](../openspec/changes/archive/2026-10-21-optimize-uiimage-simple-mesh/evidence/android-results.md)。2026-10-21 已完成 128-holder 两轮 3,600 帧测量，整体全量刷新 p95 为 3.7467 / 3.7664 ms，见 [128-holder 基线](../openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/holders128-results.md)；没有新增覆盖率、clean-checkout 或 Jenkins 结果，81.5% 仍为历史记录。
- **包版本**：`com.uidatarender/package.json` 当前为 `0.1.2`；`com.uidatarender/CHANGELOG.md` 的 `0.1.2` 仍为 Unreleased，预期 tag 为 `v0.1.2`，实际发布尚未核实。

2026-10-25 增量验证：查询复用候选已通过 EditMode 150/150、PlayMode 29/29、治理 179/179。Mi 10 四轮 36,000 帧对照中，128-holder 全量刷新候选 p95 为 1.2971 / 1.2310 ms，满足局部 1.5 ms p95 目标；峰值 1.5241 ms，未宣称硬最大值或 RC 整体验收通过。见 [结果与边界](../openspec/changes/archive/2026-10-25-optimize-128-holder-generation/evidence/query-results.md)。

2026-10-25 分桶交付：显式状态队列、2×128 公平轮转、front/back 发布和透明顺序已验证。EditMode 161/161、D3D11 PlayMode 31/31、治理 192/192；Mi 10 六轮主对照和旧绘制控制共 63,036 帧，轮转 p95 1.3555 / 1.3413 ms，局部 1.5 ms p95 目标通过。峰值 5.3688 ms，仍保留长尾、两帧总成本和设备 GC 限制。见 [完整证据](../openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/bucket-results.md)。

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

**PERF-04 中间验收（2026-09-27）**：专用场景、普通/Slim 双路径的 64/256 HUD 三模式实测和 26/26 PlayMode 已完成；见 [完整记录与原始证据](../openspec/changes/archive/2026-10-21-validate-hud-rc-performance/evidence/hud-scene-results.md)。场景运行于 Editor PlayMode/Mono/D3D11，scoped 字节计数器不可用，因此状态调整为 `inconclusive/environment-limited`，不能直接认定产品性能未达标。已用独立 GC.Alloc 正向校准补齐 extraction 零分配证据，119/119 EditMode。PERF-04 保留活动队列，下一步先做目标 Player/设备复测；只有环境有效且仍超出参考门槛时才进入预算例外流程。

**v0.1.2 治理范围**：版本元数据、文档唯一入口、TODO 生命周期、历史发布映射和验证记录。实际创建 tag、推送和发布需要单独授权；本节点不声称包已发布。

**Change 整理及归档（2026-10-20）**：完整复核 11 个活动 TODO 后增加 `VERIFY-02`（UGUI 分发契约），队列共 12 项；GOV-002 / GOV-004 保持阻塞，M3、M4 条目继续原节点。经用户确认，已同步 [治理主规格](../openspec/specs/package-release-governance/spec.md)和 [null texture cache 主规格](../openspec/specs/ui-image-null-texture-cache/spec.md)，分别归档 [治理 change](../openspec/changes/archive/2026-10-20-establish-package-governance/evidence.md)（15/15）和 [null texture cache](../openspec/changes/archive/2026-10-20-optimize-uiimage-null-texture/evidence.md)（4/4），后者原始 JSON 已补存并核对哈希。活动 change 剩 6 项；其验收缺口见 [审查记录](change-review-2026-10-20.md)。本轮没有关闭任何活动 TODO；治理归档不表示 M5 完成或发布通过。PERF-04 下一步修复 quad 子阶段默认零值并完成 Managed/Slim 对照；阶段诊断工作合并组织，历史证据分别保留。

**Profiling 同步归档（2026-10-20）**：BuildMesh 阶段合计、混合目标、关闭诊断输出等价与 warm allocation 验证已补齐，EditMode 142/142、PlayMode 27/27、治理 169/169 通过；14,400 帧真机阶段边界和 137 个 Runtime/runner 源码哈希核对通过。已创建 [BuildMesh 主规格](../openspec/specs/buildmesh-stage-profiling/spec.md)及 [quad write 主规格](../openspec/specs/simple-quad-write-profiling/spec.md)，共 3 条需求、9 个场景，并分别归档 [BuildMesh change](../openspec/changes/archive/2026-10-20-profile-buildmesh-stages/evidence.md)（8/8）与 [quad change](../openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence.md)（5/5），历史证据各自保留。活动 change 剩 4 项。细分计时默认继续关闭，下一步定位 Slim merge-fill 差异。整体复核后 12 项活动 TODO 均继续保留；未关闭 PERF-04，也不将本次工作树验证视为 RC/发布验收。

**Slim fill 修复（2026-10-20）**：新 change [optimize-slim-fill-color-check](../openspec/changes/archive/2026-10-21-optimize-slim-fill-color-check/evidence.md) 已完成 5/5，待同步归档；当前活动 change 共 5 项。颜色判断装箱由生成 IL2CPP 和先失败后通过的分配回归确认；两个 Fill 的预热分配事件由各 2,048 降至 0。四轮 Mi 10 中 Partial merge-fill median 降至 `0.2277–0.2293 ms`，Partial regeneration 下降 56.1–59.7%，Burst regeneration 下降 23.3–26.6%。源码/报告/APK 哈希和原始数据已保存；设备分配计数器不可用，不能据此宣称全应用零 GC。12 项活动 TODO 仍保持开放，M3/M5 不关闭，版本保持 0.1.2 Unreleased。

**现有 change 收尾归档（2026-10-21）**：新增 UT_BATCH_009/010、UT_UIIMAGE_008、UT_PERF_017 后，缓存输出/失效及 Sprite 路径验证缺口关闭。4 个优化分别以 5/5、5/5、6/6、9/9 完成归档；总体验收保留 10/12，以未验收阶段交付归档。五份主规格共 16 条需求、30 个场景已同步。用户确认目标未满足并转入分桶探索，原 12 项 TODO 继续活动，M3/M5、PERF-04 不因归档关闭。新 change 当前仅探索和 128-holder 基线测量，无分桶实现、提交、tag 或发布。

**分桶交付后的 TODO 整体复核（2026-10-25）**：本次以 PERF-04/M5 的子项完成 `add-holder-bucket-scheduling`，目标仍为 0.1.2 Unreleased。完整复核 12 个活动 TODO 后，没有一项可按整体要求关闭：PERF-04 保留原 1 ms/GC/代表性矩阵缺口；GOV-002、GOV-004 保持历史发布与候选 CI 阻塞；VERIFY-02、MAINT-01 继续 M5；VERIFY-01、PERF-02、PERF-03、JOB-01 继续 M3；TMP-01..03 继续 M4。无 TODO ID 被删除或复用。本次局部验收与残余风险见 [分桶结果](../openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/bucket-results.md)，功能记录于 [0.1.2 Unreleased changelog](../com.uidatarender/CHANGELOG.md)。本地无 release tag，未准备或发布 RC，不宣称 M3/M5 完成。

**发布规则**：`package.json` 是包版本唯一来源；候选使用递增且不可复用的 `0.1.2-rc.N` / `v0.1.2-rc.N`，每个候选绑定精确提交和验证证据。CI 不因 tag 推送自动视为通过；正式 `v0.1.2` 只能在 RC 验收完成并获得用户明确确认后发布。

- 每次 Runtime、Shader、包依赖或 Sample 场景变更后执行结构校验、相关 Unity 测试、治理同步和必要的 clean validation。
- 保持 package/Sample 资产边界、第三方 notices、`.meta` GUID 和文档路径一致。
- 将已确认的性能数据、已知限制和兼容性决策回写到包 README、适配说明和版本变更记录。
- 对明确的产品需求再评估 Canvas masking、层级排序、9 宫格等当前设计限制；在需求确认前不把它们作为当前交付阻塞项。

## 优先级顺序

1. **P0**：[128-holder 优化](../openspec/changes/archive/2026-10-25-optimize-128-holder-generation/proposal.md)已归档，[分桶交付](../openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/bucket-results.md)已完成 10/10，局部 1.5 ms p95 目标通过。下一步围绕 PERF-04 补齐代表性资源负载和真机分配证据，继续处理原 1 ms 参考、VERIFY-02 独立分发、覆盖率和精确候选 Jenkins。若需要进一步优化，再启动 JOB-01 的 Burst/SIMD 对照。
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

