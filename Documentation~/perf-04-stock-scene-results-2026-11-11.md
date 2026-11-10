# PERF-04：真实 stock UGUI 代表性场景对照（2026-11-11）

状态：来源、stock 相关功能、Release 构建、八轮设备计时和独立 stock Profiler scoped 字节均已验收。前两次 Profiler 拒绝证据保留，第三次完整通过。变更：[measure-stock-hud-scene-performance](../openspec/changes/archive/2026-11-11-measure-stock-hud-scene-performance/proposal.md)。归属 PERF-04 / M5 / 0.1.2 Unreleased，不关闭节点，不形成 RC 或发布通过结论。

## 本轮范围与来源

本轮没有修改生产运行代码或新增 GC 统计机制。复用现有 Sprite/TMP/多纹理代表性场景、Release 构建流程和唯一的 stock 验证工作树 `D:/workspace/HUDRenderPackageValidation`。Terra 适配本地 Profiler 工具，Sol 审查协议和来源并分析原始帧，Luna 执行；主代理独立核对来源、APK 和验收证据。原 Sample 的 manifest/lock 没有修改。

stock 准备后，1248 个普通输入与原仓库逐字节一致；仅 manifest/lock 中的 UGUI 条目和仅 Editor 的来源探针及其 meta 共四个路径存在允许差异。所有非 UGUI 依赖，包括两个 MCP 工具依赖，都保留一致。构建后没有新增输入、场景或其他普通文件变化。custom 与 SDK stock 的 86 个运行代码、shader、assembly definition 文件归一换行后仅 VertexHelper.cs 不同，差异为新增 CopyIndices 方法；原 FillMesh 未改。

| 身份 | custom A | stock B |
|---|---|---|
| APK SHA-256 | `8F81956D62840A04DF6061BF68311A901B7CE02296D97F66F83C9212B11ADC31` | `5CE722653B6469E25FA69E077681493CA67664B281B539688613BABDD96750F5` |
| APK bytes | 14,885,316 | 14,882,866 |
| Build GUID | `02fa45ff57f343108eebb7c861e3d882` | `d502fd3297aa47bf89a83a1c8279d52f` |
| Source fingerprint | `9DF8B922F3B673244841673D69BDE27059B3D3263918398EB467990D07D0FFE3` | `6D5403D79F8302EC35EC418CE32D5A3B95B6600E774D86D0FC88E603FD98B78D` |
| 构建输入数 | 1173 | 897 |
| VertexHelper SHA-256 | `40EE6F876DEEB7BF8E4084B5B1920E087914D3C1F6E308A7D8759951A5479AE9` | `1623D631B78018C8171C9B13CD5372FDBC0CA53AAC4A36A65B10027A0DF37DD1` |
| CopyIndices 实例方法 | 有 | 无 |

custom 复用上一轮冻结 APK；复用前完整旧输入清单对原仓库仅 README/CHANGELOG 两项文档差异，运行代码、构建流程、资源和设置一致。两份冻结清单相较，stock 移除 278 个定制 UGUI 文件，增加两个 Editor 探针文件；共有输入的差异仅这两份文档与 manifest/lock。随后原仓库治理同步与结论更新另有文档换行、索引和说明变化，单独记录，不改冻结 stock 输入或 APK。整体来源指纹不同符合实际依赖差异，不能要求相同后再比较。

主代理直接从两份 APK 读取 boot.config Build GUID，并在 level0 核对来源指纹与 UGUI 源码哈希，确认均为 ARM64。stock 的 897 个构建输入在构建后全量读回一致，临时 Profiler helper 不在输入中。新生成的 Editor 来源探针从实际加载程序集解析为 BuiltIn UGUI 1.0.0，无定制 CopyIndices；保留的 IL2CPP C++ 输出确认 `CopyIndicesForExtraction → VertexHelperUtils.CopyIndices → FillMesh/GetTriangles`。本轮使用真实 stock 依赖，`forceUguiFallback=false`。

## Unity 验证

| 本轮执行 | 结果 |
|---|---:|
| stock TMP 提取与 VertexHelper EditMode | 22/22 |
| stock runner D3D11 PlayMode | 12/12 |
| 测试治理 | 267/267 |
| Package layout / OpenSpec strict | 通过 |

保留的测试无失败或跳过。首次 EditMode 实际报告 22 项通过，但本地脚本误设预期为 9，执行代理随后误删该次 XML/log；该次不作为留存证据。修正为 TMP 9 项加 VertexHelper 13 项后重跑，保留的 22/22 和后续 12/12 XML/log 已由主代理独立验证。

本轮没有生产代码变化，因此没有重复上一轮原项目 205/205 EditMode、65/65 D3D11 PlayMode 或 90.2%（3413/3782）UIDataRender 覆盖率矩阵。这些属于历史工作树证据，不替代精确候选提交的回归与覆盖率。

## 设备协议

2026-11-10 注册 A/B/B/A/B/A/A/B 固定顺序。Mi 10（791d02af）、Unity 2022.3.53f1、IL2CPP ARM64/Vulkan Release、60 FPS；256 holders、Sprite3/TMP6、三纹理、Slim/query reuse、Rotating、seed 12345。两边全部 probes off：`profileBucketGeneration=false`、`allocationEvents=false`、`profileQuadWrites=false`、`disableBuildMeshDiagnostics=true`。

沿用已确认的历史正式横屏 2340×1080；每轮安装后、启动前设置方向，核对 during/after 实际 display，并要求每个 Unity 样本的 Screen/camera 四个尺寸字段均匹配。每模式 60 warmup、3000 正式样本和两帧 drain，每轮 9006 帧，完整八轮应为 72,048 帧。

冻结每个变体的 APK、清单、请求、来源和 host 脚本哈希。要求资源、请求/完成拓扑、版本、延迟、最终 holder 状态和 canonical 几何一致。分位数为 nearest rank，先按真实两帧窗口求和再计算 Burst 累计成本；每变体四次独立运行，帧不作为独立实验重复。差值方向统一为 stock minus custom。

本任务 Unity 启停、Profiler 和构建均避开正式手机采样：功能验证、构建及前两次 Profiler 在采样前，第三次 Profiler 在整组设备采样和恢复结束后。SDK ADB 32 客户端分离 stdout/stderr；幂等只读最多三次、间隔两秒并记录失败/恢复，mutation 仅一次，不结束共享服务。备份实际开始时的 APK、请求、九项设置和文件名，结束时逐阶段尽力恢复并聚合错误；仅完整读回通过才写成功记录，最终停止应用。端点温控无法排除 DVFS 和其他背景影响。

## 设备计时与恢复结果

八轮各 9006 帧，共 **72,048 帧**，无拒绝或替换轮次。冻结分析器与主代理独立脚本均通过；后者另逐行核对两份 CSV 的帧身份、请求/完成和耗时与 JSON 一致。跨八轮的资源、确定性帧字段、请求/完成轨迹、最终状态与网格校验值一致；阶段计时保持 unavailable、采样计数为零，不能把这些值解释为零成本。

单位 ms。Burst 仅统计普通样本中实际完成 holder 的刷新帧（每轮 200 帧）；两帧窗口每轮 100 个，排空帧不进入分布。

| 顺序 | Partial update p95 | Burst update median | Burst update p95 | 两帧累计 p95 |
|---|---:|---:|---:|---:|
| 01 custom | 3.1828 | 6.26725 | 6.4594 | 12.8256 |
| 02 stock | 4.2280 | 8.59585 | 8.8316 | 17.4248 |
| 03 stock | 4.2574 | 8.54745 | 8.7295 | 17.3306 |
| 04 custom | 3.1392 | 6.19370 | 6.4152 | 12.6652 |
| 05 stock | 4.3036 | 8.62135 | 8.8268 | 17.5112 |
| 06 custom | 3.1242 | 6.18080 | 6.3854 | 12.6347 |
| 07 custom | 3.1681 | 6.25720 | 6.4398 | 12.8039 |
| 08 stock | 4.2326 | 8.45240 | 8.5969 | 17.1244 |

以下为每变体四次独立运行统计量的均值，百分比按两者均值之差除以 custom 均值计算。

| 指标 | custom | stock | stock minus custom | 增幅 |
|---|---:|---:|---:|---:|
| Partial update median | 2.022450 | 2.498600 | +0.476150 | +23.54% |
| Partial update p95 | 3.153575 | 4.255400 | +1.101825 | +34.94% |
| Burst update median | 6.2247375 | 8.5542625 | +2.3295250 | +37.42% |
| Burst update p95 | 6.424950 | 8.746200 | +2.321250 | +36.13% |
| Burst 两帧累计 median | 12.4301250 | 17.0684875 | +4.6383625 | +37.32% |
| Burst 两帧累计 p95 | 12.732350 | 17.347750 | +4.615400 | +36.25% |

四组相邻反向配对的 Burst median 差分别为 `+2.32860 / +2.35375 / +2.44055 / +2.19520 ms`；前后两个四轮块的均值差为 `+2.341175 / +2.317875 ms`。custom/stock 的运行 median 极差为 `0.08645 / 0.16895 ms`。Partial median 四组差也全部为正，前后块为 `+0.4728 / +0.4795 ms`。本组观测到一致的 stock 场景额外 CPU 成本，但四次独立重复不建立普遍收益或总体显著性结论。

Burst generation median 运行均值为 `5.41910→7.7653125 ms`（+43.30%）；merge median 为 `0.79420→0.7774125 ms`（−2.11%）。不同分布的 median 不能相加重建 update median；本轮关闭叶阶段探针，不把整个差值定量归给单个 FillMesh 或 TMP 调用。Static update median 仅 `0.019125→0.019450 ms`，两块差值一正一负，不凭其百分比判断优势。以上仅比较固定代表性负载的两个真实依赖路径，不是新增运行优化，也不推导 GPU、FPS 或整帧收益。

端点电池温度在 29.0–30.0°C，全部 Thermal Status 0、无 override；轮间间隔 2.599–2.949 秒。整个正式组和会话没有日志记录的读失败/恢复，stderr 为零字节；这不证明 daemon 从未变化或不存在 DVFS/背景影响。host 完成时间减报告 UTC 为 14.408–16.696 秒，两者未经时钟校准，不解释为渲染延迟。虽然复用同一 custom APK，本组绝对时间与上一轮 generation 实验不同，不能据此宣称跨轮代码优化收益。

原 APK 完整 SHA-256 `73C94BEAC9721C4685C6C0AFE0EBEB49528114B7379A2DFCDF8400CB261B2D22` 恢复一致；九项设置、原文件名均读回一致。原请求不存在，已恢复缺席；安装输出 Success，应用已 force-stop，pidof 为空。所有恢复步骤完成，captureFailure 为 null。正常和失败注入的无设备恢复检查亦保留，不替代实际恢复读回。

第三次 Editor Profiler 退出后，再次仅用设备端哈希和只读查询确认上述 APK、九项设置、请求缺席、原文件名与停止状态仍一致。这个额外查询的首个本地 wrapper 因 Args 参数命名冲突在客户端失败，未发出有效设备命令或 mutation，失败日志独立保留；修正后的只读检查无重试，stderr 另有 79 bytes 的 daemon 启动提示。该检查与正式计时组的零失败/零恢复记录分开，不改写正式证据。

## stock Profiler：限定范围验收通过

按用户要求使用独立本地 Unity Profiler 工具，没有新增运行时 GC 机制。目标为单个连续运行的 39 个有界窗口、906 个报告关联的 HUD scope、每窗一个具有 Bytes metadata 与解析调用栈的分配正控，仅检查 Editor/Mono Main Thread。Editor 实际 Screen 为 640×480，输出 camera RenderTexture 为 2340×1080，不参与设备时间对照。

首次采集在第 3 窗遇到空 native live buffer；该窗 SaveProfile 仅 4 bytes，前两窗为预热，不能形成 HUD 分配结论。保留原 helper 快照后，仅在工具内增加不调用 AdvanceFrame、不发身份、不包 HUD scope 的有界 camera drain 等待，再以新目录重试；第二次在第 5 窗触发自有 Editor 工作集 3 GiB 上限。日志同时记录系统分页内存使用 93% 和 Profiler 丢帧，不据此确认单一故障原因。

第二次已有两段共 60 个 Static HUD scope 各观察到零个 GC.Alloc，但缺少 Partial/Burst 和完整证据，该次继续拒绝。两次原始证据均保留，没有拼接到第三次结果。

设备计时、恢复和原始帧分析结束后，第三次仅调整本地工具：在 Profiler 关闭时单次 BeginRun；非采样 warmup/模式校验的 AdvanceFrame 关闭分配调用栈记录并恢复。每个正式采样窗口在最近一次恢复后重新执行两帧 lead 和独立正控，随后整个采样窗口保持 GCAlloc 连续开启；断言禁止窗口混入 warmup/transition 或丢失 GCAlloc。保留原 AdvanceFrame 次数、39 窗和 906 帧范围。该调整减少范围外栈采集，不是运行时优化；不能确认前两次故障的唯一原因。

`stock-gc-editor-03` 的完整结果经分析器与主代理独立原始文件验收：

| 证据 | 结果 |
|---|---:|
| 保存的 native/live 窗口 | 39 |
| 检查的 Main Thread 帧记录 | 1368 |
| HUD scope 与报告帧一一对应 | 906（每模式 302，含两帧 drain） |
| 同线程 Bytes/解析调用栈正控 | 39，每个 4128 B |
| HUD scope 内 GC.Alloc | 0 事件 / 0 B |

正控包含实际分配字节 metadata、对应祖先 scope 与解析的 PositiveControl 调用栈；不是用 marker 时长代替字节。逐个核对所有 native `.data`、`.raw`、原始 live JSON、合并报告、来源清单、helper 和四个实际加载程序集哈希；临时 helper/.meta 已移除。pre-injection 清单 897 个输入与 stock APK 构建清单逐字节相同，fingerprint 仍为 `6D5403D79F8302EC35EC418CE32D5A3B95B6600E774D86D0FC88E603FD98B78D`。原仓库非执行文档差异另外记录，不伪称 APK 输入变化。helper SHA-256 为 `614B4177485253A205BF5DD9DE7920D85E7AD4AB31807250603F391E571994E4`。

结论限于 **stock Editor/Mono Main Thread、本次固定负载、完成预热后的 906 个受控 HUD scope**。范围外另计 132,549 个 GC 样本，未逐项归因；其他线程未查。采用 `readMode=live-buffer`、`captureSaved=true`、`loadSucceeded=false`，保存原生文件不等于完整离线重放已验证。Android IL2CPP 字节、全应用 GC 和其他负载不由此通过。上一轮 custom 证据只保留其自身适用范围，未代替本轮 stock 验收。

## 剩余门禁

- stock 本次受控 Editor scoped 字节已补齐；目标设备字节仍需要自身有效正控、范围和线程证据。
- 依据低扰动结果复核 TMP mesh regeneration，决定后续优化；不把探针开启阶段值直接当作正常成本，不调整原预算。
- 精确 RC 门禁继续绑定同一个候选 commit/version、源码清单、依赖解析与实际加载程序集、测试 XML/治理及覆盖率工件；UIDataRender 行覆盖率门槛仍为 70%。历史 dirty 工作树覆盖率不能替代候选提交；候选变更需相应重跑。
- 完整平台矩阵、同提交 CI 和原性能门禁未由本轮通过。包版本保持 0.1.2 Unreleased；未创建或推送 commit、RC、tag 或发布，不关闭 PERF-04/M5。

## 本地证据入口

原始证据和中间工具均在 ignored `TestResults/StockScenePerf/`，stock APK 在 `Builds/HudRc/StockScenePerf/`。关键文件包括 `protocol.md`、`build-registration.json`、`source-alignment.json`、`custom-reuse-audit.json`、`stock/player-input-review.json`、`stock/build-input-audit.json`、`root-apk-acceptance.json`、`root-focused-acceptance.json`、`analysis.json`、`root-release-acceptance.json`、`root-gc-acceptance.json`、`restoration.json`、`formal-run.log`、`profiler-attempts.json`、`stock-gc-editor-03/` 和前两个拒绝的 Profiler 目录。前轮 `TestResults/GenerationProfile/` 的 834 个已封存证据文件全量读回无变化，清单 SHA-256 仍为 `06D0222E5242FD40125D974334104CA55F4C3D3701D5B23561EA37D7BD23C18A`。

本轮 `evidence.sha256` 封存 443 个文件，清单 SHA-256 为 `E0451C8EB049DA0E6E2B286B44B95DA29BBE6A73B672D164767D5B19F6CD7A4A`。封存不包含 Python 缓存；APK 由冻结注册中的完整哈希另行绑定。原始工件不提交到仓库。
