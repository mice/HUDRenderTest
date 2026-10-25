# 256-holder 分桶真机结果 — 2026-10-25

**结论：2×128 两帧轮转满足本 change 的普通刷新帧 update CPU p95 ≤ 1.5 ms 工作目标。** 两次集中刷新 p95 为 **1.3555 / 1.3413 ms**；保持相同请求和最终输出，最大额外完成延迟为一帧。原 RC 1 ms 参考、完整设备矩阵和真机 GC 验收仍未通过，本结果不是发布许可。

## 配置与口径

- Mi 10 / Android / Release IL2CPP ARM64 / Vulkan，60 FPS 目标，256 holders，每 holder 3 个 null-Sprite Simple quad；全部使用 Slim、查询复用、关闭分段诊断。
- 三策略相同 shader、完整状态快照、front/back 发布和有序 Built-in camera 提交。顺序为 single/same-frame/rotating/rotating/same-frame/single；另加一轮旧 Graphics.DrawMesh 控制。
- 每轮三种负载各预热 60 帧、采样 3,000 帧；主对照每模式另有 2 个明确标记的 drain 帧。六轮共 54,036 帧，控制另 9,000 帧，合计 **63,036 帧**。
- 每轮 100 次集中刷新请求；轮转统计其两个实际刷新帧，共 200 个刷新帧，未将后一帧隐藏为 idle。每个主对照报告保存 103,600 条逐 holder 请求记录。
- update CPU 从状态准备/Submit 起，包含调度、生成、合并/上传和发布。draw submission 与驱动 telemetry 单列；hudCpuMs 为驱动 Advance 的总 CPU，不包含 GPU 执行时间。两帧窗口累加两个完整帧的相应字段。
- 请求帧是单调 runner 帧号，分析器验证它与实际 Unity 帧号保持固定偏移。六轮请求 holder/version/frame 序列、最终完成版本和最终几何/UV/color/index SHA256 全部一致。
- 所有轮次记录的 Thermal Status 都为 0，电池温度范围 30.2–31.7°C。不存在基于温度或异常值删除样本的处理。

## 集中刷新结果（ms）

| 顺序 | 策略 | 刷新帧数 | median | p95 | max | 两帧累计 p95 |
|---|---|---:|---:|---:|---:|---:|
| 1 | bucket-single | 100 | 2.2771 | 2.3880 | 2.4706 | 2.4062 |
| 2 | bucket-same | 100 | 2.3839 | 2.5087 | 2.5916 | 2.5249 |
| 3 | bucket-rotating | 200 | 1.2353 | 1.3555 | 5.3688 | 2.6583 |
| 4 | bucket-rotating | 200 | 1.2265 | 1.3413 | 1.4607 | 2.6255 |
| 5 | bucket-same | 100 | 2.3559 | 2.4424 | 2.9664 | 2.4650 |
| 6 | bucket-single | 100 | 2.2656 | 2.3795 | 2.5050 | 2.3977 |
| 7 | bucket-legacy | 100 | 2.1680 | 2.2794 | 2.3418 | 2.2813 |

双桶同帧没有降低单帧更新成本。轮转把生成、填充和上传分配到两帧，使每帧 p95 低于 1.5 ms；两帧累计 p95 **2.6255–2.6583 ms**，高于双桶同帧的 **2.4650–2.5249 ms**。这是峰值分摊，不是总工作量减半。

轮转两次包含驱动 bookkeeping、draw submission、telemetry 的 hudCpuMs p95 为 **1.3980 / 1.3811 ms**。第一轮仍有 5/200 个刷新帧超过 1.5 ms，峰值 **5.3688 ms**，对应两帧窗口峰值 9.1281 ms；第二轮 0/200 个刷新帧超过 1.5 ms，峰值 1.4607 ms。**达标口径是 p95，不是硬最大值**；长尾原因未由本次数据单独确定。

旧 Graphics.DrawMesh 控制的全量更新 p95 为 2.2794 ms。它没有新状态队列和 front/back 发布，因此其差值不能全部归因于 CommandBuffer；三策略的主要结论来自前六轮同源、同提交方式对照。

## 生命周期与成本

每桶有 front/back 两套 renderer。单桶对照拥有 2 个 mesh/material，双桶拥有 4 个；每帧只提交 front，所以普通 null-Sprite 场景分别是 1 / 2 次绘制。初始化两轮单桶为 6.3217 / 16.3012 ms，双桶同帧 7.7131 / 7.0778 ms，轮转 7.1356 / 7.0570 ms；初始化不混入普通刷新目标。

Unity 的对象内存查询分别返回 2,928 / 5,856 bytes，原值已保留；这只是引擎报告的对象估算，**不能作为完整 mesh buffer、GPU 显存或总内存成本**。托管队列、scratch buffers、外部 holder/资产均不在该口径内。双份几何存储的代价不能忽略。

本地 warm Submit/Tick 测试使用正向校准 GC.Alloc 计数器，观测为零事件；真机 scoped/frame GC 计数器仍不可用，报告保留 unavailable/-1，不宣称 Android 或全应用零 GC。

## 证据与复现

- [汇总 JSON](bucket-summary.json)、[源码/APK 清单](bucket-builds.json)、[源码哈希](bucket-sources.sha256)。四个包使用相同源码指纹 `610BC70253BD8340D4B0E5C757EDA7115799F833427081BE13C831AF17241FD4`。
- [原始报告 ZIP](bucket-reports.zip)：七轮 JSON、CSV、六轮额外 bucket CSV、设备 metadata 和构建清单。APK 保存在工作区 Builds/HudRc/Xiaomi10，哈希已入清单，不作为仓库源码提交。
- [采样脚本](capture-buckets.ps1)、[完整分析器](analyze-buckets.py)。将 ZIP 解压到目录后运行 `python analyze-buckets.py REPORT_DIRECTORY OUTPUT_JSON`；可选 PREFIX_COUNT 仅作运行中的局部检查，不代表完整验收。
- [本地实现验证矩阵](implementation-validation.md)：161/161 EditMode、31/31 实际图形设备 PlayMode，包括生产 shader 多纹理 A/B/A、跨桶透明重叠、异常不发布和完整恢复。

## 后续与重开条件

PERF-04/M5 保持开放：原 1 ms 参考仍未达到；继续补设备分配计数器、代表性 Sprite/TMP/纹理负载、精确候选 CI/覆盖率和独立分发证据。若要求硬最大值、资源在普通路径动态变化、多 camera/SRP、超过 256 holders 或更紧预算，需重新设计/验证，不把本次局部结论外推。Burst/SIMD 未加入本实现，后续评估仍归 JOB-01。
