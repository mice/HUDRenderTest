# Holder Bucket Scheduling Specification

## Purpose

Define opt-in, bounded holder scheduling that spreads complete generation, fill and upload work across two frames while preserving snapshot semantics, atomic publication, transparent input order and explicit resource lifetime. Require matched whole-workload evidence for the local 1.5 ms p95 update target without relaxing existing RC or allocation acceptance requirements.

## Requirements

### Requirement: Stable bounded holder buckets

显式分桶模式 SHALL 对目标 256-holder 负载建立两个稳定的逻辑桶，每桶最多 128 holders；普通几何变化 MUST NOT 改变桶归属。桶内 SHALL 保留纹理槽限制和正确局部映射，逻辑桶数 MUST NOT 被错误报告为物理 mesh 数。

首版 SHALL 接受 1..256 个无重复、显式有序成员，连续分桶；成员替换 SHALL 产生新的 epoch 并拒绝旧 handle。超范围、重复成员或单 holder 超纹理槽容量 SHALL 显式拒绝或由调用方选择原路径，不得静默破坏输出。

#### Scenario: Membership changes invalidate old requests
- **WHEN** 成员删除或重排后调用方使用旧 epoch 的 handle 提交
- **THEN** 提交被拒绝，旧待处理请求被取消，不产生幽灵几何

#### Scenario: Geometry changes without membership changes
- **WHEN** holder 的宽度、颜色、位置或顶点变化，而输入成员和顺序不变
- **THEN** holder 保持原桶归属，只有受影响桶被标脏

#### Scenario: Texture diversity needs extra meshes
- **WHEN** 一个逻辑桶不能装进单个纹理 batch
- **THEN** 桶内产生所需的多个物理 mesh，并正确报告 Draw Calls 和纹理映射

### Requirement: Two-frame complete-work scheduling

两桶跨帧模式 SHALL 在每帧调度点处理最多一个普通脏桶，将该桶的生成、填充和上传作为完整工作调度。对调度点之前提交的两桶刷新请求，调度器 SHALL 在当前帧和下一帧公平处理两桶，不允许持续更新使另一桶饥饿。

#### Scenario: Both buckets become dirty
- **WHEN** 两桶在帧 F 调度点前同时请求刷新且没有结构失效
- **THEN** 一个桶在 F 完成、另一个在 F+1 完成，并记录真实完成帧与更新年龄

#### Scenario: All buckets continue drawing
- **WHEN** 某桶尚未轮到刷新
- **THEN** 系统继续绘制其上一版有效网格，刷新成功前不清空或隐藏它

#### Scenario: Repeated updates do not starve the other bucket
- **WHEN** A 持续收新请求且 B 已待处理
- **THEN** 成功处理 A 后轮到 B，覆盖最新值不得重置 B 的首次待处理帧

#### Scenario: Submission follows the frame cutoff
- **WHEN** 请求在帧 F 的 Tick 结束后提交，或同帧重复调用 Tick
- **THEN** 新请求从 F+1 才参与调度，重复 Tick 不产生额外普通刷新；Tick 内重入修改和倒退帧号被拒绝

### Requirement: Explicit deferred update state

延迟模式 SHALL 使用独立于可变共享模板的 holder 更新状态或等价的明确生命周期契约，保留请求和完成的版本/帧信息。现有同步调用的默认执行时机 MUST NOT 因启用新 API 而被静默改变。

Submit SHALL 只保存完整目标快照而不生成 mesh；普通状态 SHALL 使用值字段或寿命受控的不可变资产引用，不得引用其他 holder 正在复用的可变模板。调度器 SHALL 独占 enrolled holder 的生成与位置修改。首版动态 Sprite/Text 资源或拓扑变更 SHALL 使用显式同步屏障。预热后的普通 Submit/Tick SHALL 避免新增状态容器、枚举器和每请求委托分配。

#### Scenario: Latest state supersedes pending state
- **WHEN** 同一 holder 在完成前收到 v1 和 v2 两份完整目标状态
- **THEN** 执行 v2 并记录其完成版本，保留首次待处理年龄；不得声称 v1 曾单独显示，位置也只随发布生效

#### Scenario: Shared template changes before a bucket is flushed
- **WHEN** 请求已排队且共享 UIImage 模板随后用于其他 holder
- **THEN** 待处理 holder 执行自身约定的目标状态，不读取其他 holder 的临时值

#### Scenario: Structural or resource invalidation
- **WHEN** holder 被删除、重排或使用的纹理映射失效
- **THEN** 系统执行明确的失效策略，避免幽灵几何和错误纹理，并将超出普通轮转的工作单独报告

### Requirement: Atomic publication and explicit resource lifetime

每桶 SHALL 复用独立 front/back mesh/material；选中桶全部生成、填充和上传成功后才发布并推进完成版本。异常 SHALL 保留有效 front、保留 dirty、进入故障状态，完整恢复成功前不得声称满足普通延迟。结构屏障 SHALL 在所有新桶 staging 成功后统一发布；外部 holder 和资产 SHALL 保留到屏障完成。Dispose SHALL 释放拥有的渲染资源、命令和订阅，不销毁外部资产。

#### Scenario: Generation or upload fails
- **WHEN** 刷新选中桶时 applier 或上传失败
- **THEN** 不发布部分输出、不推进完成版本，记录故障并等待显式完整恢复

#### Scenario: Texture mapping changes unexpectedly
- **WHEN** Tick/Draw 发现纹理槽版本变化，或普通 applier 过程中隐式更改资源
- **THEN** 进入同步恢复屏障，不提交可能使用错误资源的命令，屏障成本同时进入全帧数据和单独统计

### Requirement: Preserve transparent input order

分桶路径 SHALL 按输入顺序连续分批，不向较早物理 batch 回填。首版 SHALL 在 Built-in 单一 HUD camera 的独占 pass 内按桶和物理 batch 顺序提交全部 front；现有默认分批行为保持不变。图形等价 SHALL 使用实际图形设备验证，null graphics 跳过不得当作通过。

#### Scenario: Alternating textures exceed one batch
- **WHEN** 单槽容量下输入纹理顺序为 A/B/A
- **THEN** 产生三个连续 batch 并维持原透明覆盖顺序，报告真实 mesh 和 Draw 数量

#### Scenario: Overlap crosses the bucket boundary
- **WHEN** 第 128 和 129 个 holder 透明重叠或移动其 mesh bounds
- **THEN** 完成相同状态后像素覆盖顺序与有序同步参考一致，未刷新桶继续只绘制一次

### Requirement: Honest whole-workload comparison

验收 SHALL 对比单桶同步、双桶同帧和双桶跨帧的相同请求负载；同时报告每帧 CPU、两帧刷新窗口 CPU、可见延迟、完成数量、Draw Calls 和计数器可用性。接受额外一帧延迟 MUST NOT 被当作性能预算例外或 RC 验收通过。

三策略 SHALL 使用相同查询复用、关闭分段诊断、相同 shader 和提交机制，按 single/same-frame/rotating/rotating/same-frame/single 六轮捕获，每轮至少 100 次集中刷新请求。update CPU SHALL 从状态准备和 Submit 起计，包含调度、生成、填充、上传和发布；命令记录/绘制提交 SHALL 单列并计入整体 HUD CPU。普通刷新帧 update CPU p95 ≤ 1.5 ms SHALL 作为本 change 工作目标，原 RC 验收要求保持不变。

#### Scenario: Delayed execution coalesces requests
- **WHEN** 轮转路径合并多个版本且实际逐帧执行数不同于同步路径
- **THEN** 比较相同请求 checksum、完成版本和有限 drain 后的最终输出，分别报告首次等待年龄、最新值年龄与 drain 帧，不用执行 checksum 掩盖请求差异

#### Scenario: Burst spans two frames
- **WHEN** 原集中刷新通过两帧完成
- **THEN** 两帧都计入刷新窗口，不能遗漏第二帧成本或仅比较单个 mesh 耗时

#### Scenario: Result remains above budget or environment is incomplete
- **WHEN** 新策略仍未达到整体参考或关键设备计数器不可用
- **THEN** 保留对应未通过/环境受限状态、原始证据和 PERF-04 后续任务，不自行放宽验收门槛
