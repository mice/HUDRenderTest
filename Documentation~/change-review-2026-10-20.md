# OpenSpec change 审查（2026-10-20）

## 结论与范围

审查仓库 `D:/UnityPackages/HUDRenderPackage`，交付包为 `com.uidatarender`，版本 `0.1.2`，符合 Unity UPM 包项目范围。当前 HEAD 为 `3a6f553`。入场已有 `HUDRenderTestSample/Packages/manifest.json` 的 agent-bridge 引用修改，本次保留。

用户在审查后回复“继续”，已同步并归档 `establish-package-governance` 和 `optimize-uiimage-null-texture`。其余 6 个 change 保留活动状态，先补齐下表验收缺口。归档执行结果见文末；表内勾选数保留审查时快照。

合并以主规格和后续任务入口为单位，保留每个原 change 的提交、配置和证据链。`validate-hud-rc-performance` / `PERF-04` 继续作为总体性能验收入口。

## 逐项判定

所有 8 个 change 都是 repo-local / spec-driven，四种规划产物均为 done。下列数量是审查时 CLI 的勾选快照，不等同于独立验收通过。

| Change | 勾选状态 | 建议与依据 |
|---|---:|---|
| `establish-package-governance` | 14/15 | 同步后归档。唯一未完成项 4.3 是同步、归档和更新引用；实际发布由 GOV-002 / GOV-004 承接。 |
| `optimize-uiimage-null-texture` | 4/4 | 补存原始报告后同步归档。null→Sprite 回归测试存在，历史 EditMode 137/137；`d6bdecb` 真机报告来源 clean，但仍在被忽略的 Builds 下，须复制进 change/evidence 并更新引用。 |
| `optimize-uiimage-simple-mesh` | 9/9 | 暂留，补 Sprite fast-path 实测证据。现有 evidence 主要记录 Sprite/texture cache 和 slot remap；后续阶段报告确认默认 runner 无 Sprite。尚未找到足以验收 4.2 中 Sprite fast-path 命中和对照要求的报告。 |
| `optimize-holder-buildmesh-incremental` | 6/6 | 暂留，补 2.1 覆盖。UT_BATCH_007/008 覆盖命中、slot/顺序失效；未找到几何独立变化及冷/热规划完整输出等价测试。实际交付是批次规划缓存，尚非 holder 网格增量生成。 |
| `profile-buildmesh-stages` | 8/8 | 暂留，补 3.1 并统一规格。Package/Sample 测试中未找到阶段统计或关闭诊断时零分配覆盖；规格仍把 null-sprite 列为 fallback。 |
| `optimize-uiimage-null-sprite` | 5/5 | 暂留，补 2.1。UT_UIIMAGE_005 只比较 Managed 无 Sprite 路径；父变换测试 UT_UIIMAGE_004 使用非空 Sprite，Slim writer 单测不能代替 Slim UIImage + null Sprite + parent transform 等价测试。 |
| `profile-simple-quad-write` | 4/5 | 下一项实施任务。1.2 的计时实现被后续提交撤回，但导出仍保留默认零值；先修复采集契约，再完成 2.2 的 Managed/Slim 真机比较。 |
| `validate-hud-rc-performance` | 10/12 | 继续活动。2.3 环境有效性/预算决策、3.4 RC 交接未完成；子优化归档不能代替整体验收。 |

## 主规格合并方案

审查时 main specs 有 4 项，8 个活动 change 的 capability 均未同步；首批执行后 main specs 为 6 项，活动 change 为 6 项。

- 首批新增 `package-release-governance`、`ui-image-null-texture-cache`，保留完整需求，归档到 `openspec/changes/archive/2026-10-20-<change>/`。
- 完成治理 4.3 后勾选，更新 TODO、roadmap、changelog 引用；保持发布任务活动，不改版本或创建 tag。
- 后续按 `ui-image-simple-mesh` → `buildmesh-stage-profiling` → `ui-image-null-sprite-quad` 的行为演进合并。主规格移除“null-sprite 一律 fallback”的旧条件，保留 Simple、useSpriteMesh 和不支持模式的约束；历史 change 原文保留。
- `holder-batch-plan-cache` 保持独立 capability，不按 change 名称宣称已经实现增量网格生成。
- 两个 profiling change 共用一次诊断验收工作，保留 BuildMesh 总阶段与 quad 子阶段的规格边界。
- 未验收的 `simple-quad-write-profiling`、`hud-rc-performance-acceptance` 不作为已交付规格归档。

## 下一步及验收条件

### 1. 修复 quad 诊断，补齐共用验证

入口为 `profile-simple-quad-write`，关联 `profile-buildmesh-stages`，归属 PERF-04 / M5。提交 `80a67b3` 曾为五个子阶段计时；`2f5a0e4` 改成 `ProfilerMarker.Auto()` 后，`UIMeshData` / `UIMeshDataX` 不再给 `lastSimpleQuadWriteTimings` 赋值，runner 仍导出数字。

本地原始报告核对：

- `Builds/HudRc/Xiaomi10/null-texture-report/hud_rc_20260929_145754_098_f415011e.json`：commit `80a67b3`、Managed、五项有非零数据。
- `Builds/HudRc/Xiaomi10/null-texture-report/hud_rc_20260929_154141_590_6ff09f5d.json`：commit `d6bdecb`、Managed、6 个 Burst 刷新帧的五项全部为 0；不能解释为没有成本。

验收条件：

1. 提供有效的可选子阶段计时，或修改报告契约，明确 unavailable 并链接可用的 Profiler 捕获；不能继续输出具有有效测量含义的默认 0。
2. 覆盖阶段统计、关闭诊断时输出等价和 warm allocation；补无 Sprite 的 Managed/Slim 父变换等价及缓存几何变化/冷暖等价测试。
3. 同一提交和设备配置分别采集 Managed、Slim 并重复运行，记录 instrumentation 开关和开销；诊断构建与 Release 数据分别解释。
4. 对照 generation、BuildMesh、write、transform、merge 的 median/p95 和阶段占比，再决定优化目标。

### 2. 明确 UGUI 交付依赖，再做候选验证

当前 `VertexHelperUtils.CopyIndices` 调用仓库内 `com.unity.ugui` 增加的 `VertexHelper.CopyIndices`，Sample manifest 解析到该本地 UGUI。但包 metadata 仍声明 `com.unity.ugui: 1.0.0`，主规格还要求未经本地修改的 UGUI 可编译。布局脚本允许本地 UGUI，其通过不能证明该规格成立。

以新 ID `VERIFY-02` 跟踪：确定并实现 stock-compatible fallback 或明确受支持的定制依赖交付方式，再对齐 metadata、规格、文档和 clean consumer 编译证据。保留 PERF-01 历史归档，不把依赖变化反写成旧微基准已经验证。

### 3. 完成 PERF-04，再进入 RC 队列

最新 null-texture 报告是阶段证据，configuration 为 `useSlim=false`、`targetFrameRate=-1`，不是此前文档的固定 60 FPS 双路径验收。分配计数器 unavailable；单次运行不能把 generation 约 7.8% 的下降全归因于 null texture cache，因为比较区间还包含诊断方式变化。

环境及重复采样通过后，再评估现有 1 ms 产品预算或提交含风险、负载及重开条件的例外。没有本轮预算例外确认、accepted RC 或 stable promotion 确认。随后执行 VERIFY-01（当前覆盖率至少 70%）、clean validation、完整 Unity 回归及精确提交 Jenkins 验证。

## 文档、版本与验证

- 唯一活动文档为 `Documentation~/roadmap.md`、`Documentation~/todo.md`，没有发现根目录副本。Package Documentation~ 继续保存包测试记录。
- 审查前 TODO 11 项，均未勾选，GOV-002 / GOV-004 为 blocked/unverified 或等待发布证据；本轮增加 VERIFY-02 后 12 项。其余 9 项保留原节点，未恢复旧完成项。
- 包版本和稳定目标 `0.1.2`，`com.uidatarender/CHANGELOG.md` 为 Unreleased。本地无 tag，本次 `git ls-remote --tags origin` 成功返回空；无可核实 RC 序号或 stable tag，不能据此断言历史上从未发布。
- `0.1.0` 缺 tag/发布映射和完整功能/修复分类，由 GOV-002 跟踪。0.1.2 条目本轮补充 null texture cache 行为修复及归档证据，其余实际运行时交付内容须在候选准备时补齐并关联证据。
- 本次通过：`openspec validate --all --strict`（12/12）、`Test-PackageLayout.ps1`、`Test-UnitTestGovernance.ps1`（163 tests / 163 records）。
- 本次未运行 Unity 测试、覆盖率、真机采样、clean consumer 编译或 Jenkins。137/137 EditMode、26/26 PlayMode、81.5% 等数字属于不同历史记录，不能拼成当前候选验证。

## 首批执行结果

- [治理 change](../openspec/changes/archive/2026-10-20-establish-package-governance/)已归档，4.3 完成，最终 15/15；[治理主规格](../openspec/specs/package-release-governance/spec.md)保留 8 条需求、12 个场景。
- [null texture cache](../openspec/changes/archive/2026-10-20-optimize-uiimage-null-texture/)已归档，最终 4/4；[主规格](../openspec/specs/ui-image-null-texture-cache/spec.md)保留 1 条需求、2 个场景。
- 原始 JSON 已复制到 null texture change 的 evidence 目录，SHA256 与 Builds 原文件一致；两次归档均保留 `.openspec.yaml`。详细测量限制见[归档 evidence](../openspec/changes/archive/2026-10-20-optimize-uiimage-null-texture/evidence.md)。
- TODO、roadmap、changelog 和性能 change 的治理引用已更新。PERF-04、VERIFY-01/02、GOV-002/004 等 12 项活动 TODO 均继续保留；没有创建 tag、发布或修改 Runtime。
- 下一步仍为 `profile-simple-quad-write` 的有效采集与验证；本次归档不包含该实施工作。
- 归档后复查通过：OpenSpec strict 12/12（6 change + 6 spec）、包布局、治理 163/163、51 个相关本地链接、归档任务完整性、旧活动路径引用清理和 `git diff --check`。原有 manifest 修改保持不变。

## 后续实施更新（2026-10-20）

上述表格和任务分析保留为审查时快照。用户随后继续实施 `profile-simple-quad-write`，已修复默认零值及陈旧计时，加入独立采样开关、样本计数、v2 报告可用性和设置恢复；本地 EditMode 140/140、PlayMode 27/27、治理 167/167 通过。`UT_UIIMAGE_007` 同时补齐无 Sprite Managed/Slim 父变换组合覆盖；其余缓存验证、BuildMesh 完整诊断验证和 Sprite fast-path 性能证据没有因此自动完成。

用户随后接入并授权 Xiaomi 10，已完成四种配置的正反序 8 次对照，14,400 帧及 CSV 校验通过；quad profiling change 达到 5/5。细分计时显著扰动写入成本；关闭时两条路径的生成/写入耗时接近，Slim Partial merge-fill median 为 2.028–2.059 ms，Managed 为 0.466–0.468 ms，值得作为后续定位目标。见 [诊断修复证据](../openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence.md)和[真机对照](../openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence/android-results.md)。

用户继续执行后，UT_PREF_020/021 已补齐 `profile-buildmesh-stages` 完整阶段合计及关闭诊断 warm allocation 验证。最新回归为 EditMode 142/142、PlayMode 27/27、治理 169/169；14,400 帧阶段边界及 137 个 Runtime/runner 源码哈希均核对通过。两份主规格已同步（3 条需求、9 个场景），[BuildMesh change](../openspec/changes/archive/2026-10-20-profile-buildmesh-stages/evidence.md)和 quad change 已分别归档，活动 change 剩 4 项。

下一步定位 Slim merge-fill，优先隔离 `UIMeshDataX.FillToDrawData` 的颜色默认值比较、共享数组访问和逐元素 List 写入；这些目前是代码审查候选原因，尚未证明具体瓶颈。保留颜色（含透明黑）、UV、索引偏移和局部位置语义，再依据独立采样选择优化。整体 PERF-04、12 项活动 TODO 和发布门槛继续保留，未创建提交、tag 或发布。

## Slim fill 后续实施闭环（2026-10-20）

原“候选原因”已收敛为可复现的逐顶点 Color32 装箱：旧 IL2CPP 有 Box/ValueType_Equals，本地两个 Fill 各产生 2,048 次分配事件；四通道零判断保留原颜色契约并消除该分配。UT_MESH_029/030 覆盖输出与预热分配，最新 EditMode 144/144、PlayMode 27/27、治理 171/171、包布局及 OpenSpec strict 13/13 通过。

Mi 10 旧/新/新/旧四轮、7,200 帧和 CSV 校验通过，只有一个 Runtime 文件发生变化。Partial merge-fill median `1.7185–2.0383→0.2277–0.2293 ms`；Partial regeneration 下降 56.1–59.7%，Burst refresh regeneration 下降 23.3–26.6%。固定版已重新安装，临时屏幕常亮设置已恢复。详见 [修复证据](../openspec/changes/archive/2026-10-21-optimize-slim-fill-color-check/evidence.md)和[完整结果/限制](../openspec/changes/archive/2026-10-21-optimize-slim-fill-color-check/evidence/android-results.md)。新 change 完成 5/5，保留活动状态待同步归档，活动 change 共 5 项。

下一步先补 `optimize-holder-buildmesh-incremental` 的几何变化和冷暖完整输出等价验证，再针对 UIImage 生成中的重复布局/属性读取做隔离测量。新版 Burst generation 仍约 6.5 ms；12 项活动 TODO、PERF-04、依赖分发/覆盖率/Jenkins 发布门槛均继续保留。未创建提交、tag 或发布，原有 manifest 修改保持不变。

## 全部旧 change 收尾与分桶探索（2026-10-21）

用户要求先收尾旧 change，后继续优化。缓存新增 UT_BATCH_009/010，Sprite 新增 UT_UIIMAGE_008/UT_PERF_017；147/147 EditMode、28/28 PlayMode、175/175 治理通过。Sprite direct/fallback 四轮 7,200 帧证据补齐，没有可靠整体速度收益。五份主规格已同步，旧五项均位于 `openspec/changes/archive/2026-10-21-*`。

四个优化 change 按完成归档；`validate-hud-rc-performance` 保留 10/12，按未验收阶段交付归档。用户明确“确实未通过”，要求新开分桶 change，并接受两帧轮转、后一桶额外一帧延迟；这不是预算例外。环境和候选门槛继续由 PERF-04/M5 承接。当前唯一新 change 是 [add-holder-bucket-scheduling](../openspec/changes/add-holder-bucket-scheduling/proposal.md)，先按用户要求实测 128 个 holder 的同帧整体更新成本，再收敛设计。

## 2026-10-21 优先级更新

128-holder 两轮测量完成，全量刷新 p95 3.7467 / 3.7664 ms。用户指定优先优化至 1.5 ms，并询问 Burst/SIMD；已建立 [生成优化计划](../openspec/changes/archive/2026-10-25-optimize-128-holder-generation/proposal.md)，首先量化对象查询与准备成本，再比较纯数据批量 C#/Burst/Job。活动 change 现为生成优化和后续分桶两个，均未实现新运行时路径。原总体验收状态不变。

## 2026-10-25 128-holder 优化结果

显式查询复用已通过 150/150 EditMode、29/29 PlayMode 和 179/179 治理校验。Mi 10 ABBA 四轮共 36,000 帧，每轮 100 次全量刷新；候选 p95 1.2971 / 1.2310 ms，原路径 1.9776 / 1.9853 ms，满足局部 1.5 ms p95 目标。峰值 1.5241 ms 已记录；仍不代表原 RC 矩阵、真机 GC 或发布通过。

[生成优化 change](../openspec/changes/archive/2026-10-25-optimize-128-holder-generation/tasks.md)已按实测决定完成 9/9：Burst/Job 原型及 ARM64 SIMD 检查移交 JOB-01，未实施，不以勾选冒充实现。该 change 已于 2026-10-25 归档，5 条需求、8 个场景同步至主规格 `hud-128-generation-performance`。分桶设计已完成，进度 4/10；下一步为 2.1 状态快照与公平调度实现。原子发布、透明排序及结构/资源屏障见 [分桶设计](../openspec/changes/add-holder-bucket-scheduling/design.md)。见 [完整证据](../openspec/changes/archive/2026-10-25-optimize-128-holder-generation/evidence/query-results.md)。
