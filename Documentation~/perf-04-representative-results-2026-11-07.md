# PERF-04：仓库资源负载与设备分配诊断

日期：2026-11-06–07（Asia/Shanghai）。范围：M5 / PERF-04 / 0.1.2 Unreleased。变更：[measure-representative-hud-workloads](../openspec/changes/archive/2026-11-11-measure-representative-hud-workloads/proposal.md)。

后续：TMP 提取缓冲复用与 11 月 8 日设备对照见 [最新结论](perf-04-tmp-scratch-results-2026-11-08.md)。本文保留优化前的实测结果；正式 HUD 采样为竖屏，后续横屏对照另有协议，不直接比较跨轮绝对值。

## 结论与门禁

本轮完成可复现的 Sprite/TMP/三纹理负载、来源记录、分配事件校准和设备重复测量，**PERF-04 整体验收继续开放**。

- 混合负载四轮同 APK 的输入及最终几何一致。轮转降低 Burst 单帧 update 峰值；两帧总成本及 Partial p95 没有稳定下降，不能宣称整体性能改善。
- 混合负载的 Burst 刷新 update p95 为完整刷新 **21.1027–23.2966 ms**、轮转 **10.7821–11.4473 ms**，观测值高于原 1 ms 参考。历史 null-Sprite 当前两轮为 **2.0837 / 2.5211 ms**，本轮没有复现旧局部 1.5 ms 目标。
- Development 正向校准后，混合负载刷新存在托管分配事件；历史 null-Sprite 的测量 HUD 范围内没有观测到事件。Release 分配计数器不可用，设备 scoped 字节数仍不可用，不作 Release 或全应用零 GC 结论。
- 本轮没有修改包 Runtime，没有前后版本优化对照。旧结果与当前控制的差异不能直接归因为代码回退。
- 这是可追溯的 dirty working-tree 测量，不是已冻结 RC 的验收。stock UGUI 场景性能、完整 RC 矩阵和本轮覆盖率仍未验证。未变更预算、包版本或发布状态。

## 负载与环境

用户选择仓库现有资源构造固定负载。每个 holder 为三张 Simple Sprite 加 `HP 1000`–`HP 1010` 的主 atlas TMP 文本：6 个可见字形、36 顶点、54 索引。256 holders 共 9,216 顶点、13,824 索引；历史对照仍为 256×3 个 null-Sprite 四边形。资源共享模板通过公开 holder generation、生产 merge/upload 和 camera draw 路径生成；自动 Canvas 绘制关闭。

| 资源 | 尺寸 | 用途 |
|---|---:|---|
| `Assets/Art/Atlas/Main/01_0040.png` | 100×100 | 图片槽 1 |
| `Assets/Art/Atlas/Login/02_0041.png` | 142×47 | 图片槽 2 |
| `Assets/Art/Atlas/IconCtrl/02_11.png` | 35×35 | 图片槽 3 |
| `LiberationSans SDF.asset` | 1024×1024 atlas | 主材质字形 |
| `UIE-TMP-SDF.shader` | — | 三纹理与 SDF 混合绘制 |

报告包含资源文件与 `.meta` SHA-256、尺寸、请求 ID/原始 JSON 哈希、完整配置、源码指纹、build GUID/type 和 UGUI 来源。预计算文本变体，分桶快照携带变体索引；资源槽经 `UpdateTextureIndex` 更新 UV，再用 `MaterialBinder` 绑定，不向全局纹理注册表添加临时资源。

设备为 Mi 10 `791d02af`，Android 11/API 30，Unity 2022.3.53f1，IL2CPP ARM64，Vulkan。**2026-11-08 按原始 display 证据更正**：六轮正式计时及两轮 Development 诊断的测后实际方向均为 `0 / 1080×2340`（竖屏），此前写成正式横屏 2340×1080 不准确；原始报告、耗时和哈希不变。目标 60 FPS；预热 60 帧/模式，采样 3,000 帧/模式，seed 12345，Partial 10%，Burst 每 30 帧请求 256 holders。启用 Slim/query reuse，关闭 holder stage 与 allocation-event 诊断。每模式额外两帧排空，单轮 9,006 帧、100 个完整 Burst 窗口。

先独立检查画面：初始竖屏裁掉部分列；独立 `smoke-landscape` 锁定横屏后确认全部 16×16 holders 可见，实际记录为 `1 / 2340×1080`。截图发生于该 smoke 的 600 帧预热中，没有在正式计时采样中截图；这张横屏截图不能证明后续正式轮次也是横屏或全部 holder 可见。2026-11-08 短测定位到 APK 安装会重置设备 user_rotation，后续按每次安装后设置并验证实际方向的协议重采；不直接用本轮竖屏绝对耗时归因后续横屏优化收益。所有 capture 起止 thermal status 为 0、override false；电池温度 26.0–26.7°C。控制器每三秒通过 ADB 查询报告是否完成，此开销在各轮一致；没有采样期文件导出或逐帧日志。

## Release 计时结果

`update` 包含请求准备/队列、生成、merge/upload 和发布；`HUD` 另含 draw submission/telemetry。两帧窗口包括请求帧与后一帧，SingleBucket 也保留后一帧的空闲 update 成本。表内 p95 为各轮独立 nearest-rank 统计，不混合不同轮次。

| 顺序 / 负载 / 策略 | Partial update p95 (ms) | Burst 刷新 update p95 (ms) | Burst 刷新 HUD p95 (ms) | Burst 两帧 update p95 (ms) |
|---|---:|---:|---:|---:|
| 01 混合 / SingleBucket | 4.9650 | 23.2966 | 23.3537 | 23.3147 |
| 02 混合 / Rotating | 5.0426 | 10.7821 | 10.8348 | 20.4229 |
| 03 混合 / Rotating | 5.2347 | 11.4473 | 11.4991 | 22.6346 |
| 04 混合 / SingleBucket | 4.8828 | 21.1027 | 21.1426 | 21.1720 |
| 05 历史 / Rotating | 1.7386 | 2.0837 | 2.1447 | 4.2539 |
| 06 历史 / Rotating | 1.5086 | 2.5211 | 2.6326 | 5.2534 |

四轮混合按 ABBA 顺序完成，36,024 个含排空样本。两轮历史控制共 18,012 个含排空样本；Release 合计 54,036 个。每个模式的请求 holder/frame/version 序列一致，四轮三组最终几何 SHA-256 一致，所有请求在至多额外一帧内完成，所有最终请求版本与完成版本相同。

混合 SingleBucket 的 Burst 生成中位数为 18.2277 / 17.5539 ms，merge 为 1.9309 / 2.1708 ms，生成仍占主要成本。Rotating 的刷新 update 最大值为 14.0895 / 21.4804 ms，不能承诺硬最大帧耗时。Burst 空闲帧很多，不能用 all-frames p95 掩盖刷新成本。

Release Main Thread 计数器有样本；frame GC 与 scoped allocation 字节没有有效样本。完整帧还包含 pacing、引擎和其他线程工作；本表不能证明完整帧率或 GPU 收益。历史旧 1.3555/1.3413 ms 保留为当时条件的记录，不作为当前验收通过证据。两轮当前控制输出相同，但缺少同环境旧二进制配对，暂不归因差异。

## 分配诊断

独立 Development 包使用 `Development | ConnectWithProfiler` 构建；诊断耗时不参与上述 Release 比较。每负载运行 300 帧/模式、每模式两帧排空，共 906 帧。`GC.Alloc` recorder 仅收集当前线程，在 HUD 工作前 reset/start、工作后 stop，容量 16,384；已知 `new byte[4096]` 校准各得到 1 个事件，无饱和。

| 负载 | Static 事件/帧 | Partial 事件/帧 median / p95 / max | Burst 刷新事件/帧 | scoped 字节 |
|---|---:|---:|---:|---|
| Sprite3 + TMP6 | 0 | 26 / 52 / 52 | 128（20 个刷新帧） | unavailable |
| 历史 null-Sprite | 0 | 0 / 0 / 0 | 0 | unavailable |

混合负载每个普通样本的事件数恰等于该帧 completed holders。它与 `TMPMeshDataUtility.Fill` 每次新建 `VertexHelper` 的实现相符；尚未采集逐分配调用栈，归因需局部对照确认。历史范围零事件仅适用于该 Development 诊断的当前线程 HUD 范围。

`GC.Alloc` 描述的单位为 **TimeNanoseconds**；两次控制原始值 677 / 312 为时间值，不能当作字节。事件可用与字节可用分别标记，所有不支持字段用 flag false 与 -1。IL2CPP 全程不调用 `GC.GetAllocatedBytesForCurrentThread`。Release 启用事件诊断的独立 smoke 未观察到控制事件，因此不能用 Release 的零值建立分配结论。

## 来源、验证与恢复

| 项目 | 身份 |
|---|---|
| HEAD | `520b4c16c2de0ea2741a66c5e94c1b61d5fa461d`，dirty working tree |
| 两个构建相同源码 manifest SHA-256 | `2E95D811E8251F49EE43E937A85D6DF857CC99A26472EA1A6992F75A6D5263B4` |
| Release GUID | `9b48eb9cfcf7430d87d5cfef17d7881c` |
| Release APK SHA-256 | `E924501C42C8F142E72276796148D6F7F3E88BFB8A7719F38A83DD0947A17CAF` |
| Development GUID | `b73dfd71db0949108fa49f23a2fca14b` |
| Development APK SHA-256 | `BC87FDCF839B520622E9FC17A85BA57D9191EA46896571FBECCAEE4D8B07F970` |
| 实际定制 UGUI VertexHelper SHA-256 | `40EE6F876DEEB7BF8E4084B5B1920E087914D3C1F6E308A7D8759951A5479AE9` |

报告反射确认编译程序集存在兼容的直接 `CopyIndices(int[], int, int)` 方法。本轮测的是该路径；[VERIFY-02](verify-02-stock-ugui-results-2026-11-06.md) 的 stock 正确性/提取分配证据继续有效，但没有在 stock 环境测本轮资源场景性能。

收尾对构建 manifest 的 1,165 个文件逐一读回核对；仅包 README/changelog 因补充最终结论而变化，可执行源与资源输入未变。差异和原始哈希保存在 `build-input-readback.json`，构建时的 source fingerprint 不因文档收尾重命名。

没有可用 Unity MCP，使用原 Sample 与既有脚本验证。runner 专项 11/11、完整 EditMode 196/196、D3D11 PlayMode 64/64，通过且无跳过；UT_PERF_021..023 覆盖槽位/字形/材质、三策略一致性、资源清理、非法负载、正向分配控制及饱和。治理 260/260，布局、OpenSpec strict 21/21 和 diff 检查通过。首次测试暴露 TMP 未初始化导致空提取及计数器值误作字节的问题，修正后完成专项及全回归；失败证据保留。

原 APK 与设备设置在安装前备份。结束后重装原 APK、pull 回来核对 SHA-256 为 `73C94BEAC9721C4685C6C0AFE0EBEB49528114B7379A2DFCDF8400CB261B2D22`，恢复原先不存在的 `hud_rc_request.json` 状态、应用停止状态及 9 项设置；原文件名清单仍完整。用户已有 manifest/lock、场景、prefab 和其他项目的 Unity 实例未修改。

## 本地证据索引

原始证据保持本地，不提交。`TestResults/Perf04/` 中保留：

- `timing-01-single`、`timing-02-rotating`、`timing-03-rotating`、`timing-04-single`、`timing-05-historical`、`timing-06-historical`：原始 JSON/CSV、请求与哈希、起止环境、内存及 APK/report 身份。
- `diagnostic-01-mixed`、`diagnostic-02-historical`：独立分配诊断；`smoke-release-events`、`smoke-landscape`：独立画面/计数器检查。10 份报告共 56,220 个含排空样本，不把这些不同用途样本合并作性能统计。
- `release/development-sources.sha256`、构建日志、`analysis.json`、`capture.ps1`、`analyze.py`、`unity-validation.json`、专项失败/通过和完整测试 XML/log。
- `device-original/`、`restoration.json`：原 APK、设置、文件清单及实际恢复读回。`evidence.sha256`：本地证据完整性清单。
- APK：`Builds/HudRc/Perf04/representative-release.apk`、`representative-development.apk`。

复现使用 `Tools/Invoke-HudRcAndroidValidation.ps1` 的 `-RepositoryResources -UseSlim -GenerationBatch -DisableBuildMeshDiagnostics -BucketStrategy Rotating -SampleFrames 3000 -WarmupFrames 60 -TargetFrameRate 60`。诊断另加 `-Development -AllocationEvents`，使用独立输出 APK。可把报告的完整 `configuration` 与唯一 `requestId` 包装为 `RunRequest` JSON，写入 Player persistentDataPath 的 `hud_rc_request.json` 后冷启动；报告保留原始请求字节哈希。没有请求时使用 APK 内置配置。资源引用只写入临时构建场景。

## 下一步 / TODO 出口

先对固定文本、变化文本和图片控制做生成阶段/分配调用栈对照，验证 TMP 提取缓存复用的收益及清理契约；再以同环境配对构建复核当前 null-Sprite 基线。若后续需要调整使用预算，必须提供测量、风险和重开条件并获得明确接受，本轮不作预算例外。

整体复核活动 TODO 仍为 9 项：PERF-04、GOV-002、MAINT-01 延续 M5；PERF-02/03、JOB-01 延续 M3；TMP-01..03 延续 M4。GOV-002 仍 blocked/unverified；JOB-01 按预算触发。本轮不关闭任何整体 TODO，也不复用 ID。Jenkins 不在当前范围。
