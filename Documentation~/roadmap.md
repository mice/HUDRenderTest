# HUDRenderPackage Roadmap

> 当前路线图唯一入口。更新时间：2026-11-11。状态以仓库当前结构、现有测试记录和发布证据为准；`Documentation~/Legacy/` 下的计划与审计文件属于历史资料，活动任务以本文和 [todo.md](todo.md) 为准。

## 项目定位

`HUDRenderPackage` 为 Unity 2022.3 提供可独立分发的 UPM UI 数据渲染包 `com.uidatarender`，并用同仓库的 `HUDRenderTestSample` 验证 UGUI、TextMeshPro、纹理分批、Job/Burst 和性能诊断路径。

## 当前基线

- **PERF-04 stock 增量（2026-11-11）**：真实 BuiltIn UGUI 1.0.0 与 custom 的横屏 2340×1080 八轮 72,048 帧同负载对照、来源/输出等价和设备恢复均已验收。Burst update median/p95 的运行统计量均值为 custom `6.2247375/6.42495`、stock `8.5542625/8.74620 ms`，stock 分别增加 37.42%/36.13%；不推广为 GPU/FPS 或通用收益。stock 相关 22/22 EditMode、12/12 D3D11 PlayMode，治理 267/267 通过；第三次 Profiler 有 39 个 Bytes/解析栈正控、906 个受控 Editor/Mono 主线程 scope，观测 0 事件/0 B，前两次拒绝证据保留。见 [完整结论](perf-04-stock-scene-results-2026-11-11.md)。预算、目标设备字节和精确 RC/覆盖率门禁继续开放。

- **包结构**：`com.uidatarender` 是 UPM 交付包，Sample 通过 `file:../../com.uidatarender` 引用；当前 Sample 使用仓库内定制 UGUI。`VERIFY-02` 已完成包内公共 API 回退、定制 `CopyIndices` 快速路径兼容及实际 stock UGUI 1.0.0 独立分发验收，详见 [结论与来源](verify-02-stock-ugui-results-2026-11-06.md)。
- **核心渲染**：公共 `VertexHelper` 提取、`UIMeshData`/`UIMeshDataX`、几何池、`TextureSlotTable`、`MergeBatcher`、`HolderBatchRenderer`、材质绑定和纹理超容量分批已经落地。
- **观测与 Job**：`PerfProbe` 已接入 Fill/Merge/Draw 路径并支持 CSV；Burst SoA 路径已有输出等价测试，旧的托管 Job 路径仍保留。
- **TMP**：`UITMPText` 已进入现有批渲染链路，基础 SDF/描边材质路径和 Sample 场景已存在；多材质、fallback atlas、sprite 与高级 TMP 效果仍有限制。
- **验证**：2026-11-06 的 VERIFY-02 回归中，原 Sample 与实际 stock UGUI 消费环境各通过 EditMode 196/196、D3D11 PlayMode 61/61，治理 257/257；stock 首次专项从无 Library 状态完成编译与 13/13 提取测试。UIDataRender 行覆盖率 90.0%（3,342/3,713）仍为历史值，本次未重跑覆盖率。同期完成同 APK 的正式 GPU 矩阵 13 组/52 轮、187,464 个有效样本及设备恢复读回；B32 GPU 中位数较低但部分纯绘制 p95 更高，完整帧收益为 unverified。见 [GPU 与完整帧结论](damage-03-gpu-results-2026-11-06.md)及[历史阶段归档](../openspec/changes/archive/2026-10-28-compare-damage-text-renderers/archive-record.md)。原始工件仅本地保存。Sprite 和 128-holder 真机数据保留各自负载的历史适用范围；Jenkins、Android/IL2CPP 和 RC 验收未在本次重跑。
- **包版本**：`com.uidatarender/package.json` 当前为 `0.1.2`；`com.uidatarender/CHANGELOG.md` 的 `0.1.2` 仍为 Unreleased，预期 tag 为 `v0.1.2`，实际发布尚未核实。
- **PERF-04 增量（2026-11-10）**：generation 量化与独立 Profiler GC 完成；原项目 205/205 EditMode、65/65 D3D11 PlayMode，stock 相关 9/9、12/12，治理 267/267，UIDataRender 行覆盖率 90.2% 通过。Mi 10 八轮 72,048 帧显示正常 generation median 运行均值 8.11815 ms；全阶段探针使 Burst update median/p95 增加 37.42%/44.12%，阶段值仅作仪器化定位。Profiler 39 个正控覆盖 906 个 Editor/Mono 主线程 HUD scope 为 0 事件/0 B，不替代目标设备字节或全应用 GC。stock 场景性能、原预算和精确 RC/覆盖率绑定仍开放。详见 [本轮结论](perf-04-generation-profile-results-2026-11-10.md)。

2026-10-25 增量验证：查询复用候选已通过 EditMode 150/150、PlayMode 29/29、治理 179/179。Mi 10 四轮 36,000 帧对照中，128-holder 全量刷新候选 p95 为 1.2971 / 1.2310 ms，满足局部 1.5 ms p95 目标；峰值 1.5241 ms，未宣称硬最大值或 RC 整体验收通过。见 [结果与边界](../openspec/changes/archive/2026-10-25-optimize-128-holder-generation/evidence/query-results.md)。

2026-10-25 分桶交付：显式状态队列、2×128 公平轮转、front/back 发布和透明顺序已验证。EditMode 161/161、D3D11 PlayMode 31/31、治理 192/192；Mi 10 六轮主对照和旧绘制控制共 63,036 帧，轮转 p95 1.3555 / 1.3413 ms，局部 1.5 ms p95 目标通过。峰值 5.3688 ms，仍保留长尾、两帧总成本和设备 GC 限制。见 [完整证据](../openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/bucket-results.md)。

2026-10-26 战斗飘字：首期数字事件/固定 atlas/整数布局、GPU 动画、持久桶、确定性播放器及 Sample 已完成；正常回放 48/384/640 事件记录，无拒绝/漏显。后继 A/B 与 FP16 已完成实现、回归及 Mi 10 CPU/精度对照并阶段归档；2026-11-06 完成正式 GPU/完整帧测量与分口径结论，完整帧收益未证实。真实 ECS、URP 和业务设备预算继续后续验收，见 DT1/DT2。

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

**节点 ID**：`M3`；**目标版本**：未分配，性能证据不因 v0.1.2 治理周期而关闭；当前只推进性能与本地交付证据，CI 实施已移出活动范围。

**TODO lifecycle**：节点入口已建立当前活动项：`VERIFY-01`、`PERF-02`、`PERF-03`、`JOB-01`。`PERF-01` 已归档；`CI-01` 因 CI 执行渠道改为 Jenkins 而废止，已记录在本节点的归档决定中。节点关闭前必须整体复核 `todo.md`，并在本节点下记录每个已归档 ID、验收证据、遗留项及其后续节点；完成项随后从活动 `todo.md` 清理。

**已归档 TODO**：`PERF-01` 已完成节点级整体审视并归档。证据为 [PERF-01 benchmark](../openspec/changes/archive/2026-09-27-reassess-public-ugui-extraction-performance/evidence/perf-01-benchmark.md)、`UT_MESH_018` 和 EditMode 119/119。结论是接受 stock UGUI 公共 API 路径的 CPU 回归，保留 0 B warm-up allocation 与索引契约，当前预算为 median < 10 us/call、p95 < 15 us/call；若出现无分配的索引 API 或 HUD regeneration 超过 1 ms p95，再重新打开性能调查。其余 M3 条目继续保持活动状态。

`CI-01` 已于 2026-09-27 归档为废止项：原 GitHub Actions 一次性验证任务不再执行，后续真实 CI 验证改由 Jenkins 任务承接。该归档不表示 CI 验证已通过；候选发布仍需记录绑定精确提交的 Jenkins run，并由 `GOV-004` 跟踪。

**VERIFY-01 验证归档（2026-10-26）**：本次在首期飘字的精确工作树源码清单上执行完整布局、EditMode 185/185、实际 D3D11 PlayMode 39/39、治理 224/224 和 UIDataRender 行覆盖率 89.4% / ≥70% 门禁，满足该项本地复核要求。`VERIFY-01` 已从活动 TODO 清理，证据见 验证汇总（原始工件仅本地保存）。这是 dirty 工作树的本地验证，不替代精确 RC 提交、clean-checkout 或 Jenkins；M3 的 PERF-02、PERF-03、JOB-01 继续开放。

目标是让已实现的路径拥有可重复、可比较、可审计的性能和本地验证证据。

1. 公共 UGUI `VertexHelper` 提取的 CPU 回归已完成复测和决策；`FillData2` 约 4.924–8.285 us、`FillData3` 约 6.038–8.405 us，超过旧路径的 5 us / 30x 参考门槛，但满足当前接受的预算和 0 B warm-up allocation 条件，见已归档 `PERF-01`。
2. 用 Unity Profiler 或可用的帧捕获补齐 3 槽与 8 槽 Shader 的 GPU 对照；据此决定是否调整默认纹理槽数。
3. 建立 UGUI、`UIMeshData`、`UIMeshDataX`、Job 和高并发压力的统一场景/CSV 基线。
4. 本地覆盖率与治理复核已由 VERIFY-01 在 2026-10-26 完成；后续功能/候选变化仍需相应重跑，精确 RC 验证继续由 M5 承接。

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

**PERF-04 stock 对照入场复核（2026-11-11）**：整体重读九项活动 TODO：PERF-04、GOV-002、MAINT-01 延续 M5；PERF-02/03、JOB-01 延续 M3；TMP-01..03 延续 M4，GOV-002 继续 blocked/unverified。本轮仅承接真实 stock 场景性能和独立 Profiler scoped 字节，固定 2340×1080 同负载八轮；来源、相关 22/22 EditMode、12/12 D3D11 PlayMode 与 Release 构建已验收。stock Profiler 两次证据不完整，GC 不记为通过。详见 [本轮记录](perf-04-stock-scene-results-2026-11-11.md)。不改变 0.1.2 Unreleased、原预算或精确 RC/覆盖率门禁，不归档活动 TODO。

**PERF-04 stock 对照 / 出口复核（2026-11-11）**：八轮 72,048 帧及两份 CSV 逐行验收；stock Burst update median/p95 运行均值比 custom 高 37.42%/36.13%，四组相邻配对同向。第三次独立 stock Profiler 完整通过 39 正控/906 scope，受控 Editor/Mono 主线程为 0 事件/0 B；前两次仍拒绝，不拼接。原 APK、请求、九项设置与文件名恢复一致，应用停止。完整重读九项活动 TODO，仍全部 carried-forward（M5/M3/M4 各三项），GOV-002 保持 blocked/unverified，JOB-01 按预算触发，不归档或复用 ID。下一步低扰动复核 TMP regeneration，补目标设备 scoped 字节及精确候选测试/覆盖率绑定；0.1.2 仍 Unreleased，PERF-04/M5 和发布门禁不由本轮关闭。

**PERF-04 generation 量化 / 出口复核（2026-11-10）**：交付默认关闭的实际完成 holder/TMP 阶段、计数/可用性与 Unity 尺寸；原项目 205/205、65/65，stock 相关 9/9、12/12，治理 267/267，UIDataRender 行覆盖率 90.2% 通过。冻结来源下同 Mi 10 同 APK 八轮 72,048 帧的请求/几何/方向均等价，正常路径 Burst median/p95 运行均值为 9.2754/10.23545 ms，generation median 为 8.11815 ms。全阶段探针增加 median/p95 37.42%/44.12%，ForceMeshUpdate 仅确认为仪器化叶阶段中的最大项，不称正常路径精确瓶颈或优化收益。独立 Unity Profiler 39 个 Bytes/解析栈正控覆盖 906 个主线程 scope，Editor/Mono 固定负载为 0 事件/0 B；scope 外 136,538 个诊断 GC 样本、其他线程未检验、完整离线重放与目标设备字节仍未验证。原 APK/请求/九项设置恢复后主代理实际读回通过，原始失败组保留。见 [完整结论](perf-04-generation-profile-results-2026-11-10.md)。

出口整体重读九项活动 TODO，全部 carried-forward：PERF-04/GOV-002/MAINT-01 继续 M5，PERF-02/03/JOB-01 继续 M3，TMP-01..03 继续 M4；GOV-002 仍 blocked/unverified，JOB-01 按预算触发，无 ID 归档、删除或复用。下一步补 stock 场景性能，并在优化前以低扰动方法复核 TMP mesh regeneration。原预算、目标设备 scoped 字节、精确 RC/覆盖率绑定继续开放；0.1.2 仍 Unreleased，不关闭 M5，不触发 Jenkins、tag 或发布。

**PERF-04 generation 量化 / 入口复核（2026-11-09）**：承接 [profile-representative-hud-generation](../openspec/changes/profile-representative-hud-generation/proposal.md)，先补实际 Rotating 完成 holder 的阶段采样，再量化同 APK 探针开关开销与 generation 构成。用户指定 GC 验收通过 Unity Profiler，避免增加运行时统计代码；独立 Profiler 诊断必须有正控和有效采样范围，不把其耗时混入 Release 结果。完整重读九项活动 TODO，PERF-04/GOV-002/MAINT-01 继续 M5，PERF-02/03/JOB-01 继续 M3，TMP-01..03 继续 M4，GOV-002 仍 blocked/unverified、JOB-01 按预算触发；不新增/复用 TODO ID，不改变预算/版本/发布状态。stock 场景性能、scoped 字节和精确 RC/覆盖率继续开放。

**PERF-04 TMP 提取复用子项 / 出口复核（2026-11-08）**：完成 [reuse-tmp-extraction-scratch](../openspec/changes/reuse-tmp-extraction-scratch/proposal.md) 的分配归因、缓冲复用、正确性和两种 UGUI 验证。原 Sample EditMode 204/204、D3D11 PlayMode 64/64、stock 相关 EditMode 12/12、PlayMode 11/11、治理 265/265 通过。Mi 10 九轮 56,754 帧（含排空）来源/请求/几何/方向验收通过；历史正式横屏为 2340×1080，因此保留当前方向和分辨率。混合 HUD 有界分配事件由 10,360 降为 0，正控有效且无饱和，字节不可用。Release Burst 刷新 p95 运行均值 `10.7578→9.86305 ms`（观测下降 8.32%）；历史无 TMP 单对无一致收益。每版本两次运行、中断 556 秒及环境端点限制已披露。原 APK/请求/九项设置恢复读回通过。见 [完整结论](perf-04-tmp-scratch-results-2026-11-08.md)。

完整重读活动 TODO 后，九项均 carried-forward：PERF-04、GOV-002、MAINT-01 继续 M5；PERF-02/03、JOB-01 继续 M3；TMP-01..03 继续 M4。GOV-002 保持 blocked/unverified，JOB-01 按预算触发，无 TODO ID 归档、删除或复用。下一步量化剩余 generation 成本，补 stock 场景性能、设备 scoped 字节、精确 RC/覆盖率门禁；不改预算、版本或发布状态，M5 和 PERF-04 继续开放。

**PERF-04 代表性负载阶段完成 / 出口复核（2026-11-07）**：三独立 Sprite 纹理、主 atlas TMP、分桶文本快照、正向分配事件控制及来源报告已交付。原 Sample EditMode 196/196、D3D11 PlayMode 64/64、治理 260/260 通过；Mi 10 六轮 Release 共 54,036 个含排空样本，另有独立 Development 诊断和 smoke。混合负载 Burst 刷新 update p95 为 SingleBucket 21.1027–23.2966 ms、Rotating 10.7821–11.4473 ms；两帧总成本与 Partial 无稳定改善，刷新存在分配事件。历史控制当时 2.0837/2.5211 ms 未复现旧 1.5 ms 目标；Release/scoped 分配字节仍不可用。原 APK、请求缺省状态和 9 项设置恢复读回通过。见 [完整结论与本地证据索引](perf-04-representative-results-2026-11-07.md)及 [0.1.2 Unreleased changelog](../com.uidatarender/CHANGELOG.md)。

完整重读 TODO 后，9 项均 carried-forward：PERF-04、GOV-002、MAINT-01 继续 M5；PERF-02/03、JOB-01 继续 M3；TMP-01..03 继续 M4。GOV-002 保持 blocked/unverified，JOB-01 按预算触发，无 ID 归档、删除或复用。下一步先定位 TMP 生成分配/缓存，并匹配环境复核当前控制；不把本轮可追溯工作树测量记作 RC、stock 场景性能、覆盖率或发布通过，不改变预算。

**PERF-04 代表性负载入口复核（2026-11-06）**：承接 [measure-representative-hud-workloads](../openspec/changes/measure-representative-hud-workloads/proposal.md)，用户选择仓库现有 Sprite、TMP 和多纹理资源作为可复现负载。完整复核 9 个活动 TODO：PERF-04、GOV-002、MAINT-01 延续 M5；PERF-02/03、JOB-01 延续 M3；TMP-01..03 延续 M4。GOV-002 保持 blocked/unverified，JOB-01 按预算触发。目标保持 0.1.2 Unreleased，不新增 TODO ID，不把历史 null-Sprite 局部 1.5 ms 目标扩展为新负载预算；代表性设备计时、分配、精确候选和覆盖率证据分别核实。

**VERIFY-02 入口复核（2026-11-06）**：完整重读 10 个活动 TODO，本轮承接 `VERIFY-02 / M5 / v0.1.2`，实施 [stock UGUI 兼容变更](../openspec/changes/restore-stock-ugui-extraction/proposal.md)。PERF-04、GOV-002、MAINT-01 延续 M5，PERF-02/03、JOB-01 延续 M3，TMP-01..03 延续 M4；GOV-002 保持 blocked/unverified。已加入回退实现、资源清理和 UT_MESH_031..036，治理静态检查 257/257；唯一复用 stock 快照位于 `D:/workspace/HUDRenderPackageValidation`，准备阶段未启动 Unity。stock 消费项目的真实编译、正确性和分配验证通过前，不关闭 VERIFY-02，活动队列仍为 10 项。未改包版本或发布状态。

**完成记录 / 归档 VERIFY-02（2026-11-06）**：用户授权自由使用编辑器后，原 Sample/定制 UGUI 与 stock BuiltIn 1.0.0 两种环境的提取专项各 13/13、完整 EditMode 各 196/196、D3D11 PlayMode 各 61/61 通过，无失败/跳过，治理 257/257、布局、OpenSpec strict 20/20 和 diff 检查通过。首次 stock 消费从无 Library 状态编译；随后同一目录改为 detached worktree，修复普通导出缺少 Git 来源导致的一项 Sample 报告断言失败，保留失败证据并完成专项及全套复测。stock 实际依赖路径/源码哈希、定制方法缺失、预热零分配和资源清理见 [验收结论](verify-02-stock-ugui-results-2026-11-06.md)；主规格已同步，修复进入 [0.1.2 Unreleased changelog](../com.uidatarender/CHANGELOG.md)。

**VERIFY-02 出口整体复核**：完整复核 10 项活动 TODO，`VERIFY-02 / M5 / v0.1.2` 分类为 completed 并移出队列。PERF-04、GOV-002、MAINT-01 延续 M5，PERF-02/03、JOB-01 延续 M3，TMP-01..03 延续 M4；最终 9 项（M5 3、M3 3、M4 3），GOV-002 仍 blocked/unverified，JOB-01 保持按预算触发。无需新增 ID，stock 回退的场景性能确认由既有 PERF-04 承接。回退的 `<65,000` 顶点/额外 Mesh 成本已文档化，未把功能验收扩为性能、覆盖率或 RC/发布通过。版本仍为 0.1.2 Unreleased。

**当前范围调整（2026-10-27）**：按用户要求，将 `GOV-004` 及 Jenkins/候选 CI 执行任务从活动队列和 DT2 验收移除，后续另行安排。此次为移出当前范围，不记录为 CI 已通过。整体复核后活动 TODO 从 12 项减为 11 项（M5 4、M3 3、M4 3、DT2 1），其余条目继续原节点；历史快照保留。正式发布仍按发布规格执行。

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

### DT1：战斗飘字与确定性播放器 —— 已完成（首期功能范围）

**节点 ID**：`DT1`；**目标版本**：`0.1.2 Unreleased`，发布门禁仍由 M5 承接。

**TODO lifecycle 入场复核（2026-10-26）**：完整读取活动 TODO 的 12 项。`PERF-04`、`GOV-002/004`、`VERIFY-02`、`MAINT-01` 继续 M5，`VERIFY-01`、`PERF-02/03`、`JOB-01` 继续 M3，`TMP-01..03` 继续 M4；历史门禁按原节点处理。新增 `DAMAGE-01` 承接已授权 [add-damage-text-playback](../openspec/changes/archive/2026-10-26-add-damage-text-playback/proposal.md) 的 M1–M3；后继 [渲染对照/FP16](../openspec/changes/archive/2026-10-28-compare-damage-text-renderers/proposal.md) 仍为计划。

**完成记录 / 归档 DAMAGE-01（2026-10-26）**：独立事件/布局、固定 atlas、GPU 动画、持久桶和播放器已实现；完整 EditMode 185/185、D3D11 PlayMode 39/39、治理 224/224、布局及 89.4% 行覆盖率门禁通过，新增 32 项测试。使用契约见 [DamageTextPlayback.md](../com.uidatarender/Documentation~/DamageTextPlayback.md)，逐项需求、源码/报告哈希与截图见 实施验收（原始工件仅本地保存）。功能写入 [0.1.2 Unreleased changelog](../com.uidatarender/CHANGELOG.md)，未发布。

**TODO lifecycle 出口整体复核**：重新通读整个队列，`DAMAGE-01` 按以上证据完成并移出活动队列；本地复核同时满足 `VERIFY-01`，在 M3 单独归档。新增 `DAMAGE-02 / DT2` 承接 M4–M5。其余 11 项继续原节点；队列最终 12 项（M5 5、M3 3、M4 3、DT2 1）。GPU/真机性能、FP16、真实 ECS、URP 与业务替换继续开放，不因本节点完成而通过。2026-10-26 按用户要求，将 13 条需求、26 个场景同步到 [数字渲染主规格](../openspec/specs/damage-text-rendering/spec.md)和 [播放器主规格](../openspec/specs/damage-text-playback/spec.md)，并归档至 `2026-10-26-add-damage-text-playback`；后继对照的最新状态见 DT2。

### DT2：数字渲染对照与 FP16 —— 阶段归档（GPU 续测已收口）

**节点 ID**：`DT2`；**目标版本**：未分配。`DAMAGE-02` 承接 DT1，完成共享 Quad A / 持久 Mesh B、三模式/两阶段采样、局部 XY FP16、可追溯报告和原生 Vulkan 查询通路。规格已同步到 [渲染对照](../openspec/specs/damage-text-renderer-comparison/spec.md)与 [FP16](../openspec/specs/damage-text-local-fp16/spec.md)；阶段范围和结论见 [归档记录](../openspec/changes/archive/2026-10-28-compare-damage-text-renderers/archive-record.md)，功能进入 [0.1.2 Unreleased changelog](../com.uidatarender/CHANGELOG.md)。

**TODO lifecycle 入口复核（2026-10-26）**：当时通读 12 个活动条目，本节点承诺 `DAMAGE-02`。M5 的 PERF-04、GOV-002/004、VERIFY-02、MAINT-01，M3 的 PERF-02/03、JOB-01，M4 的 TMP-01..03 按原节点延续。2026-10-27 用户将 GOV-004 / Jenkins 移出当前范围后，队列为 11 项。

**阶段交付 / 归档 DAMAGE-02（2026-10-28）**：用户明确要求先记录、同步规格和归档，保留 21/22 的实际状态。已完成 13 组 ABBA、52 轮、187,450 帧 CPU/生命周期对照及 32 场景/640 点精度检查。连续生命周期外层 CPU 中位数 A 约 0.092 ms、B32 约 0.173 ms；FP16 相对 B32 增加约 0.0327 ms，Float32 保持默认。已有源码验证为 190/190 EditMode、61/61 D3D11 PlayMode、251/251 治理、90.0% UIDataRender 行覆盖率及适用的干净快照验证。

原生查询完成 1,680 帧校准及 3,600 帧非排名记录诊断；最新 10 组/40 张 A/B、B32/B16 与插桩 off/on 图逐字节一致。设备 thermal status 3 不满足正式门槛，插桩影响 ABBA 和正式 GPU 矩阵尚未执行；`tasks.md` 的 3.1 保持未勾选，GPU/完整帧收益未验证，不记为整个 DT2 已通过。

**TODO lifecycle 出口整体复核**：完整复核活动队列，`DAMAGE-02` 分类为阶段交付归档，未完成的 GPU 验收转入新 ID `DAMAGE-03 / DT2`，状态 `deferred/environment-limited`、目标版本未分配。下一触发条件和验收见 [todo.md](todo.md)；不把未完成门禁归档为通过。其余 10 项继续原节点：M5 的 PERF-04、GOV-002、VERIFY-02、MAINT-01；M3 的 PERF-02/03、JOB-01；M4 的 TMP-01..03。归档后活动队列仍为 11 项，未恢复历史任务。

本轮未重启设备。Jenkins 另行安排；C excluded，D not-integrated，其他 API/URP、真实业务 ECS 与主城全局约 10 次更新/秒继续后续范围。原始工件及旧源码检查点仅本地保存，远端保留源码、主规格和最终结论；未创建 RC/tag/release。

**DAMAGE-03 续测入口（2026-10-28）**：重新完整复核 11 个活动 TODO，本节点仅承接 DAMAGE-03，其余 10 项保留原节点；目标版本仍未分配。用户确认正常重启并手动解锁后，系统热状态恢复为 0，未使用温控 override。已重新备份 APK/请求/设置并核对候选 APK 的运行时代码与当前提交一致，继续同 APK 的 16 轮插桩开关对照及 13 组/52 轮正式 GPU 矩阵；归档的 DAMAGE-02 仍保留当时 21/22 的历史状态，不提前关闭未完成验收。

插桩 off/on/on/off 的 A/B × 纯绘制/生命周期四组共 16 轮、57,600 个样本已通过单轮及输入一致性检查，电池温度 30.2–33.2°C，热状态全为 0。相机回调的绑定 CPU 和准备成本单列，60 Hz 节奏限制整帧小差异的判断，不推算未插桩 GPU 时间。正式矩阵首次入口在任何采样开始前遇到本地 Windows 路径参数问题，保留失败记录并恢复设备后，已从新目录开始；有效采样不覆盖或静默重跑，最终 GPU/完整帧验收仍待完成。

**DAMAGE-03 中断审计及续测入口（2026-11-06）**：按用户接受的计划，完整复核 11 个活动 TODO，本轮仅承接 DAMAGE-03，其余 10 项继续原节点。设备端恢复了 2026-10-28 15:21:54 UTC 的第 28 轮导出；采样实际完成，主机没有留下拉取、完成登记及结束温控记录，主机中断原因未知。28 轮报告的哈希、输入/状态、逐帧原生查询与排空校验通过；静态 2/5/8 位、连续生命周期及 127/128 爆发共 6 个完整 ABBA 组、24 轮可保留。原 129 组的 A2 缺少同期结束温控，整组保留为 environment-unverified，不把当前设备温度补作历史证据。

已重新备份接入时的 APK、请求与屏幕/GPU 调试设置，原 APK 哈希、构建 GUID、校准、画面及插桩开关证据保持一致；当前 Runtime/基准源码与历史构建提交无功能差异，Sample 依赖修改不计入该 APK。新会话注册 7 组/28 轮：完整重测 129，继续未启动的 256、高重叠、B32/B16 与实时负载；各 ABBA 四轮保持在同一会话内。原温控、预热、采样、排空、保留尾部及无效即停规则不变。以上为入口快照，正式矩阵及结论的完成状态见下文；原始证据仅本地保存，历史 DAMAGE-02 仍为 21/22。

**完成记录 / 归档 DAMAGE-03（2026-11-06）**：旧 24 + 新 28 轮组成 13 个完整 ABBA 组、52 轮、187,464 个有效样本；原 129 四轮另行保留为 environment-unverified。每轮至少 3,000 帧，爆发每轮 100 个完整窗口，原始哈希、输入/状态、原生逐帧查询、排空和起止温控均通过；电池范围 25.2–30.2°C，thermal status 0。设备 APK/请求/屏幕/旋转/GPU 调试设置恢复后再次实际读回通过。同 APK 原 16 轮/57,600 样本插桩对照继续适用，计时区间包含插桩开销。

B32 的 HUD GPU 运行中位数比 A 低 1.45%–6.58%，但部分纯绘制 p95 更高；A 生命周期 CPU 更低。FP16 没有可靠 GPU 改善，固定连续生命周期外层 CPU 约增加 0.061725 ms / 28.21%，Float32 保持默认。实际帧间隔受 60 Hz 节奏限制，完整帧收益明确为 unverified；按 DAMAGE-03 的分口径测量与结论验收完成，不宣称业务替换、统一分布优势或发布通过。完整结果、统计局限和本地证据索引见 [最终结论](damage-03-gpu-results-2026-11-06.md)。本轮不改 Runtime，不重跑 Unity 测试或构建；历史 DAMAGE-02 仍保留归档时 21/22。

**TODO lifecycle 出口整体复核（2026-11-06）**：完整复核 11 个活动条目，`DAMAGE-03 / DT2 / target: unassigned` 分类为 completed，以上验收及最终结论为证据，已移出活动 TODO。其余 10 项 carried-forward 至原节点：M5 的 PERF-04、GOV-002、VERIFY-02、MAINT-01；M3 的 PERF-02/03、JOB-01；M4 的 TMP-01..03。GOV-002 仍 blocked/unverified，JOB-01 仍按预算触发；无新增或复用 TODO ID。未把 FP16 美术接受、其他 API/URP、真实业务 ECS、主城、Jenkins 或 RC 门禁归档为通过。活动队列最终 10 项（M5 4、M3 3、M4 3），后续优先 VERIFY-02。

## 优先级顺序

**DAMAGE-03 与 VERIFY-02 已完成**。PERF-04 已完成 TMP 提取复用、generation 量化、真实 stock 场景性能对照及两种依赖各自的受控 Editor 主线程 Profiler GC 验收；下一步以低扰动方法复核潜在优化点，继续处理目标设备 scoped 字节和精确 RC/覆盖率门禁。

1. **P0**：按 [stock 对照结论](perf-04-stock-scene-results-2026-11-11.md)继续低扰动复核 TMP mesh regeneration，再决定后续优化；当前同负载 custom/stock Burst update median 均值为 6.2247375/8.5542625 ms。上一轮全阶段探针干扰明显，仪器化 ForceMeshUpdate 值不等于正常成本；不同轮次绝对时间不作代码收益对照。历史 [128-holder](../openspec/changes/archive/2026-10-25-optimize-128-holder-generation/proposal.md)和[分桶](../openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/bucket-results.md)局部 1.5 ms 通过不扩展为当前通过。原预算、目标设备 scoped 字节、精确 RC/覆盖率绑定继续由 PERF-04 承接，JOB-01 按预算触发。
2. **P1**：8 槽 GPU 证据和统一性能场景，确定默认槽数与 Job 路径选择。
3. **P2**：TMP 高级材质/资源能力、低风险代码清理和可选 UI 能力扩展。

## 日常验证入口

```powershell
.\Tools\Test-PackageLayout.ps1
.\Tools\Invoke-LocalValidation.ps1
.\Tools\Invoke-CleanValidation.ps1 -TestPlatform Both -WithGraphics
.\Tools\Invoke-LocalValidation.ps1 -WithCoverage -CoverageThreshold 70
.\Tools\UnityTestRunner\Sync-UnitTestGovernance.ps1 -Scope All
```

具体任务、优先级、验收条件和来源见 [todo.md](todo.md)。
