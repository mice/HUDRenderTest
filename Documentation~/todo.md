# HUDRenderPackage TODO

> 当前未闭环事项。更新时间：2026-09-27。`[ ]` 表示仍需执行或重新验证；完成项已归档到 roadmap 节点，不留在活动队列。每个活动条目必须绑定 roadmap 节点；节点完成时，先整体审视本文件，再把已完成条目写入节点记录并从这里清理。

## 节点绑定规则

- `node: M3`：当前节点承诺处理的条目。
- `node: M4` 或 `node: future`：后续节点或尚未承诺的条目。
- `blocked:` / `deferred:`：保留在活动队列，必须写明原因和下一触发条件。
- 已完成条目不长期保留在这里；它们归档到对应 roadmap 节点的 `TODO lifecycle` 记录，并保留原始 TODO ID。

## M5 v0.1.2 治理周期

- [ ] **PERF-04 代表性 HUD 场景性能验收** (`node: M5`, `target: v0.1.2`, `source: validate-hud-rc-performance`)
  - **行动**：基于已归档 `PERF-01` 微基准，新增专用 `Scene_Perf_HUD_RC.unity` 和 Sample runner，记录静态、局部更新、集中刷新下的 regeneration CPU、主线程/帧时间 median/p95、负载、预热、采样环境和分配归因。沿用 `PERF-04`，不重新分配已有后续 ID。
  - **验收**：先通过 Player/目标设备、计数器有效性、重复采样和噪声控制的环境有效性门；再关联 `PERF-01` 正确性与 0 B warm allocation 证据，对照 5 us / 30x 参考形成场景预算、风险和重开条件。环境无效时不得直接判定性能失败或要求用户确认例外。
  - **状态（2026-09-27）**：`inconclusive/performance-over-budget-and-traceability-incomplete`。r3/r4 已在 Xiaomi 10 稳定运行；r4 将目标帧率设为 60 后，Partial regeneration（generation+merge）median `2.3046 ms`，Burst 全量刷新 median `11.1100 ms`，均超过整体 `1 ms` 预算。r3 的 33 ms 已确认主要是 30 FPS 节奏；r4 帧间隔约 16.75 ms。由于报告 `commit/sourceStatus/sourceFingerprint` 为 `unknown` 且 IL2CPP scoped allocation 不可用，仍不能作为发布候选最终验收。下一步用 exact candidate/source manifest 重建，并针对 generation/merge 优化后复测。见 [场景实测报告](../openspec/changes/validate-hud-rc-performance/evidence/hud-scene-results.md)。
  - **GC 定位（2026-09-27）**：`FillToDrawData` 的主要分配来自调用方四个 List 的扩容；`Rebuild` 每次从零创建这些 List，并且 `MergeBatcher.Plan`/`BuildLocalSlotMap` 每次创建 HashSet、List、Dictionary。`MergeJob.Execute` 本身未发现托管容器分配。优先复用/预分配 Rebuild 容器，再做直接数组或 NativeArray 写入优化。
  - **GC 修复进展（2026-09-27）**：编辑器复测确认合并输出 List 复用已消除该路径 GC；随后已加入 `MergeBatcher`/`RenderBatch` 的 slot scratch、批次、排序和 local map 复用。待项目锁释放后执行完整 Unity 回归与 Xiaomi 10 r5 量化，继续定位 Mesh.Set*、材质绑定和其他引擎侧分配。
  - **18 KiB 热点修复（2026-09-27）**：`CollectSlots`/`Plan` 改为索引循环，避免接口 `foreach` 枚举器 boxing；`RenderBatch` holder 容器按输入规模预留，避免 `TryAdd` 扩容。需在编辑器 Profiler 中复测该 marker；若仍有固定 18 KiB，应拆分 `Debug.LogWarning` 与 Mesh.Set* 引擎分配。
  - **UnionWith 修复（2026-09-27）**：`RenderBatch.Reset`/`TryAdd` 改为预分配 HashSet 后逐项 `Add`，移除 `HashSet.UnionWith`；需用编辑器 Profiler 确认 18 KiB 热点是否消失。
  - **计时器分配修复（2026-09-27）**：`HolderBatchRenderer.Rebuild` 已将 `Stopwatch.StartNew()` 替换为 `Stopwatch.GetTimestamp()`，消除每次重建的 Stopwatch 对象分配。
  - **优化前基线（2026-09-27）**：基于提交 `9a6603a` 的 Xiaomi 10 IL2CPP/ARM64/Release/Vulkan APK 已完成基线采样。Partial regeneration median `1.4641 ms`，Burst 全量刷新 median `10.9372 ms`，均超过 1 ms 预算。后续优化必须复用同一配置和设备对比；详见 [hud-scene-results.md](../openspec/changes/validate-hud-rc-performance/evidence/hud-scene-results.md)。
  - **CopyIndices 复测（2026-09-27）**：新增 `VertexHelper.CopyIndices` 后，Xiaomi 10 相同配置 Partial regeneration median `0.6395 ms`，Burst `2.7551 ms`；相对基线下降约 56.3%/74.8%。Partial 已低于 1 ms，Burst 剩余瓶颈在 `buildMesh`（median 1.9018 ms），继续定位 UIImage/UGUI 生成路径。
  - **延迟顶点变换（deferred）**：将 `TransformVertex` 从 mesh 缓存写回阶段延迟到最终 `FillToDrawData`/合并输出阶段，避免重复修改缓存顶点；当前仅记录方案，待确认多次生成、Slim/Managed 两条路径和批处理语义后再评估。
  - **Graphics.DoMeshGeneration CPU 分解（2026-09-27）**：已确认 `UIImage.OnPopulateMesh` 存在基础 Image 生成、逐顶点复制和 `VertexHelper.FillMesh` 往返三段工作；新增四个 Profiler marker，下一步按编辑器采样结果决定绕过基础生成或优化索引复制。
  - **FillMesh 快速路径（2026-09-27）**：`VertexHelper.FillMesh` 内部不可继续拆分；4 顶点/6 索引标准四边形现在直接写固定索引，复杂几何保留原回退路径。需复测 CPU 基线和拓扑测试。
  - **Runner 阶段基线（2026-09-27）**：`HudRcPerformanceRunner.AdvanceFrame` 已拆分 `runnerBookkeepingMs`、`holderSelectionMs`、`buildMeshMs`，用于识别 Runner 自身调度是否污染 generation 数据。
  - **Runner ProfilerMarker（2026-09-27）**：已加入 `HUDRC.Runner.Bookkeeping/HolderSelection/BuildMesh/Rebuild/Draw` marker；下一轮 Profiler 采样需记录 Burst 的 256 个 BuildMesh 调用分布。
  - **来源**：[validate-hud-rc-performance](../openspec/changes/validate-hud-rc-performance/)、[真机验收矩阵](../openspec/changes/validate-hud-rc-performance/evidence/device-acceptance-matrix.md)、[PERF-01 archive](../openspec/changes/archive/2026-09-27-reassess-public-ugui-extraction-performance/evidence/perf-01-benchmark.md)。

- [ ] **GOV-002 核实 v0.1.0 历史发布映射** (`node: M5`, `target: future`)
  - **行动**：核对本地和远端 tag、发布提交、日期及 `0.1.0` changelog 条目；远端不可查时保持 `blocked/unverified`。
  - **状态**：`blocked/unverified`。当前 `git ls-remote --tags origin` 返回空结果；这只能说明本次查询未发现远端 tag，不能证明 `0.1.0` 从未发布。
  - **验收**：有 tag/提交/日期证据，或有带触发条件的阻塞记录；不得改写历史条目。
  - **下一触发条件**：获得 release、tag 或 registry 发布证据后重新查询并更新映射。
  - **来源**：[com.uidatarender/CHANGELOG.md](../com.uidatarender/CHANGELOG.md)、Git tag 查询。

- [ ] **GOV-004 核对候选版本与 CI 验证对应关系** (`node: M5`, `target: future`, `blocked: release evidence pending`)
  - **行动**：通过 Jenkins 为每个 `v0.1.2-rc.N` 记录精确提交、tag、changelog、clean checkout、EditMode、PlayMode、治理、覆盖率和真实 CI run；确认 tag 推送不会被误认为已触发 CI。
  - **验收**：同一提交的验证证据完整，失败候选递增到下一个未使用的 RC 序号；正式 v0.1.2 仍需用户明确确认。
  - **触发条件**：创建首个 RC 或发现远端发布状态可核实时执行。
  - **来源**：[package-release-governance spec](../openspec/changes/establish-package-governance/specs/package-release-governance/spec.md)。

## P0：交付证据与性能门槛

- [ ] **VERIFY-01 复核当前覆盖率与测试治理** (`node: M3`)
  - **行动**：在当前提交运行 `Invoke-LocalValidation.ps1 -WithCoverage -CoverageThreshold 70` 和 `Sync-UnitTestGovernance.ps1 -Scope All`。
  - **验收**：`UIDataRender` 行覆盖率不低于 70%；Package/Sample 测试与记录双向一致、无重复或孤立记录；将最新数字写回证据文档。
  - **参考基线**：最近交付证据为 118 个 EditMode、22 个 PlayMode、81.5% 行覆盖率。

## P1：性能场景与渲染路径决策

- [ ] **PERF-02 补齐 3 槽/8 槽 GPU 对照** (`node: M3`)
  - **现状**：已有 `Scene_Tex_03_8SlotKeyword`、`EightSlotPerfRunner` 和 CPU A/B 数据；GPU 成本尚无可用帧捕获证据。
  - **行动**：用 Unity Profiler Detailed 或可用的 GPU frame capture，对 `HUD_8_TEX_SLOTS` 与默认路径比较 GPU 时间、Draw Calls、批次数量和显存/材质变化。
  - **验收**：保留可复现采样配置和 CSV/截图；明确默认 3 槽、按需 8 槽，或调整默认值的决定。
  - **来源**：[场景测试说明](Legacy/场景测试说明.md)、[实施状态审计](Legacy/实施状态审计.md) T-S-03。

- [ ] **PERF-03 建立统一性能基线场景** (`node: M3`)
  - **范围**：UGUI、`UIMeshData`、`UIMeshDataX` Slim、Managed Job、Burst SoA、高并发压力（对应历史 T-PB-01..05）。
  - **行动**：复用 `PerfBaselineRunner`/`PrefabBatchSceneRunner`，补齐缺失的可打开场景或明确现有场景映射；统一 count、持续时间、`useSlim`、Job、纹理槽和设备标签。
  - **验收**：每个变体自动输出 CSV，包含 fill/merge/draw、主线程耗时、Draw Calls、GC 和批次数；可用于优化前后横向比较。
  - **来源**：[场景测试说明](Legacy/场景测试说明.md) §3.2、`HUDRenderTestSample/Assets/Scripts/HOT/PerfBaselineRunner.cs`。

- [ ] **JOB-01 量化 Burst SoA 与托管 Job 的收益** (`node: M3`)
  - **现状**：Burst 输出等价测试已存在，主渲染路径是否切换仍由性能数据决定。
  - **行动**：在统一基线场景中比较吞吐、主线程时间、Job 调度开销、GC 和不同 mesh 数量；检查低数量场景是否因转换成本反而变慢。
  - **验收**：给出切换阈值或保持兼容路径的理由；不以 `dotnet build` 代替 Unity runtime 性能证据。

## P2：TMP 能力与低风险维护

- [ ] **TMP-01 支持 fallback font atlas 与多材质子网格** (`node: M4`)
  - **现状**：当前 `UITMPText` 只保证单字体、主材质 atlas。
  - **行动**：定义子网格到纹理槽/批次的映射，覆盖 fallback 字形和 material reference 变化；保持 `UIText` 兼容。
  - **验收**：新增 EditMode 网格/索引测试、PlayMode 可见渲染测试和超槽分批测试；更新 [TextMeshProAdapter.md](../com.uidatarender/Documentation~/TextMeshProAdapter.md)。

- [ ] **TMP-02 支持 TMP sprite asset** (`node: M4`)
  - **行动**：明确 sprite atlas 的资源归属、纹理注册和材质选择；验证同一文本混合字形与 sprite 时的批次和 UV。
  - **验收**：有最小 Sample 场景、测试记录和不支持资源的明确报错/降级行为。

- [ ] **TMP-03 扩展并验证 SDF 材质效果** (`node: M4`)
  - **范围**：underlay、glow、bevel、masking、rich text material effect；只实现有明确需求和可验证材质契约的效果。
  - **验收**：每种效果有视觉回归场景或快照策略、性能数据和文档；不能支持的效果继续显式列为限制。

- [ ] **MAINT-01 处理低风险代码/环境项** (`node: M5`)
  - `UIMeshDataX.NEXT` 是否需要按测试或实例重置，避免未来诊断断言跨测试污染。
  - 若仍遇到 `License Client protocol 1.16.1 unsupported (max 1.10)`，升级 Unity Hub/Editor 或调整 LicensingClient，并把环境修复记录到验证文档。
  - 清理仍引用旧路径或旧性能结论的历史说明；历史文档保留，但当前入口统一指向 `Documentation~/roadmap.md` 和 `Documentation~/todo.md`。

历史完成索引 `HIST-001..004` 已写入 [roadmap.md](roadmap.md) 的 M0–M2 节点记录，并从活动队列清理。

