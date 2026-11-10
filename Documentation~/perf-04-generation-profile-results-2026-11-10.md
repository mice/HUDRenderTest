# PERF-04：代表性 Rotating generation 成本量化（2026-11-10）

状态：功能、Unity 验证、八轮设备计时和独立 Profiler GC 验收均完成，主代理独立重跑并核对实际证据。变更：[profile-representative-hud-generation](../openspec/changes/archive/2026-11-11-profile-representative-hud-generation/proposal.md)。归属 PERF-04 / M5，目标仍为 0.1.2 Unreleased；不关闭整个节点或发布门禁。

## 诊断范围

复用既有 `BuildMeshDiagnostics` 和值类型聚合，以默认关闭的 `configuration.profileBucketGeneration` 采集实际完成 holder 的阶段。保留 `disableBuildMeshDiagnostics=true`；Rotating 新开关独立启用阶段采样。TMP 只增加 Canvas 更新、强制网格更新、提取与变换四个边界，没有逐字形计时、几何缓存或跳过重解析。正常调用顺序保持不变。

每帧 JSON 记录 `profiledHolders`、`tmpStageSamples`、阶段可用性及实际 Screen/camera 像素尺寸。关闭或零完成帧使用 count=0 / unavailable / -1，null、异常和后续关闭不会留下旧 TMP 时序。阶段采样只累计数值，不在热路径新建集合或写文件。

用户要求 GC 在验收时通过 Unity Profiler 检查，以减少运行时代码。本轮没有增加运行时 GC 计数器或字节接口，既有可选事件诊断保持兼容，IL2CPP 对不安全字节 API 的防护保留。Profiler GC 使用本地临时 Editor 工具；工具不进入 Release 构建输入，Profiler 耗时不进入 Release 对照。

## Unity 验证

| 验证 | 结果 |
|---|---:|
| 原项目 TMP 提取相关 EditMode | 9/9 |
| 原项目 D3D11 runner PlayMode | 12/12 |
| 原项目完整 EditMode | 205/205 |
| 原项目完整 D3D11 PlayMode | 65/65 |
| stock 相关 EditMode | 9/9 |
| stock D3D11 runner PlayMode | 12/12 |
| 治理记录 | 267/267 |
| UIDataRender 行覆盖率 | 90.2%（3413/3782，门槛 70%） |
| Package layout / OpenSpec strict | 通过 |

均无失败或跳过。stock 实际解析为 UGUI 1.0.0 BuiltIn，VertexHelper SHA-256 为 `1623D631B78018C8171C9B13CD5372FDBC0CA53AAC4A36A65B10027A0DF37DD1`，没有定制 CopyIndices。本轮 stock 仅相关功能验证，不是 stock 场景性能矩阵。

UT_UI_012 覆盖 TMP 改变/空文本、null、异常及关闭后清理；UT_PERF_024 验证 Rotating 开关前后的请求/完成、最终几何、阶段计数和尺寸。完整测试后仅补了一行报告说明字符串，明确 Rotating 开关与 legacy 诊断开关的关系；无执行逻辑变化，相关测试适用。当前工作树测试和覆盖率不替代精确 RC 提交验收。

## 冻结来源与协议

唯一 Release APK 为 `Builds/HudRc/GenerationProfile/generation-release.apk`，14,885,316 bytes，SHA-256 `8F81956D62840A04DF6061BF68311A901B7CE02296D97F66F83C9212B11ADC31`；Build GUID `02fa45ff57f343108eebb7c861e3d882`。source fingerprint `9DF8B922F3B673244841673D69BDE27059B3D3263918398EB467990D07D0FFE3` 已在 APK level0 中核对，1173 个输入在构建后全量读回一致，临时 Profiler helper 不在输入中。

实际 custom UGUI VertexHelper SHA-256 为 `40EE6F876DEEB7BF8E4084B5B1920E087914D3C1F6E308A7D8759951A5479AE9`。不改原项目 manifest/lock，不将 dirty 工作树测量称为 RC。

设备 Mi 10（791d02af），IL2CPP ARM64/Vulkan、60 FPS，256 holders、Sprite3/TMP6、三纹理、Slim/query reuse、Rotating。沿用历史正式横屏 2340×1080，并强制每个 Unity 样本的 Screen/camera 四个尺寸字段与此一致。每模式 60 预热 / 3000 正式样本 / 2 排空，共每轮 9006 帧。

同一 APK 的八轮固定顺序：off / on / on / off / on / off / off / on，共 72,048 帧。只切换 `profileBucketGeneration`，不开分配事件计数器、Profiler、逐 quad 诊断或截图。每轮安装后、启动前设置方向，启动后和采样后核对真实 display；采样中不纠正方向。前置 APK 路径读取曾在任何设备修改或正式采样前失败，记录后修复预检，未筛选或替换计时结果。

请求/完成拓扑、三模式最终几何、资源/依赖和 APK/来源身份均验收一致。p95/p99 使用 nearest rank，两帧 Burst 成本先按真实窗口逐帧求和再计算分布。每个开关四次独立运行，不把相邻帧当作独立实验重复。

先前两组都在第四轮因主机 ADB 读回失败中断，前三轮完整结果及第四轮拒绝证据均隔离保存，不混入本组。最终协议统一为当前服务对应的 Unity SDK ADB 32.0.0 客户端；stdout 与 stderr 分离，幂等只读命令最多三次、间隔两秒且记录失败/恢复，安装/启动/停止/请求/设置修改不自动重发，不结束共享服务或其他项目。Unity 启停与手机计时分开。正式组八轮实际均没有日志记录的读失败/恢复；这不证明 daemon 从未发生短暂变化，也不证明客户端混用是旧故障的唯一原因。

## 计时结果

八轮均完整 9006 帧，共 **72,048 帧**。单位 ms；Burst 刷新只统计实际完成 holder 的普通帧，排空单独验证并从耗时分布排除。

| 顺序 | Partial update p95 | Burst 刷新 median | Burst 刷新 p95 | 两帧累计 p95 |
|---|---:|---:|---:|---:|
| 01 off | 5.0870 | 9.3124 | 10.0207 | 19.2832 |
| 02 on | 6.8642 | 12.6754 | 14.9155 | 27.0541 |
| 03 on | 6.5471 | 12.7133 | 14.1411 | 26.0774 |
| 04 off | 5.0889 | 9.39225 | 11.0740 | 20.2339 |
| 05 on | 6.8307 | 12.7274 | 14.8998 | 27.0821 |
| 06 off | 5.0796 | 9.2381 | 10.0379 | 19.2081 |
| 07 off | 4.8663 | 9.15885 | 9.8092 | 18.8042 |
| 08 on | 6.9088 | 12.8676 | 15.0504 | 27.1380 |

以四次独立运行统计量的均值比较，正常路径 Burst update median/p95 为 **9.27540/10.23545 ms**，开启诊断为 **12.745925/14.75170 ms**，分别增加 **3.470525 ms（37.42%）/4.51625 ms（44.12%）**。两帧累计 p95 为 `19.38235→26.83790 ms`（增加 38.47%）；Partial median `3.0697875→4.005325 ms`（增加 30.48%），p95 `5.03045→6.78770 ms`。

正常路径 generation median 的运行均值为 **8.11815 ms**，merge median 为 **1.15280 ms**；不同分布的 median 不能相加重建 update median。四次 off 的 Burst 已完成刷新帧逐帧求和后，generation 占 update 总时长约 **87.55%**，仍是主要剩余部分，但这个 generation 包含提交/调度/发布。开启探针的变化明显，且 merge 的 median 也发生变化，因此开关差值包含测量和系统响应，不能全部归为某个计时调用的固有开销。正常路径证据以 off 为准；本轮没有运行时优化前后对照。

### 开启探针后的阶段定位

下表是四次 on 运行各自阶段 median/p95 的均值，均为**含探针扰动的阶段成本**。占比按每次运行的有效帧阶段总和/同帧 generation 总和计算，再给出范围；不是 median 相除，也不是正常路径比例。

| 阶段 | median 均值 | p95 均值 | 占观测 generation 累计时间范围 |
|---|---:|---:|---:|
| TMP ForceMeshUpdate | 4.15740 | 4.793075 | 34.39–35.15% |
| TMP 提取 Fill | 1.6970125 | 2.02205 | 13.25–14.62% |
| TMP Canvas 更新 | 0.1839125 | 0.232825 | 1.54–1.73% |
| TMP 变换 | 0.31485 | 0.354025 | 2.52–2.67% |
| Image SimplePath（含其子阶段） | 2.2237375 | 2.50955 | 17.91–18.26% |
| BuildMesh 父级 | 10.8831125 | 12.19245 | 不与子项重复相加 |
| DoGenerate 父级 | 9.6163375 | 10.79345 | 不与 TMP/Image 子项重复相加 |

已记录叶阶段中 ForceMeshUpdate 最大，TMP 提取也保留可见 CPU 成本；零分配不等于零提取成本。由于总探针扰动达约 37–44%，这只能作为仪器化路径的后续定位依据，不能据此断言正常路径中各阶段的精确成本、占比或某项优化的收益。下一次优化前应先用更低扰动的方法复核目标，而不继续默认开启全部细分时钟。

`generationMs = updateCpuMs - mergeMs` 包含提交、调度和发布，不能直接称为 TMP 生成耗时。`BuildMesh` 包含 `DoGenerate`，后者包含 TMP 四阶段；Image 既有阶段也有嵌套关系。只在单帧内按相容边界计算余量，再汇总分布，不相减 percentile 或重复相加父子阶段。

八轮电池温度端点为 30.0–31.0°C，thermal status 0、无 override；这些只是端点，不证明全程无降频或后台干扰。主机轮间时间间隔约 2.46–2.66 秒，主机/设备时钟未专门校准，不能把两者 UTC 差当作精确端点延迟。四次/开关的重复不支持总体显著性或完整帧/GPU/FPS 收益声明。

## 独立 Profiler GC 验收

使用标准 Unity Profiler，Deep Profiling 关闭、GCAlloc 调用栈开启；原项目 Editor PlayMode / Mono，受控 `Camera.onPreCull` 回调通过缓存委托驱动真实 runner。诊断开关为 off，256 holders、同一 Sprite3/TMP6 固定输出长度负载。Camera RT 为 2340×1080，实际 Editor Screen 为 640×480；这不是同屏幕环境的设备性能对照，也不将 Editor 字节结果移用于 Android。

一次连续 BeginRun 完成三个模式，每模式仍 60 预热 / 300 样本 / 2 排空。为控制工具资源，按每模式十二个 30-step 加末尾 2-step 保存为 39 个窗口；窗口间暂停驱动，控制使用独立真实帧，存在物理 Unity frame 间隙，故不用于 CPU/FPS/实际一帧完成延迟结论。只有 pre-call modeFrame 60..361 的正式样本步包入 HUD scope，所有准备、预热、模式间几何哈希、导出和清理排除。

| Profiler 验收 | 实际证据 |
|---|---|
| 唯一正式 HUD scope | 906，Static/Partial/Burst 各 302（含排空） |
| 独立正控 | 39/39，每窗已知 4096 B 数组 |
| 正控原始 metadata | `Size / Int64 / Bytes / 4128`，均有 PositiveControl/BeforeCamera 解析栈 |
| 接受线程 | 同一 Main Thread，threadId 21040 |
| 正式 HUD 分配 | **0 次事件 / 0 B**，字节可用 |
| 数据读取方式 | 暂停录制后的 native live RawFrameDataView，逐窗保存原始 raw/data 与 live JSON 哈希 |
| 离线重放 | 未验证完整重读；`readMode=live-buffer`、`captureSaved=true`、`loadSucceeded=false`、`offlineReplayVerified=false` |
| 其他线程 | 未检查，不作全线程或全应用声明 |

scope 外主线程另有 **136,538** 个 GC 样本，包含准备、模式间校验及导出等诊断工作。源码与 Mono IL 核对发现 `CaptureBucketFinalState` 使用的 `BinaryWriter.Write(Single)` 会创建临时 byte 数组，不能把它归为正式采样帧的 generation 分配，也不能把整轮应用称为零 GC。

早期捕获受默认历史截断、首帧录制状态和大窗口 native view 的内存占用影响，均未作为通过数据。采用更小窗口和每窗正控后，保持原有 906 个报告帧完整性，工具仍保留 3 GiB 自有 Editor 限额；既不提高限额，也不降低样本/正控要求。native history 仅记录 requested 64 和实际保留范围（每窗 9–42 帧），没有 getter 证明容量就不虚称实际容量为 64。

最终 `gc-profiler-live-30` 的预注入 1173 项输入清单与 Release source fingerprint 完全相同，结束读回零差异；完整 helper 源快照 SHA-256 为 `208CF36D3F6609E6598161410FC9B16D1A3EFA8A8EB8958891E965FF66342FCA`。HOT/UIDataRender/UGui.Extends 三个实际加载程序集均保留副本与哈希。场景序列化的旧 fingerprint 明确不作当次来源证明。主代理独立核对 39 个原始窗、正控 metadata/栈、906 个 frame 映射及资源/程序集哈希，重新运行严格分析通过；临时 helper 与 meta 均已清理。

上述零分配仅覆盖该 Editor/Mono、主线程、预热后固定容量与指定样本 HUD scope；目标 IL2CPP scoped 字节仍不可用，Managed 输出长度变化、扩容等其他输入没有据此承诺 0 B。

## 恢复与剩余门禁

最终恢复记录 UTC 为 `2026-11-09T18:05:25.7203948Z`。主代理在 Profiler 验收结束后再次直接读取设备：原安装 APK SHA-256 `73C94BEAC9721C4685C6C0AFE0EBEB49528114B7379A2DFCDF8400CB261B2D22` 与备份一致，九项设置逐项相同，原请求不存在状态恢复、原文件名齐全、应用停止；其他项目的 Unity 进程未操作。

本轮只量化 generation 构成与探针开销，不声明优化收益、完整帧/GPU/FPS 改善，不调整原预算，不关闭 PERF-04、M5 或发布门禁。后续仍需 stock 场景性能、目标设备 scoped 字节能力及精确 RC/覆盖率绑定；Jenkins 不在本轮执行范围。

原始证据、APK、XML、覆盖率、Profiler binary 和本地工具仅保留 `TestResults/GenerationProfile/` 与 `Builds/HudRc/GenerationProfile/`；最终文档和源代码入库范围不包含这些中间产物。旧 `TestResults/TmpScratch/`、`TestResults/Perf04/` 证据保留。

本地证据入口：`analysis.json`（严格八轮）、`root-acceptance.json`（主代理独立流式核对）、`gc-profiler-live-30/gc-analysis.json`（Profiler scope/正控）、`root-device-readback/readback.json`（实际设备复核）、`final-source-readback.json`（构建后仅文档差异）、`evidence.sha256`（全部原始/失败/脚本封存）。旧轮 393+190 个证据文件逐项重算均未改变。

完成出口整体重读活动 TODO：PERF-04/GOV-002/MAINT-01 继续 M5，PERF-02/03/JOB-01 继续 M3，TMP-01..03 继续 M4，九项全部 carried-forward；GOV-002 仍 blocked/unverified，JOB-01 按预算触发。下一步补 stock 场景性能，并在实际优化前以低扰动方式复核 TMP mesh regeneration 的目标；不新增/复用 TODO ID，不将本轮子项归档成整个性能或发布通过。
