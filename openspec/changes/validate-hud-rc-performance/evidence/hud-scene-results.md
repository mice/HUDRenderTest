# PERF-04 HUD 场景实测与验收缺口

日期：2026-09-27。状态：**inconclusive/environment-limited，尚未形成 RC 性能结论**。本次增加 Sample 场景、采样器、自动化入口和测试，没有修改包 Runtime/Shader，不宣称性能改善。

## 验收状态解释

本报告已完成当前工作树上的场景配置、负载执行和原始证据收集，但运行环境是 Editor PlayMode/Mono/D3D11，且场景分配计数器校准失败；结果包含 Editor 调度噪声，也不是目标 Player/设备的稳定性证据。因此这里的 CPU 数值只能作为环境限定的诊断数据，不能直接推出“产品性能未达标”。

验收顺序已调整为：先在目标 Player/设备上通过环境有效性门，再判断 5 us/30x 和 HUD 场景预算。当前唯一目标验收机为 **Xiaomi 10**；设备字段和状态见[真机验收记录](device-acceptance-matrix.md)。r3 已包含真机运行结果，但报告的 commit/source manifest 未绑定精确候选提交，且 IL2CPP scoped allocation 不可用，因此状态调整为 `inconclusive/traceability-incomplete`，不触发性能例外确认。

真机复测至少要为 Xiaomi 10 记录型号/系统版本、Unity Player 构建标识、Graphics API、刷新率设置、温度/电源条件、负载配置、预热和采样次数，并保存原始逐帧数据。r3 已记录 Mi 10 / `umi` / Android 11 / 1080×2340 / 60 Hz、USB 供电、32.7°C、60 warmup + 180 sample frames/mode、三模式共 540 帧。报告路径为 [JSON](xiaomi10/r3-report/hud_rc_20260927_094916_659_bdbf6896.json) / [CSV](xiaomi10/r3-report/hud_rc_20260927_094916_659_bdbf6896.csv)。

### Xiaomi 10 真机结果（2026-09-27，IL2CPP Release r3）

- 环境：Unity 2022.3.53f1 / Player / IL2CPP / ARM64 / Vulkan / package 0.1.2；进程稳定，未复现 r1/r2 崩溃。
- 540 帧（Static、Partial、Burst 各 180）；frame interval median/p95 `33.5142/33.9611 ms`，主线程 median/p95 `33.5092/33.9472 ms`。
- regeneration（generation+merge）median/p95：`0.0007/1.2194 ms`；generation 最大 `26.6296 ms`，merge 最大 `7.8723 ms`；draw submit median/p95 `0.0082/0.0170 ms`。
- `scopedAllocationAvailable=false`（IL2CPP 兼容性限制），generation/merge/measured bytes 为 unavailable；不能据此宣称零 GC。
- 报告的 `commit`、`sourceStatus`、`sourceFingerprint` 均为 `unknown`，所以该轮只能作为未绑定候选提交的诊断数据，不能作为发布验收结论。

### Xiaomi 10 目标帧率复测（2026-09-27，IL2CPP Release r4）

r4 将 `targetFrameRate` 显式设为 60，其他配置保持 256 holders、3 quads、10% partial、30 帧 burst、60 warmup/180 sample。报告：[JSON](xiaomi10/r4-report/hud_rc_20260927_101018_649_0b5813e2.json)。帧间隔回到约 16.75 ms，证明 r3 的 33 ms 主要是 30 FPS 节奏，不是固定的 HUD CPU 耗时。

| 模式 | generation median/p95 | merge median/p95 | generation+merge median | 帧间隔 median/p95 |
|---|---:|---:|---:|---:|
| Static | 0.0002/0.0004 ms | 0.0002/0.0002 ms | 0.0004 ms | 16.750/16.984 ms |
| Partial（26/256） | 1.1126/1.1867 ms | 1.1833/1.8973 ms | 2.3046 ms | 16.755/16.902 ms |
| Burst（256，全量刷新帧） | 9.4791/10.4163 ms | 1.6119/2.0072 ms | 11.1100 ms | 16.753/17.009 ms |

结论：以整体 regeneration 预算 **1 ms** 计算，Partial 中位数约 2.30 ms，Burst 全量刷新约 11.11 ms，均超预算。Partial 的 generation 与 merge 都是主要贡献；Burst 主要由 generation 贡献。r4 的目标帧率为 60，但 Burst 仍可能占用大部分 16.67 ms 帧预算。该结论仍受 `commit/sourceFingerprint=unknown` 和 IL2CPP 分配计数器不可用限制，不能作为发布候选最终验收。

### Xiaomi 10 优化前基线（2026-09-27，提交 `9a6603a`）

基线 APK：`HUDRenderTest-IL2CPP-Release-baseline.apk`，SHA256 `6A72102DBBEED5D59D67CA8A9F52B23F15F3ED2BEB657E2DC8A6BE67844DB28F`。报告：[JSON](xiaomi10/baseline-report/hud_rc_20260927_120325_567_ee7a774c.json) / [CSV](xiaomi10/baseline-report/hud_rc_20260927_120325_567_ee7a774c.csv)，JSON SHA256 `06102E681580A2AB36848D9ECCA4604C9E68C663D9732DE5FD41C0CD3AA46019`。配置为 256 holders、3 quads、10% Partial、30 帧 Burst、60 warmup/180 sample、targetFrameRate=60、Vulkan/IL2CPP/ARM64/Release。

| 模式 | generation median/p95 | merge median/p95 | regeneration median | 帧间隔 median/p95 |
|---|---:|---:|---:|---:|
| Static | 0.0002/0.0004 ms | 0.0002/0.0002 ms | 0.0004 ms | 16.6929/17.0402 ms |
| Partial（26/256） | 1.0998/1.1615 ms | 0.3643/0.3970 ms | **1.4641 ms** | 16.7096/16.9348 ms |
| Burst（256，全量刷新帧） | 10.5761/10.6583 ms | 0.3611/0.3931 ms | **10.9372 ms** | 16.6944/16.7074 ms |

该基线仍超过整体 1 ms 预算：Partial 约 1.46 ms，Burst 约 10.94 ms。后续优化必须使用相同设备、配置、构建模式和报告字段进行前后对比；该版本只作为诊断基线，不代表验收通过。

### Xiaomi 10 CopyIndices 复测（2026-09-27）

当前版本 APK 使用本地 UGUI `VertexHelper.CopyIndices`，绕过 `VertexHelper.FillMesh` 的 scratch Mesh 往返；UGUI 原有 `Image`/`Graphic` 逻辑未修改。相同 Xiaomi 10、256 holders、3 quads、10% Partial、30 帧 Burst、targetFrameRate=60 配置下，结果为：

| 模式 | generation median | merge median | regeneration median | buildMesh median | mergeFill median |
|---|---:|---:|---:|---:|---:|
| Partial（26/256） | 0.2709 ms | 0.3686 ms | **0.6395 ms** | 0.2173 ms | 0.2198 ms |
| Burst（256，全量刷新帧） | 2.3537 ms | 0.4014 ms | **2.7551 ms** | 1.9018 ms | 0.2251 ms |

相对优化前基线（Partial `1.4641 ms`、Burst `10.9372 ms`），regeneration 中位数约下降 **56.3% / 74.8%**。当前 Partial 已低于 1 ms 瞬时预算；Burst 仍约 2.76 ms，尚未达到 1 ms。当前 Burst 的主要剩余成本是 `buildMesh`（约 1.90 ms），而不是 mergeFill（约 0.23 ms）。报告：[JSON](xiaomi10/copyindices-report/hud_rc_20260927_133926_498_ef23d132.json) / [CSV](xiaomi10/copyindices-report/hud_rc_20260927_133926_498_ef23d132.csv)。

### GC 热点定位（代码审查，2026-09-27）

当前 `UIData.Fill` 与 `UIData.MergeJob` 的托管分配来源已拆开：

1. `UIMeshData.FillToDrawData` / `UIMeshDataX.FillToDrawData` 主要向调用方传入的 `List<Vector3>`, `List<Vector4>`, `List<Color32>`, `List<int>` 写入；`Add`/`AddRange` 在容量不足时扩容并产生新的托管数组。方法内部没有 `new List`，因此 GC 热点是调用方列表生命周期和容量，而不是每个 `Fill` 调用必然分配。
2. `HolderBatchRenderer.Rebuild` 每次批次重建都新建四个输出 List，初始容量为 0。256 holders/3 quads 的单批输出约 3072 vertices、4608 indices，会触发多次数组扩容；旧数组随后成为 GC 垃圾。这是当前 `Fill` 路径最直接的 GC 来源。
3. `MergeBatcher.Plan` 每次重建为每个 holder 创建一个 `HashSet<int>`，并创建 batches/RenderBatch 容器；`RenderBatch.BuildLocalSlotMap` 又创建排序 List 和 Dictionary。即使只有一个纹理批次，这些容器仍在每次 `Rebuild` 分配。
4. `UIData.BurstMergeJob.Execute`、`BigXMergeJob.Execute` 和其它 MergeJob `Execute` 方法只操作 struct、托管数组引用或 NativeArray 元素；它们本身没有 `new List`/`new HashSet`/LINQ。若使用 `NativeArray`，其分配属于 Native 内存，需与 GC 分开记录。旧的 `UIMeshData.Create*` 中临时 List/`ToArray` 只发生在创建文本数据阶段，不是每帧 Fill 热点。

优先级：先复用并预分配四个合并输出 List，再缓存 `MergeBatcher` 的批次/slot map 并避免每次 `CollectSlots` 创建 HashSet；之后才评估把 `FillToDrawData` 改为直接写入预分配数组或 NativeArray。修复前不要把整帧 GC 数值归因给 `MergeJob.Execute`。

### GC 修复进展（2026-09-27）

编辑器复测已确认 `HolderBatchRenderer` 的四个合并输出 List 复用后，该路径的反复 List 创建/扩容 GC 已消除。随后继续完成批次规划容器复用：`MergeBatcher` 复用 holder slot scratch、批次池，`RenderBatch` 复用 UsedSlots、排序缓存和本地 slot map。这样 `Rebuild` 不再为每个 holder 创建临时 HashSet，也不再为每个批次创建新的 slot map 容器。待 Unity 编辑器释放项目锁后，需重新跑 EditMode/PlayMode 和 Xiaomi 10 r5，确认无行为回归并量化剩余 Mesh API/材质路径分配。

针对 `MergeBatcher.CollectSlots` / `RenderBatch.TryAdd` 的 18 KiB 采样热点，已进一步移除接口集合的 `foreach`：改用 `IReadOnlyList`/`IList` 索引循环，避免枚举器 boxing；批次 holder List 按输入规模预留容量，避免 `TryAdd` 期间扩容。若 18 KiB 仍存在，下一步应在 Profiler 中拆开 `MergeBatcher.Plan` 的 `Debug.LogWarning`、`Mesh.Set*` 和 Unity 引擎侧 Mesh 上传，而不是继续归因给 `MergeJob.Execute`。

补充修复：`RenderBatch.Reset`/`TryAdd` 不再调用 `HashSet.UnionWith`，改为对已预分配容量的 HashSet 逐项 `Add`；新建批次按 `maxSlots` 预留槽容量，池化批次保留该容量。需用编辑器 Profiler 复测 `UIData.MergeJob` 是否低于原 18 KiB。

另修复 `HolderBatchRenderer.Rebuild` 内 `Stopwatch.StartNew()` 的逐次对象分配，改用 `Stopwatch.GetTimestamp()` 值类型计时；Fill 毫秒统计口径保持不变。

### Graphics.DoMeshGeneration CPU 分解（2026-09-27）

`Graphics.DoMeshGeneration` 的高 CPU 已定位到 UGUI 几何重复转换链：`UIImage.OnPopulateMesh` 先调用 `Image.OnPopulateMesh`，随后 `UIMeshData.FillVertex` 再逐顶点读取 `VertexHelper`；`VertexHelperUtils.CopyIndices` 还会调用 `VertexHelper.FillMesh` 写入 scratch Mesh，再通过 `GetTriangles` 读回索引。已加入四个细粒度 marker：`UIData.UIImage.BasePopulateMesh`、`UIData.UIImage.CustomPopulateMesh`、`UIData.VertexHelper.CopyVertices`、`UIData.VertexHelper.FillMesh`。下一轮编辑器采样据此决定是绕过基础 Image 生成，还是直接从 VertexHelper 索引路径读取，避免在没有证据前改动 UGUI 行为。

`VertexHelper.FillMesh` 内部无法继续拆分；对当前固定四边形（4 vertices / 6 indices）路径已加入 `QuadIndicesFastPath`，直接写入标准 `[0,1,2,2,3,0]` 索引并绕过 scratch Mesh。非四边形或非六索引几何仍使用原 `FillMesh` 回退路径，因此复杂 UI 行为保持不变。需用 Xiaomi 10/Editor 基线复测确认 5.x ms 是否显著下降。

### HudRcPerformanceRunner.Update 分解（2026-09-27）

`AdvanceFrame` 已增加逐帧阶段字段：`runnerBookkeepingMs`（pending sample、模式切换和状态维护）、`holderSelectionMs`（索引选择、宽度计算、RectTransform 尺寸写入）、`buildMeshMs`（所有 `holders[index].BuildMesh(draws)` 累计）。原 `generationMs` 现在从 selection 阶段开始单独记录，`mergeMs`、`mergePlanMs`、`mergeFillMs`、`meshUploadMs` 和 `drawSubmitMs` 保持不变。后续基线报告可区分 Runner 自身开销与 UIImage/VertexHelper/合并路径开销。

同时加入 Unity ProfilerMarker：`HUDRC.Runner.Bookkeeping`、`HUDRC.Runner.HolderSelection`、`HUDRC.Runner.BuildMesh`、`HUDRC.Runner.Rebuild`、`HUDRC.Runner.Draw`。`BuildMesh` 包在每个 holder 调用周围，可展开查看单次和累计成本；JSON 字段用于跨设备汇总，Profiler marker 用于编辑器现场定位。

### Xiaomi 10 真机接线结果（2026-09-27，Development APK）

- 环境：Android 11 / Unity 2022.3.53f1 / Player / Mono / Vulkan / 256 holders / 3 quads per holder / 60 Hz。
- Partial：regeneration median/p95 `21.6262/27.3133 ms`；主线程 p95 `33.9655 ms`。
- Burst refresh frames：regeneration median/p95 `81.1059/92.4813 ms`；主线程 p95 `94.6613 ms`，共 6 个集中刷新样本。
- Static/Burst 非刷新帧 regeneration p95 约 `0.0036/0.0044 ms`。
- 该 Development APK 仅用于确认场景运行和报告导出；不作为真实性能数据。Release/IL2CPP APK 当前启动崩溃，尚未进入性能采样。

## 重放与来源

打开 `HUDRenderTestSample/Assets/Scenes/Scene_Perf_HUD_RC.unity`，进入 Play Mode 可自动完成三模式并导出。组件提供 Run / Reset / Export 菜单。批处理在仓库根目录执行：

```powershell
.\Tools\Invoke-HudRcValidation.ps1 -Action PlayMode
.\Tools\Invoke-HudRcValidation.ps1 -Action Measure -HolderCount 64 -SampleFrames 600
.\Tools\Invoke-HudRcValidation.ps1 -Action Measure -HolderCount 64 -SampleFrames 600 -UseSlim
.\Tools\Invoke-HudRcValidation.ps1 -Action Measure -HolderCount 256 -SampleFrames 600
.\Tools\Invoke-HudRcValidation.ps1 -Action Measure -HolderCount 256 -SampleFrames 600 -UseSlim
```

环境：Unity 2022.3.53f1 / Windows 11 10.0.26200 / Intel i5-1240P / Editor PlayMode / Mono / Direct3D11；有图形设备的 batchmode，未使用 nographics。不是设备 Player 或 GPU 性能保证。每模式预热 60 帧、采样 600 帧；每报告 1800 帧，集中刷新子集 20 帧。seed=12345，3 quad/holder，局部更新 ceil(10%)，每 30 帧全量刷新，vSync=0、targetFrameRate=-1。

64 holders：768 顶点/1152 索引，局部每帧更新 7；256 holders：3072 顶点/4608 索引，局部每帧更新 26。两路径均实际合为 1 批并调用生产 DrawMesh。用无 sprite 色条代表简单 HUD，不覆盖 TMP、复杂布局、多纹理槽或 Job。

来源 HEAD `bb40e52f5a46e7f748c3e2424cf84a9b9eb9a7ad`，工作树有未提交内容，UPM 版本 0.1.2 unreleased；**不是该 HEAD 的 clean candidate 验证**。每次报告保存源文件 SHA256 清单摘要。64 两次和 PlayMode 为 `5C6E7AB929274E5BB68626A0B7E50C13D959E733C23234B6B9FFE5CE351516BA`，256 两次为 `D3E9073A4264B81E5316BB6CABDC09F2EFB5CF2C78FEC4126FA39961BA7C3CDA`；期间测试治理同步仅规范化 Sample 测试文件换行，git diff 无语义变化；完整清单保留以供核对。后续另修正包测试的分配计数方法并补跑 EditMode，源码及哈希见证据包；包 Runtime 未变。

## 原始证据

[hud-rc-evidence.zip](hud-rc-evidence.zip) 包含四组 JSON/CSV、Unity 日志、逐文件 SHA256 清单、26/26 PlayMode XML/日志、补跑的 119/119 EditMode XML/日志和测试源码/哈希，以及布局/测试治理报告。压缩包 SHA256：`A747FC5F2F84197197B9C915D80F578E02C57ED5F7D865B6141BB5170A3111B8`。

| 配置 | 报告文件（JSON/CSV 同名） |
|---|---|
| 64 / UIMeshData | hud_rc_20260927_073237_326_f5127469 |
| 64 / UIMeshDataX | hud_rc_20260927_073307_238_d93e09d7 |
| 256 / UIMeshData | hud_rc_20260927_073349_787_f6704d3e |
| 256 / UIMeshDataX | hud_rc_20260927_073418_356_4824b57a |

早先未校准版本的三份试跑留在本机 Logs/PerfProbe，不纳入验收数据；其中 scoped bytes=0 已被判定不可用，不能引用为零 GC。

## CPU 结果

单位 ms，nearest-rank median/p95。regeneration=generation+merge，不包含 draw submission；原始数据另存 draw submission。静态约 0.0001–0.0002 ms 是计时开销，不是执行了 regeneration。集中刷新必须单列，不能采用被 580 个空更新帧稀释的全帧 p95。

| 数量/路径 | 模式 | regeneration median/p95 | Main Thread median/p95 | 帧间隔 p95 |
|---|---|---:|---:|---:|
| 64/Data | Static | 0.0002/0.0002 | 0.0914/0.3131 | 0.3239 |
| 64/Data | Partial | 0.8473/2.9656 | 1.2849/3.5463 | 3.6210 |
| 64/Data | Burst 刷新帧 | 4.9734/6.7482 | 5.4116/7.0886 | 7.2559 |
| 64/X | Static | 0.0001/0.0002 | 0.1025/0.2429 | 0.2447 |
| 64/X | Partial | 0.9558/3.0429 | 1.2871/3.5198 | 3.6117 |
| 64/X | Burst 刷新帧 | 4.4659/6.2162 | 4.6832/6.5545 | 6.6710 |
| 256/Data | Static | 0.0001/0.0002 | 0.0923/0.1699 | 0.1716 |
| 256/Data | Partial | 2.0562/3.9182 | 2.4805/4.4418 | 4.4888 |
| 256/Data | Burst 刷新帧 | 15.7059/18.1429 | 15.9501/18.3793 | 18.5498 |
| 256/X | Static | 0.0001/0.0002 | 0.0873/0.1565 | 0.1522 |
| 256/X | Partial | 2.9148/5.6389 | 3.4917/6.0259 | 6.1269 |
| 256/X | Burst 刷新帧 | 16.4369/18.3420 | 16.7610/18.7642 | 19.0150 |

仅一轮/配置、20 个集中刷新样本，有 Editor 调度噪声，不能据此证明 Slim 更快或跨设备稳定。旧 1 ms regeneration p95 在这两种规模的持续局部更新和集中刷新均未达到。256 集中刷新已超出 60 FPS 的整帧 16.67 ms，不适合作为其余系统零成本的假设基础。

## 分配归因与缺口

新增启动校准：分配 4096-byte 数组并 KeepAlive，检查 `GC.GetAllocatedBytesForCurrentThread` 增量。当前 Editor Mono 返回零，校准失败；全部 scoped generation/merge/total 字段明确 `-1` / CSV `unavailable`。这些字段不构成零分配证据。

整帧 ProfilerRecorder 有效。Static 通常 40 B/帧，最大 81 B；局部更新与集中刷新帧的最小分配分别为：64/Data 93,497 B、64/X 124,217 B、256/Data 368,761 B、256/X 491,641 B。它包含编辑器和其他系统，不能精确归到 extraction。局部与全量更新的分配接近、随总 holder 数增加，与 `HolderBatchRenderer.Rebuild` 每次新建顶点/UV/颜色/索引 List、`MergeBatcher.Plan` 每次新建批次和 slot 集合的源码行为一致；这是归因线索，不是隔离测量结论。

关联 [PERF-01 原始归档](../../archive/2026-09-27-reassess-public-ugui-extraction-performance/evidence/perf-01-benchmark.md)：FillData2 median/p95 5.380/7.759 us，FillData3 6.327/9.182 us，原归档报告 119/119 EditMode 和 warm allocation 0 B。相对原修改 UGUI 的 0.164/0.183 us，median 约 32.8x/34.6x，未达 5 us/30x 参考。上述是历史结果，归档原文保持不变；本轮在发现计数器问题后补跑，见下文。由于旧测试也使用同一线程计数 API 且无正向校准，**原 0 B 结论不能直接沿用，现已通过独立事件计数补证**；PlayMode 的校准失败不直接证明 EditMode 也失败，但足以禁止未经核验引用。

### extraction 独立分配复核（已完成）

根据 [Unity GC.Alloc 当前线程采集示例](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html)，UT_MESH_017/018 改用 `ProfilerRecorder` / Internal / GC.Alloc / CollectOnlyOnCurrentThread，不启用逐帧合并。先观察 4096-byte 数组的正向分配事件，再 Reset/Start 测目标操作、Stop 读事件数，检查 recorder 有效且未饱和。断言与日志输出在采样外；CPU 计时与事件采集分开，包 Runtime 未改。

`Invoke-LocalValidation.ps1 -SkipPlayMode` 新一轮 **119/119 Passed**。UT_MESH_017：FillData2/3 各 10,000 次，预热后均 **0 GC.Alloc 事件**，正向控制通过；这支持该固定 fixture 的零托管分配，不是整场景零 GC 或对所有输入的保证。UT_MESH_018 同样通过正向校准，新的 CPU median/p95：FillData2 **5.025/6.423 us**，FillData3 **6.083/7.529 us**；median 相对旧 baseline 约 **30.6x/33.2x**，仍未达原 5 us/30x 参考。没有优化提交，不把数值波动称为性能改善。

场景 scoped bytes 仍保留 unavailable；整帧分配与 extraction 零事件分别陈述，不用一种口径替代另一种。

## 使用预算草案（未批准，不能替代环境复测）

- extraction 固定四顶点/六索引 fixture 保持正向校准后的 0 分配事件；CPU 复测预算草案 median < 10 us、p95 < 15 us。旧 5 us/30x 差距须用户明确接受。
- 限于本机/此简单 HUD 配置，可将 64 holders、7 个/帧更新的 regeneration p95 4 ms 和集中刷新 8 ms 作为下一轮复测预算草案；不是新发布门槛或已获例外。
- 256 holders 仅作为压力点保留；不接受一次全量刷新为 60 FPS 预算内操作。要么摊帧/降负载，要么优化再测；本 change 没有擅自改包 Runtime。
- 64/X 相比普通路径的整帧分配更高；不能仅凭 Slim 名称推断更低总成本。任何路径选择需结合目标 Player 与分配证据。
- 重开条件：同环境复测超出获批预算、目标硬件/Player/backend 改变、复杂度/更新比例增加、Runtime/UGUI/Shader/Job 改动，或合批分配造成 GC 尖峰。

## 验收与交接

新增 UT_PERF_012–015；最终 PlayMode 26/26（PlayMode-20260927_153137_078.xml），验证场景绑定、真实几何、三模式和双路径、Run/Reset 清理/恢复、报告快照及刷新子集。第一次场景加载测试因未等待下一帧失败，修正时序后通过；没有删测试或降低几何断言。包布局通过，测试治理 145 tests/145 records，OpenSpec strict 通过。包 Runtime/Shader/Job 未变，PERF-02、PERF-03、JOB-01 和覆盖率任务均继续保留。

PERF-04 保留活动：r3 已解决 IL2CPP 启动崩溃并完成 Xiaomi 10 采样，但因候选提交追踪缺失及 scoped allocation 不可用，状态为 `inconclusive/traceability-incomplete`。下一步需用 exact candidate/source manifest 重建并复测；只有环境门完整通过后，才判断性能门槛或提交预算例外。M5 RC 队列仍须 exact candidate clean validation、覆盖率、真实 Jenkins run 和最终发布确认；本次不创建任何 tag。
