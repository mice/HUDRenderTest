## Context

用户优先要求优化 128-holder 整体更新到 1.5 ms，并询问 Burst/SIMD 可行性。[两轮基线](../../add-holder-bucket-scheduling/evidence/holders128-results.md)使用 Mi 10、Release IL2CPP ARM64、Slim、128×3 无 Sprite Simple Image；每轮 1,800 帧，含 20 次全量刷新。整体 p95 为 3.7467 / 3.7664 ms；生成中位数 3.3193 / 3.3260 ms，合并 0.3173 / 0.3181 ms。达到目标需整体减少约 60%。

`Runtime/UIDataRender/Jobs/MergeJobs.Burst.cs` 的 `BurstMergeJob` 只合并已生成的数据；`ManagedCodeInJob` 则执行托管任务，不能视为 Burst 生成路径。测试模式名 `Burst` 表示周期性全量刷新，不证明执行了 Burst 编译代码。

## Goals / Non-Goals

**Goals:**

- 同步更新全部 128 个 holder，工作目标为 generation + merge（含上传）p95 ≤ 1.5 ms。
- 找出可避免的查询/准备开销，验证纯数据批量生成与 SIMD 的实际收益。
- 保留顶点、UV、颜色、索引、纹理槽、矩阵和当前帧可见性契约。

**Non-Goals:**

- 本 change 不实现两帧分桶；该计划见 [分桶探索](../../add-holder-bucket-scheduling/design.md)。
- 不把 128-holder 的 1.5 ms 目标当成原 RC 全矩阵验收通过或预算例外。
- 不承诺任意 UGUI/TMP 类型可直接进入 Burst。

## Decisions

### 先识别准备成本，再决定数据布局

生成内部 Simple path 中位数约 1.89 ms，其中 drawing dimensions 约 0.66 ms、transform 约 0.46 ms、write 约 0.29 ms；这些阶段有嵌套关系，不能简单相加。检查对象查询、矩阵获取、共享模板状态、重复初始化和计时扰动，逐项做可关闭实验。

本地 UGUI `Graphic.canvas` 在缓存为空时执行父 Canvas 查找；Runner 的 inactive 模板使重复空结果查找成为待验证假设。必须实测，不能据此直接跳过 `GetPixelAdjustedRect`。Canvas、父级、缩放、pixelPerfect 和 RectTransform 变更都需要正确失效。

### 纯数据批处理作为 Burst 候选

主线程读取必要状态，构建可复用的 quad 输入（尺寸、UV、颜色、矩阵、槽位和输出偏移），连续批量计算并写入预分配输出。共享模板逐 holder 修改的值必须在读取时正确快照，不能让所有 holder 使用最后一次赋值。

比较普通 C# 批量循环、同步 Burst 编译内核和一个或少量批量 Burst Job。避免每 holder 调度一个 Job。将输入准备、拷贝、调度、等待、输出转换和上传都计入整体刷新时间；内核微基准只用于定位。

Burst 支持纯值类型和 NativeArray，不能直接访问托管 Unity 对象，见 [官方类型支持](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/csharp-type-support.html)。优先连续数组、明确索引和简单循环，让编译器自动向量化；通过 ARM64 编译输出确认，再考虑手写 SIMD，见 [官方循环向量化说明](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/optimization-loop-vectorization.html)。是否采用 SoA、float4 及同步调用入口由真实输入布局和总成本实验决定。

### 回退与正确性

初始候选仅覆盖可证明等价的 Simple quad，其他类型走现有生成路径。比较 Managed/Slim、Sprite/null Sprite、父级变换、颜色和动态纹理等输出。尤其不能直接复用旧 Burst merge 的颜色行为：Slim 当前全零 Color32 输出为白色，原始数据复制未必相同。

### 真机验收

用同设备、相同配置、相同请求序列、关闭细粒度 quad 时间戳的对照采样。先确认路径实际命中、工作量和输出，再做至少 ABBA 四轮；每轮目标采集至少 100 次全量刷新，记录热状态、APK/source/report 哈希与原始 JSON/CSV。逐轮报告全量刷新 median/p95/max，不将非刷新帧混入百分位，也不相加各阶段 p95 代替总体 p95。所有候选轮全量刷新 p95 ≤ 1.5 ms 才满足本项预算。

## Risks / Trade-offs

- 小批量调度/复制可能抵消向量化收益 → 保留同步基线，以整体耗时选择路径。
- 缓存状态失效导致旧几何 → 明确失效条件并覆盖动态变更。
- 浮点重排造成差异 → 不预先开启 fast math，测试容差和视觉输出。
- 仅部分工作负载获益 → 明确支持范围与回退，补充 Partial/No-change 回归。
- 真机 allocation counter 不可用 → 标明 unavailable，并补本地分配证据，不宣称真机 0 GC。

## Migration Plan

先建立可关闭的候选路径，完成等价性与本地回归，再做真机对照。仅在证据支持时采用为默认；保留回退开关及失败诊断。分桶实施等待本项结果，避免同时更改刷新语义和生成内核导致无法归因。

## Open Questions

- 负 Canvas 查询、重复矩阵获取、诊断成本各占多少实际开销？
- 同步 Burst 与批量 Job 哪个适合 384 个 quad？数据准备与上传后还剩多少预算？
- 纯数据布局能否复用现有 mesh 存储，避免额外复制？

## 2026-10-25 实施进展

已增加可关闭的 per-holder/Image 阶段计时，整体 generation/merge 计时保留。首轮 Mi 10 诊断关闭的 p95 约 1.93 ms，仅属探索结果，不能将相对旧基线的差值宣称为运行时优化收益。

本地 inactive 模板查询实验：38,400 次 GetPixelAdjustedRect / rect 读取分别为 26.6988 / 4.2487 ms；相对矩阵计算 15.1691 ms。编辑器数据用于选择优化点，不外推真机预算。

首个候选采用显式 `UIImage.BeginGenerationBatch(root)` 值类型作用域：每批缓存 Canvas（包括空结果）、祖先关系和 root.worldToLocalMatrix；仍逐次读取尺寸、Sprite、颜色和子节点矩阵。调用方必须在作用域内保持父级/Canvas 层级与根变换稳定，变更后重新开始作用域。不同 root 的调用回到普通查询，Dispose 后不使用缓存，禁止嵌套，过期副本 Dispose 不影响新作用域。Sample 用预分配作用域数组和 try/finally；创建/释放成本均纳入整体 generation 计时。默认不开启该候选。

Burst/SIMD 批量纯数据生成仍待该候选的真机结果，不因关闭诊断或查询缓存而标记完成。

## 2026-10-25 实测决定

[四轮 36,000 帧对照](evidence/query-results.md)中，查询复用的两轮全量刷新 p95 为 1.2971 / 1.2310 ms，满足当前 1.5 ms p95 目标；原路径 p95 为 1.9776 / 1.9853 ms。第一轮候选有一次 1.5241 ms 峰值，不承诺硬最大耗时。

保留同步查询复用和显式 opt-in 调用契约。由于它已满足用户优先目标，纯数据 Burst/Job 原型与 ARM64 SIMD 检查后置到 JOB-01，未实施；tasks 的 2.2/2.3 已按这一实测决定调整，记录的是选择与移交。后续若工作量、预算或输入类型变化，再恢复这些实验。1.5 ms p95 门槛及 100 次刷新/轮要求不变，RC 整体验收仍未通过。
