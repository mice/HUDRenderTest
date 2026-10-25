## Context

状态：**探索已收敛为实施方案，尚未实现分桶**。用户接受 256 holders / 128 容量 / 两帧轮转及后一桶额外一帧延迟。当前 128-holder 同步查询复用的 Mi 10 p95 为 1.2971 / 1.2310 ms；证据见 [优化归档](../archive/2026-10-25-optimize-128-holder-generation/evidence/query-results.md)。这是局部结果，不能外推为双桶已达标。历史带分段诊断的 7 ms / 3.75 ms 数据不再用作新方案的生产耗时基线。

代码依据：`DataPrefabHolder.BuildMesh` 执行生成；`PrefabHolderExt.SetWidth/SetSprite/SetText` 立即生成；`HolderBatchRenderer.Rebuild` 填充/上传全部纹理 batches；`MergeBatcher.Plan` 使用 first-fit，会将后续 holder 放回早先 batch；`HolderNotifier` 只处理纹理槽变化，`HolderLifecycle` 的 HashSet 不提供绘制顺序。`HolderBatchRenderer.Rebuild` 会逐个 Clear/上传 mesh，故异常后不能假定原输出仍完整。

## Goals / Non-Goals

**Goals:** 在生成之前排队，稳定两桶、公平轮转、保留旧可见状态；以确定的透明绘制顺序和明确资源失效边界完成整条刷新链路；测量每帧更新成本、两帧总成本和更新年龄。

**Non-Goals:** 本轮不写运行时代码；不把同步 API 静默改为异步；不引入 Burst/Job；首版不承诺 SRP、多 camera 与任意外部透明物体交错排序，也不承诺任意复杂 holder 都能装进一个物理 mesh。

## Decisions

### 1. 稳定身份、顺序与容量

初版有界范围 1..256 holders、容量固定 128，按显式输入序列连续分桶（0..127、128..255）。输入不能来自无序 HashSet；禁止重复 holder。每次替换成员序列产生新的 membership epoch，handle 由 epoch + index 构成，旧 handle 的提交被拒绝。普通状态更新不改变顺序或桶归属；超过范围显式拒绝，不沿用“两帧完成”的保证。

桶 ID、桶内物理 mesh ID 和全局绘制序号分别记录。任一 holder 自身需要的纹理槽数超过单 mesh 容量时，初始化/结构刷新明确拒绝或由上层选择原路径；首版不暗中画错，也不在本 change 扩展单 holder 拆分。

### 2. 状态快照与 API 契约

拟新增独立的 `HolderBucketScheduler<TState>` 与 `IHolderStateApplier<TState>`；具体命名可在实施时调整，以下语义固定。

| 操作 | 约定 |
|---|---|
| ReplaceMembers(ordered bindings, initial states) | 同步建立新 epoch，预分配两桶的状态/dirty/位置缓冲；生成所有初始网格后才能首次绘制 |
| Submit(handle, in state, frame) | 按值保存完整目标状态，返回本 holder 单调递增的 request version；只排队，不调用 BuildMesh |
| Tick(frame) | 同一主线程、每帧至多一次；普通模式刷新最多一个脏桶，返回执行/待处理/版本/延迟统计 |
| FlushAll(reason) | 初始化、成员/顺序、模板层级、资源失效等同步刷新；单独报告原因、数量和耗时 |
| RecordDraws / Draw adapter | 提交全部 front 网格，独立于本帧哪个桶发生刷新 |
| Dispose | 解除 camera 命令缓冲与事件订阅，销毁拥有的 mesh/material，释放状态引用；不销毁外部 holder/纹理 |

`TState : struct` 不等于自动深拷贝：契约要求纯值字段，或由调用方保证寿命的不可变资产引用；禁止捕获共享模板、可变数组/List 或每请求闭包。首个 Sample 状态使用宽度、颜色和位置等值字段。模板/资源布局在普通 epoch 内固定，动态 Sprite/Text 导致资源或拓扑变化时走显式 FlushAll，不宣称首版已经支持通用 UI 属性异步编辑。

Applier 注册时绑定一次，执行时完整重放该 holder 的目标状态，再调用一次 BuildMesh；不得使用会立即生成的 PrefabHolderExt setter 来制造额外重建。支持共享模板的 applier 可在选中桶内部开启 `UIImage.BeginGenerationBatch`；不跨帧保留该作用域，遵守其根变换和 Canvas 稳定契约。不满足契约的 adapter 使用现有安全生成路径。

调度器独占 enrolled holder 的生成/位置状态；调用者不能绕开它同步修改后再声称延迟契约成立。现有同步 API 仍保留，切换回同步模式前需显式 flush 或重建成员。普通 Submit/Tick 在预热后不创建委托、状态容器或枚举器。

### 3. Latest-wins、公平性与帧定义

每个 holder 保存 requestedVersion、completedVersion、最新目标状态、firstPendingFrame 和 latestRequestFrame。干净到脏时设置 firstPendingFrame；后续覆盖只更新目标值/version/latestRequestFrame，不重置首次待处理帧。中间状态可以被合并；完成版本 V 表示之前的请求已被 V 覆盖并处理，不表示每个中间值都显示过。

Tick 起始定义提交截止点；Tick 内禁止重入 Submit/Replace/Flush，重复 Tick 同帧不再执行刷新，倒退 frame 报错。截止前的请求本帧 eligible；截止后提交的请求从下一帧 eligible。两桶同时待处理时按游标轮转，刷新成功后游标移到下一桶；扫描可跳过干净桶，但不能让持续变化的 A 饿死 B。对截止前请求，普通成功路径最迟本帧或下一帧完成；故障/结构屏障必须明确报告，不套用此延迟保证。

```text
F 截止前：A、B 请求 v1
F Tick：生成/上传 A(v1)；绘制 A(v1) + B(旧)
F+1 截止前：B 收到 v2，覆盖待处理 v1，但保留首次请求帧 F
F+1 Tick：生成/上传 B(v2)；绘制 A(v1) + B(v2)
```

报告同时记录首次待处理年龄与最新目标年龄，不能通过不断覆盖版本把等待时间清零。待处理 position 也属于快照，直到对应桶发布新 mesh 才改变可见位置。

### 4. 网格发布、失败与结构/资源屏障

每桶持有 front/back 两套可复用 `HolderBatchRenderer`，内含独立 mesh/material。生成只更新待处理 holder 的 mesh data；随后 back 对整个选中桶填充/上传，成功后才交换 front/back。未选中桶不 Fill、不上传，但继续绘制 front。front/back 增加内存和预热成本，必须报告，不把它等同于每帧多画一套。

任意 applier/上传异常：不发布该桶、不清 dirty、不推进 completedVersion，保留 front 并暂停普通调度，返回 Faulted；不能无限重试并伪造一帧延迟。显式恢复必须从完整最新状态重新生成，已部分写入的 holder 缓存不能直接当作成功结果。首次初始化失败则没有可绘制 front。

成员删除/重排/数量变化使用同步结构屏障，在下一次 Draw 前建立新 epoch；旧 epoch 请求全部取消并清除引用。根/Canvas/模板结构改变使受影响几何重新生成，不能仅重用旧 holder mesh。删除期间调用方不得提前 Dispose 仍被调度器引用的 holder。

纹理槽 Version 在 Tick/Draw 前校验；资源变更使用显式屏障，保守刷新所有相关桶并重新绑定槽。禁止在普通 applier 中隐式注册/释放纹理。如果执行中发现版本变化则进入 NeedsResync，停止提交可能错误的旧资源命令，直到显式同步刷新成功。调用方保留旧资产寿命直到屏障完成；引用一个 Texture 并不能阻止外部 Destroy。

结构屏障必须把所有新桶 staging 成功后统一发布；失败时若旧输出仍有效则保留旧输出，否则明确暂停绘制并报错。屏障成本纳入整体帧统计，同时另列原因/耗时，不混入“普通单桶”子集冒充达标。

### 5. 透明顺序与绘制接入

桶内新增连续分批模式：只尝试最后一个物理 batch，槽位容不下就开新 batch，不回填早先 batch。既有 MergeBatcher first-fit 默认行为保持不变。例：槽容量 1，输入纹理 A/B/A 必须得到三个连续 batch，不能合成 A/A、B。先保证输入顺序，再统计额外 Draw Calls。

初版 Sample 明确使用 Built-in、单一指定 HUD camera、独占 HUD pass。采用一个复用的 CommandBuffer，按“桶顺序 → 桶内连续 batch 顺序”记录 DrawMesh，使用明确 submesh 和 shader pass，包含所有 front 网格；不要把多次 Graphics.DrawMesh 调用顺序当作透明排序保证。建议接入 AfterForwardAlpha；三种性能策略使用同一提交机制，并补记录旧 Graphics.DrawMesh 基线以辨别接入成本。跨外部透明物体/Canvas 的排序不在首版承诺内。

本地 GraphicsSettings 的 render pipeline asset 为 0，支持先做 Built-in 实验。Unity API 依据：[CommandBuffer.DrawMesh](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rendering.CommandBuffer.DrawMesh.html)、[Camera.AddCommandBuffer](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Camera.AddCommandBuffer.html)、[AfterForwardAlpha](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rendering.CameraEvent.AfterForwardAlpha.html)。这是待验证的接入选择，不是已经通过的视觉等价证明。

命令记录只能在刷新发布结束后进行；不复用引用过期 front 的旧命令。初始化/Dispose 时负责添加、移除、释放；双桶同帧与轮转路径都不能重复添加或重复绘制。必须用实际图形设备的重叠透明像素测试验证顺序，null graphics 下的跳过不算视觉验收通过。

### 6. 公平对照与预算

比较 256 单桶同步、2×128 同帧、2×128 轮转。全部开启同样的查询复用、关闭 per-holder/Image 和 quad 分段计时，复用同一请求事件序列、shader、绘制提交路径和源清单。六轮采用 single/same-frame/rotating/rotating/same-frame/single，每策略两轮，每轮至少 100 次集中刷新请求。

请求必须先保存快照，计时从状态准备/Submit 开始，包含调度、生成、填充/上传及发布；首个双桶验证以普通刷新帧 update CPU p95 ≤ 1.5 ms 为工作目标，不把其改写为原 RC 1 ms 已通过。命令记录/Draw 提交单列，并报告整个 HUD CPU 总和与物理 mesh/Draw 数量。两帧刷新窗口要累计两帧全部相关 CPU（包括第二帧新请求入队成本），不得只统计首桶或把第二帧算作 idle。

比较请求 checksum 与最新状态，不要求延迟策略逐帧实际执行 checksum 等于同步基线。记录完成的 holder/version、firstPendingAge、latestValueAge、pending 数量以及实际 scope/bucket/mesh 数。停止请求后允许有限 drain 帧，三种策略最终几何、槽位和可见输出必须相同；drain 单独标记，不能悄悄延长延迟承诺。

首次构建、结构屏障、失败和资源恢复分别报告；它们仍出现在全帧数据中。真机 allocation counter 不可用时保持 unavailable。在完成双桶实测、排序验证前，1.5 ms 只是目标，PERF-04/M5 继续开放。

## Validation plan

| 类型 | 必须覆盖 |
|---|---|
| EditMode 调度 | 1/128/129/256 边界、超范围/重复成员、stale handle、latest-wins、持续脏 A 不饿死 B、截止后提交、同帧重复 Tick |
| EditMode 输出/寿命 | 两 holder 共享模板但状态不同、位置延迟一致、异常不发布、完整恢复、换 epoch 清引用、纹理版本屏障、预热分配正向校准 |
| EditMode 分批 | 连续 A/B/A 顺序、槽位超限与单 holder 超容量处理、几何等价、front/back 不别名 |
| PlayMode 图形 | 128/129 跨桶透明重叠，移动 mesh bounds 后顺序仍正确；多纹理 batch 与单桶参考像素一致；相机命令添加/释放恰好一次 |
| Mi 10 | 同源三策略六轮、100 请求窗口/轮、每帧和两帧统计、更新年龄、实际两个桶均每帧绘制、APK/source/report/CSV 哈希 |

## Migration Plan

探索 1.4 的接口与语义决策已完成；下一任务为 2.1 的调度器/状态快照实现，然后 2.2 的 front/back、顺序与资源屏障，再做测试和同源真机对照。所有新行为 opt-in，现有同步 API 和默认 renderer 不变。若视觉或资源契约无法满足，不以仅数值性能达标替代正确性。

## Deferred scope

SRP、多 camera、与外部透明物体交错、任意 Sprite/Text 资源在普通延迟路径的生命周期、单 holder 超槽拆分和超过 256 的延迟契约均后续处理。本轮无阻止首版实施的产品问题；排序接入、内存和双桶预算是明确的实施验证门槛。
